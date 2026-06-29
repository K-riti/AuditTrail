using AuditTrail.Application.Interfaces;
using AuditTrail.Application.Models;
using AuditTrail.Domain.Aggregates;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Handler for GetEntityStateQuery. Loads all events and replays them to reconstruct state.
/// </summary>
public class GetEntityStateQueryHandler : IRequestHandler<GetEntityStateQuery, EntityStateReadModel?>
{
    private readonly IAuditEventRepository _repository;

    public GetEntityStateQueryHandler(IAuditEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<EntityStateReadModel?> Handle(GetEntityStateQuery request, CancellationToken cancellationToken)
    {
        var events = await _repository.GetByEntityIdAsync(request.EntityId, cancellationToken);

        if (!events.Any())
        {
            return null;
        }

        var aggregate = EntityAggregate.ReplayEvents(events, request.AsOf);

        return new EntityStateReadModel
        {
            EntityId = aggregate.EntityId,
            EntityType = aggregate.EntityType,
            CurrentVersion = aggregate.CurrentVersion,
            State = aggregate.State,
            LastModifiedAt = aggregate.LastModifiedAt,
            LastModifiedBy = aggregate.LastModifiedBy
        };
    }
}
