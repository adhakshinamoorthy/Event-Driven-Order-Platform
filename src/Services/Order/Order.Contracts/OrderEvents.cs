using BuildingBlocks.Contracts;

namespace Order.Contracts;

public sealed record OrderLineV1(string ProductId, int Quantity);
public sealed record OrderCreatedV1 : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required decimal Total { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyCollection<OrderLineV1> Items { get; init; }
}

public sealed record OrderCancelledV1 : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required string Reason { get; init; }
}
