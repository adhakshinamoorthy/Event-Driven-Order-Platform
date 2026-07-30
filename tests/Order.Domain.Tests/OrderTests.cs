using BuildingBlocks.SharedKernel;
using FluentAssertions;
using Order.Domain;
using Xunit;

namespace Order.Domain.Tests;

public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidLines_CreatesPendingOrderAndRaisesEvent()
    {
        var order = Domain.Order.Create("customer-1", "usd", [("sku-1", "Keyboard", 2, 50m)]);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Total.Should().Be(100m);
        order.Currency.Should().Be("USD");
        order.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Create_WithoutItems_IsRejected()
    {
        var act = () => Domain.Order.Create("customer-1", "USD", []);
        act.Should().Throw<DomainException>().WithMessage("*at least one item*");
    }

    [Fact]
    public void Cancel_Twice_IsRejected()
    {
        var order = Domain.Order.Create("customer-1", "USD", [("sku-1", "Keyboard", 1, 50m)]);
        order.Cancel("Customer request");
        var act = () => order.Cancel("Again");
        act.Should().Throw<DomainException>().WithMessage("*cannot be cancelled*");
    }
}
