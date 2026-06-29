namespace AuditTrail.Domain.Events;

/// <summary>
/// Base class for domain events with common properties.
/// </summary>
public abstract class DomainEventBase
{
    public Guid EventId { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; protected set; } = DateTimeOffset.UtcNow;
    public string OccurredBy { get; protected set; } = string.Empty;

    protected DomainEventBase() { }

    protected DomainEventBase(string occurredBy)
    {
        OccurredBy = occurredBy ?? throw new ArgumentNullException(nameof(occurredBy));
    }
}
