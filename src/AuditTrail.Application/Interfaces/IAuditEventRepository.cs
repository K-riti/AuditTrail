using AuditTrail.Domain.Events;

namespace AuditTrail.Application.Interfaces;

/// <summary>
/// Repository interface for audit event operations.
/// </summary>
public interface IAuditEventRepository
{
    Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEvent>> GetByEntityIdAsync(
        string entityId, 
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEvent>> GetByEntityIdAsync(
        string entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentVersionAsync(string entityId, CancellationToken cancellationToken = default);

    Task<AuditEvent> AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string entityId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AuditEvent> Events, int TotalCount)> SearchAsync(
        string? entityType,
        string? eventType,
        string? occurredBy,
        string? entityId,
        string? correlationId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AuditEvent> Events, int TotalCount)> GetByEntityIdPagedAsync(
        string entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
