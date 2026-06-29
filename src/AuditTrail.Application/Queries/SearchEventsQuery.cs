using AuditTrail.Application.Models;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Query to search events with multiple filter criteria.
/// </summary>
public class SearchEventsQuery : IRequest<PagedResult<AuditEventReadModel>>
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
