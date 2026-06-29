using System.Text.Json;

namespace AuditTrail.Application.Models;

/// <summary>
/// Read model for entity state after event replay.
/// </summary>
public class EntityStateReadModel
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public Dictionary<string, JsonElement> State { get; set; } = new();
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

/// <summary>
/// Read model for a single audit event in history.
/// </summary>
public class AuditEventReadModel
{
    public Guid Id { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string OccurredBy { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public JsonElement Payload { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
}

/// <summary>
/// Read model for entity history containing all events (with pagination support).
/// </summary>
public class EntityHistoryReadModel
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int CurrentVersion { get; set; }
    public List<AuditEventReadModel> Events { get; set; } = new();

    // Pagination info
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public int? TotalPages { get; set; }
    public bool? HasNextPage { get; set; }
    public bool? HasPreviousPage { get; set; }
}

/// <summary>
/// Result model for append event operation.
/// </summary>
public class AppendEventResult
{
    public Guid EventId { get; set; }
    public int NewVersion { get; set; }
    public string? CorrelationId { get; set; }
}
