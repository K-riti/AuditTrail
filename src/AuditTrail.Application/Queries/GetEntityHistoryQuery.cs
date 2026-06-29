using AuditTrail.Application.Models;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Query to get the full event history for an entity.
/// Optionally filter by date range and paginate results.
/// </summary>
public class GetEntityHistoryQuery : IRequest<EntityHistoryReadModel?>
{
    public string EntityId { get; set; } = string.Empty;
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
}
