namespace BuildingBlocks.Contracts;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public int Version { get; init; } = 1;
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
    public required Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
}
