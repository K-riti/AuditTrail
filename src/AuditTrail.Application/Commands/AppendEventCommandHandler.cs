using System.Text.Json;
using AuditTrail.Application.Interfaces;
using AuditTrail.Application.Models;
using AuditTrail.Domain.Events;
using AuditTrail.Domain.Exceptions;
using MediatR;

namespace AuditTrail.Application.Commands;

/// <summary>
/// Handler for AppendEventCommand. Validates concurrency and appends event to store.
/// </summary>
public class AppendEventCommandHandler : IRequestHandler<AppendEventCommand, AppendEventResult>
{
    private readonly IAuditEventRepository _repository;

    public AppendEventCommandHandler(IAuditEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AppendEventResult> Handle(AppendEventCommand request, CancellationToken cancellationToken)
    {
        // Get current version for optimistic concurrency check
        var currentVersion = await _repository.GetCurrentVersionAsync(request.EntityId, cancellationToken);

        // Check for concurrency conflict
        if (request.ExpectedVersion != currentVersion)
        {
            throw new ConcurrencyConflictException(
                request.EntityId, 
                request.ExpectedVersion, 
                currentVersion);
        }

        // Serialize the payload
        var payloadJson = JsonSerializer.Serialize(request.Payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Create the new event with correlation/causation IDs
        var newVersion = currentVersion + 1;
        var auditEvent = AuditEvent.Create(
            entityId: request.EntityId,
            entityType: request.EntityType,
            eventType: request.EventType,
            version: newVersion,
            occurredBy: request.OccurredBy,
            payload: payloadJson,
            schemaVersion: request.SchemaVersion,
            correlationId: request.CorrelationId,
            causationId: request.CausationId);

        // Append to store
        var savedEvent = await _repository.AppendAsync(auditEvent, cancellationToken);

        return new AppendEventResult
        {
            EventId = savedEvent.Id,
            NewVersion = savedEvent.Version,
            CorrelationId = savedEvent.CorrelationId
        };
    }
}
