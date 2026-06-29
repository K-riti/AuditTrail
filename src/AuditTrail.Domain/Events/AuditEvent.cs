namespace AuditTrail.Domain.Events;

/// <summary>
/// Represents an immutable audit event stored in the event store.
/// Each event captures a single state change for an entity.
/// </summary>
public class AuditEvent
{
    public Guid Id { get; private set; }
    public string EntityId { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string OccurredBy { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing - groups related events across services.
    /// </summary>
    public string? CorrelationId { get; private set; }

    /// <summary>
    /// Causation ID - references the event that directly caused this event.
    /// </summary>
    public string? CausationId { get; private set; }

    private AuditEvent() { } // EF Core constructor

    public static AuditEvent Create(
        string entityId,
        string entityType,
        string eventType,
        int version,
        string occurredBy,
        string payload,
        int schemaVersion = 1,
        string? correlationId = null,
        string? causationId = null)
    {
        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId)),
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType)),
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType)),
            Version = version,
            OccurredAt = DateTimeOffset.UtcNow,
            OccurredBy = occurredBy ?? throw new ArgumentNullException(nameof(occurredBy)),
            Payload = payload ?? throw new ArgumentNullException(nameof(payload)),
            SchemaVersion = schemaVersion,
            CorrelationId = correlationId,
            CausationId = causationId
        };
    }
}
