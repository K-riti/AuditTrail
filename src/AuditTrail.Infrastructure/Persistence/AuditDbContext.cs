using AuditTrail.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace AuditTrail.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the append-only event store.
/// Configured to prevent updates and deletes on audit events.
/// </summary>
public class AuditDbContext : DbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.EntityId)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.EntityType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Version)
                .IsRequired();

            entity.Property(e => e.OccurredAt)
                .IsRequired();

            entity.Property(e => e.OccurredBy)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.SchemaVersion)
                .IsRequired()
                .HasDefaultValue(1);

            entity.Property(e => e.Payload)
                .IsRequired();

            // Correlation and Causation IDs for distributed tracing
            entity.Property(e => e.CorrelationId)
                .HasMaxLength(100);

            entity.Property(e => e.CausationId)
                .HasMaxLength(100);

            // Composite unique index - enforces optimistic concurrency at DB level
            entity.HasIndex(e => new { e.EntityId, e.Version })
                .IsUnique();

            // Index for querying by entity
            entity.HasIndex(e => e.EntityId);

            // Index for time-based queries
            entity.HasIndex(e => e.OccurredAt);

            // Index for entity type queries
            entity.HasIndex(e => e.EntityType);

            // Index for correlation ID queries
            entity.HasIndex(e => e.CorrelationId);

            // Index for actor queries
            entity.HasIndex(e => e.OccurredBy);
        });
    }

    public override int SaveChanges()
    {
        ValidateNoUpdatesOrDeletes();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateNoUpdatesOrDeletes();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateNoUpdatesOrDeletes()
    {
        var invalidEntries = ChangeTracker.Entries<AuditEvent>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException(
                "Audit events are immutable. UPDATE and DELETE operations are not allowed.");
        }
    }
}
