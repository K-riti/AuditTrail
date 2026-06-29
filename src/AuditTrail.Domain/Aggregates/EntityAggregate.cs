using System.Text.Json;
using AuditTrail.Domain.Events;

namespace AuditTrail.Domain.Aggregates;

/// <summary>
/// Represents an entity's state reconstructed from its event history.
/// Provides event replay functionality for event sourcing.
/// </summary>
public class EntityAggregate
{
    public string EntityId { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public int CurrentVersion { get; private set; }
    public Dictionary<string, JsonElement> State { get; private set; } = new();
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private EntityAggregate() { }

    public static EntityAggregate Create(string entityId, string entityType)
    {
        return new EntityAggregate
        {
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId)),
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType)),
            CurrentVersion = 0,
            State = new Dictionary<string, JsonElement>()
        };
    }

    /// <summary>
    /// Replays a list of events to reconstruct the entity's state.
    /// Events are applied in order, with later events overwriting earlier values.
    /// </summary>
    public static EntityAggregate ReplayEvents(IEnumerable<AuditEvent> events, DateTimeOffset? asOf = null)
    {
        var eventList = events.ToList();
        if (!eventList.Any())
        {
            throw new InvalidOperationException("Cannot replay empty event list");
        }

        var filteredEvents = asOf.HasValue
            ? eventList.Where(e => e.OccurredAt <= asOf.Value).ToList()
            : eventList;

        if (!filteredEvents.Any())
        {
            throw new InvalidOperationException("No events found before the specified time");
        }

        var firstEvent = filteredEvents.First();
        var aggregate = Create(firstEvent.EntityId, firstEvent.EntityType);

        foreach (var @event in filteredEvents.OrderBy(e => e.Version))
        {
            aggregate.Apply(@event);
        }

        return aggregate;
    }

    /// <summary>
    /// Applies a single event to update the aggregate's state.
    /// The event payload is merged into the current state.
    /// </summary>
    public void Apply(AuditEvent @event)
    {
        if (@event.EntityId != EntityId)
        {
            throw new InvalidOperationException($"Event entity ID '{@event.EntityId}' does not match aggregate entity ID '{EntityId}'");
        }

        if (@event.Version != CurrentVersion + 1)
        {
            throw new InvalidOperationException($"Event version {@event.Version} is not sequential. Expected {CurrentVersion + 1}");
        }

        // Parse the payload and merge into state
        try
        {
            var payloadDoc = JsonDocument.Parse(@event.Payload);
            foreach (var property in payloadDoc.RootElement.EnumerateObject())
            {
                State[property.Name] = property.Value.Clone();
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse event payload: {ex.Message}", ex);
        }

        CurrentVersion = @event.Version;
        LastModifiedAt = @event.OccurredAt;
        LastModifiedBy = @event.OccurredBy;
    }

    /// <summary>
    /// Gets the current state as a JSON string.
    /// </summary>
    public string GetStateAsJson()
    {
        return JsonSerializer.Serialize(State);
    }
}
