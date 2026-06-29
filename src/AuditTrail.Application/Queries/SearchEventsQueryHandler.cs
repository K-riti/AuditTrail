using System.Text.Json;
using AuditTrail.Application.Interfaces;
using AuditTrail.Application.Models;
using MediatR;

namespace AuditTrail.Application.Queries;

/// <summary>
/// Handler for SearchEventsQuery. Searches events with multiple filter criteria.
/// </summary>
public class SearchEventsQueryHandler : IRequestHandler<SearchEventsQuery, PagedResult<AuditEventReadModel>>
{
    private readonly IAuditEventRepository _repository;

    public SearchEventsQueryHandler(IAuditEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PagedResult<AuditEventReadModel>> Handle(
        SearchEventsQuery request, 
        CancellationToken cancellationToken)
    {
        var (events, totalCount) = await _repository.SearchAsync(
            request.EntityType,
            request.EventType,
            request.OccurredBy,
            request.EntityId,
            request.CorrelationId,
            request.From,
            request.To,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = events.Select(e => new AuditEventReadModel
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
        }).ToList();

        return PagedResult<AuditEventReadModel>.Create(
            items, 
            request.PageNumber, 
            request.PageSize, 
            totalCount);
    }
}
