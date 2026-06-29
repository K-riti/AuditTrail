using AuditTrail.Application.Interfaces;
using AuditTrail.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace AuditTrail.Infrastructure.Persistence;

/// <summary>
/// Repository for accessing audit events from the event store.
/// All operations are append-only - no updates or deletes.
/// </summary>
public class AuditEventRepository : IAuditEventRepository
{
    private readonly AuditDbContext _context;

    public AuditEventRepository(AuditDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByEntityIdAsync(
        string entityId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditEvents
            .AsNoTracking()
            .Where(e => e.EntityId == entityId)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByEntityIdAsync(
        string entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditEvents
            .AsNoTracking()
            .Where(e => e.EntityId == entityId);

        if (from.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= to.Value);
        }

        return await query
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCurrentVersionAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var maxVersion = await _context.AuditEvents
            .AsNoTracking()
            .Where(e => e.EntityId == entityId)
            .MaxAsync(e => (int?)e.Version, cancellationToken);

        return maxVersion ?? 0;
    }

    public async Task<AuditEvent> AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await _context.AuditEvents.AddAsync(auditEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return auditEvent;
    }

    public async Task<bool> ExistsAsync(string entityId, CancellationToken cancellationToken = default)
    {
        return await _context.AuditEvents
            .AsNoTracking()
            .AnyAsync(e => e.EntityId == entityId, cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditEvent> Events, int TotalCount)> SearchAsync(
        string? entityType,
        string? eventType,
        string? occurredBy,
        string? entityId,
        string? correlationId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(e => e.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);

        if (!string.IsNullOrWhiteSpace(occurredBy))
            query = query.Where(e => e.OccurredBy == occurredBy);

        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(e => e.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(e => e.CorrelationId == correlationId);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenBy(e => e.EntityId)
            .ThenBy(e => e.Version)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (events, totalCount);
    }

    public async Task<(IReadOnlyList<AuditEvent> Events, int TotalCount)> GetByEntityIdPagedAsync(
        string entityId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditEvents
            .AsNoTracking()
            .Where(e => e.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderBy(e => e.Version)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (events, totalCount);
    }
}
