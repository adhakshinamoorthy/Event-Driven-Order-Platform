namespace BuildingBlocks.SharedKernel;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public long Version { get; protected set; }
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class Entity<TId>(TId id) where TId : notnull
{
    public TId Id { get; protected init; } = id;
}

public abstract record ValueObject;

public class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");
