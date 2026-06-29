using AuditTrail.Application.Models;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Query to get the current state of an entity by replaying its events.
/// Optionally specify a point in time to get historical state.
/// </summary>
public class GetEntityStateQuery : IRequest<EntityStateReadModel?>
{
    public string EntityId { get; set; } = string.Empty;
    public DateTimeOffset? AsOf { get; set; }
}
