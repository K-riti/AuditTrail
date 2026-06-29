namespace AuditTrail.Domain.Exceptions;

/// <summary>
/// Exception thrown when optimistic concurrency conflict is detected.
/// This occurs when the expected version doesn't match the current version in the store.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public string EntityId { get; }
    public int ExpectedVersion { get; }
    public int CurrentVersion { get; }

    public ConcurrencyConflictException(string entityId, int expectedVersion, int currentVersion)
        : base($"Concurrency conflict for entity '{entityId}'. Expected version {expectedVersion} but current is {currentVersion}. Retry with latest version.")
    {
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        CurrentVersion = currentVersion;
    }
}
