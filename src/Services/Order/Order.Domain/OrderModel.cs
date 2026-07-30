using BuildingBlocks.SharedKernel;

namespace Order.Domain;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new DomainException("Money cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Currency must be a three-letter ISO code.");
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }
    public static Money operator +(Money left, Money right) =>
        left.Currency == right.Currency
            ? new(left.Amount + right.Amount, left.Currency)
            : throw new DomainException("Currencies must match.");
}

public enum OrderStatus { Pending, InventoryReserved, PaymentAuthorized, Shipped, Completed, Cancelled }

public sealed class OrderItem : Entity<Guid>
{
    private OrderItem() : base(Guid.Empty) { ProductId = string.Empty; ProductName = string.Empty; UnitPrice = null!; }
    internal OrderItem(string productId, string productName, int quantity, Money unitPrice) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(productId)) throw new DomainException("Product id is required.");
        if (string.IsNullOrWhiteSpace(productName)) throw new DomainException("Product name is required.");
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        ProductId = productId.Trim(); ProductName = productName.Trim(); Quantity = quantity; UnitPrice = unitPrice;
    }
    public string ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public decimal LineTotal => UnitPrice.Amount * Quantity;
}

public sealed record OrderCreatedDomainEvent(Guid EventId, DateTimeOffset OccurredOn, OrderId OrderId) : IDomainEvent;
public sealed record OrderCancelledDomainEvent(Guid EventId, DateTimeOffset OccurredOn, OrderId OrderId, string Reason) : IDomainEvent;

public sealed class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];
    private Order() { CustomerId = string.Empty; Currency = string.Empty; }
    private Order(OrderId id, string customerId, string currency, IEnumerable<OrderItem> items)
    {
        Id = id; CustomerId = customerId; Currency = currency; _items.AddRange(items);
        if (_items.Count == 0) throw new DomainException("An order requires at least one item.");
        if (_items.Any(x => x.UnitPrice.Currency != Currency)) throw new DomainException("All item currencies must match the order currency.");
        Status = OrderStatus.Pending; CreatedOn = DateTimeOffset.UtcNow; UpdatedOn = CreatedOn;
        Raise(new OrderCreatedDomainEvent(Guid.NewGuid(), CreatedOn, Id));
    }
    public OrderId Id { get; private set; }
    public string CustomerId { get; private set; }
    public string Currency { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }
    public DateTimeOffset UpdatedOn { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public decimal Total => _items.Sum(x => x.LineTotal);

    public static Order Create(string customerId, string currency, IEnumerable<(string ProductId, string Name, int Quantity, decimal UnitPrice)> lines)
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new DomainException("Customer id is required.");
        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var items = lines.Select(x => new OrderItem(x.ProductId, x.Name, x.Quantity, new Money(x.UnitPrice, normalizedCurrency)));
        return new(OrderId.New(), customerId.Trim(), normalizedCurrency, items);
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled) throw new DomainException($"A {Status} order cannot be cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Cancellation reason is required.");
        Status = OrderStatus.Cancelled; CancellationReason = reason.Trim(); UpdatedOn = DateTimeOffset.UtcNow; Version++;
        Raise(new OrderCancelledDomainEvent(Guid.NewGuid(), UpdatedOn, Id, CancellationReason));
    }
}

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(OrderId id, CancellationToken cancellationToken);
}
