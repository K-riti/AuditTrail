using System.Text.Json;
using AuditTrail.Application.Interfaces;
using AuditTrail.Application.Models;
using AuditTrail.Domain.Events;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Handler for GetEntityHistoryQuery. Returns all events for an entity with optional pagination.
/// </summary>
public class GetEntityHistoryQueryHandler : IRequestHandler<GetEntityHistoryQuery, EntityHistoryReadModel?>
{
    private readonly IAuditEventRepository _repository;

    public GetEntityHistoryQueryHandler(IAuditEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<EntityHistoryReadModel?> Handle(GetEntityHistoryQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditEvent> events;
        int totalCount;

        // Use pagination if page parameters are provided
        if (request.PageNumber.HasValue && request.PageSize.HasValue)
        {
            var result = await _repository.GetByEntityIdPagedAsync(
                request.EntityId,
                request.From,
                request.To,
                request.PageNumber.Value,
                request.PageSize.Value,
                cancellationToken);

            events = result.Events;
            totalCount = result.TotalCount;
        }
        else
        {
            events = await _repository.GetByEntityIdAsync(
                request.EntityId,
                request.From,
                request.To,
                cancellationToken);
            totalCount = events.Count;
        }

        if (!events.Any() && totalCount == 0)
        {
            return null;
        }

        var firstEvent = events.FirstOrDefault();
        var currentVersion = await _repository.GetCurrentVersionAsync(request.EntityId, cancellationToken);

        var result2 = new EntityHistoryReadModel
        {
            EntityId = request.EntityId,
            EntityType = firstEvent?.EntityType ?? string.Empty,
            TotalEvents = totalCount,
            CurrentVersion = currentVersion,
            Events = events.Select(e => new AuditEventReadModel
            {
                Id = e.Id,
                EntityId = e.EntityId,
                EntityType = e.EntityType,
                EventType = e.EventType,
                Version = e.Version,
                OccurredAt = e.OccurredAt,
                OccurredBy = e.OccurredBy,
                SchemaVersion = e.SchemaVersion,
                Payload = JsonDocument.Parse(e.Payload).RootElement.Clone(),
                CorrelationId = e.CorrelationId,
                CausationId = e.CausationId
            }).ToList()
        };

        // Add pagination info if paginated
        if (request.PageNumber.HasValue && request.PageSize.HasValue)
        {
            result2.PageNumber = request.PageNumber.Value;
            result2.PageSize = request.PageSize.Value;
            result2.TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize.Value);
            result2.HasNextPage = request.PageNumber.Value < result2.TotalPages;
            result2.HasPreviousPage = request.PageNumber.Value > 1;
        }

        return result2;
    }
}
