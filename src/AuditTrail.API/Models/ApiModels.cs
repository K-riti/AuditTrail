using System.Text.Json;

namespace AuditTrail.API.Models;

/// <summary>
/// Request model for appending a new event.
/// </summary>
public class AppendEventRequest
{
    public string EntityType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string OccurredBy { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public JsonElement Payload { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
}

/// <summary>
/// Response model for successful event append.
/// </summary>
public class AppendEventResponse
{
    public Guid EventId { get; set; }
    public int NewVersion { get; set; }
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public int? ExpectedVersion { get; set; }
    public int? CurrentVersion { get; set; }
}

/// <summary>
/// Query parameters for event search.
/// </summary>
public class SearchEventsRequest
{
    public string? EntityType { get; set; }
    public string? EventType { get; set; }
    public string? OccurredBy { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
