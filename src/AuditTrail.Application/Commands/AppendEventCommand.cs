using AuditTrail.Application.Models;
using MediatR;

namespace AuditTrail.Application.Commands;

/// <summary>
/// Command to append a new event to an entity's event stream.
/// </summary>
public class AppendEventCommand : IRequest<AppendEventResult>
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string OccurredBy { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
    public object Payload { get; set; } = new();
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Correlation ID for distributed tracing - groups related events across services.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Causation ID - references the event that directly caused this event.
    /// </summary>
    public string? CausationId { get; set; }
}
