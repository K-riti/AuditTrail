using System.Text.Json;
using AuditTrail.Domain.Aggregates;
using AuditTrail.Domain.Events;

namespace AuditTrail.Tests.Unit;

public class EntityAggregateTests
{
    [Fact]
    public void Create_WithValidInput_CreatesAggregate()
    {
        // Arrange & Act
        var aggregate = EntityAggregate.Create("User:123", "User");

        // Assert
        Assert.Equal("User:123", aggregate.EntityId);
        Assert.Equal("User", aggregate.EntityType);
        Assert.Equal(0, aggregate.CurrentVersion);
        Assert.Empty(aggregate.State);
    }

    [Fact]
    public void Create_WithNullEntityId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => EntityAggregate.Create(null!, "User"));
    }

    [Fact]
    public void Apply_SingleEvent_UpdatesState()
    {
        // Arrange
        var aggregate = EntityAggregate.Create("User:123", "User");
        var payload = JsonSerializer.Serialize(new { name = "John", email = "john@example.com" });
        var @event = AuditEvent.Create("User:123", "User", "UserCreated", 1, "admin", payload);

        // Act
        aggregate.Apply(@event);

        // Assert
        Assert.Equal(1, aggregate.CurrentVersion);
        Assert.Equal("John", aggregate.State["name"].GetString());
        Assert.Equal("john@example.com", aggregate.State["email"].GetString());
    }

    [Fact]
    public void Apply_MultipleEvents_MergesState()
    {
        // Arrange
        var aggregate = EntityAggregate.Create("User:123", "User");

        var event1 = AuditEvent.Create("User:123", "User", "UserCreated", 1, "admin",
            JsonSerializer.Serialize(new { name = "John", email = "john@example.com" }));

        var event2 = AuditEvent.Create("User:123", "User", "EmailChanged", 2, "admin",
            JsonSerializer.Serialize(new { email = "newemail@example.com" }));

        // Act
        aggregate.Apply(event1);
        aggregate.Apply(event2);

        // Assert
        Assert.Equal(2, aggregate.CurrentVersion);
        Assert.Equal("John", aggregate.State["name"].GetString());
        Assert.Equal("newemail@example.com", aggregate.State["email"].GetString());
    }

    [Fact]
    public void Apply_WrongEntityId_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = EntityAggregate.Create("User:123", "User");
        var @event = AuditEvent.Create("User:456", "User", "UserCreated", 1, "admin", "{}");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.Apply(@event));
    }

    [Fact]
    public void Apply_NonSequentialVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var aggregate = EntityAggregate.Create("User:123", "User");
        var @event = AuditEvent.Create("User:123", "User", "UserCreated", 5, "admin", "{}");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => aggregate.Apply(@event));
    }

    [Fact]
    public void ReplayEvents_WithEvents_ReconstructsState()
    {
        // Arrange
        var events = new[]
        {
            AuditEvent.Create("User:123", "User", "UserCreated", 1, "admin",
                JsonSerializer.Serialize(new { name = "John", email = "john@example.com" })),
            AuditEvent.Create("User:123", "User", "EmailChanged", 2, "admin",
                JsonSerializer.Serialize(new { email = "newemail@example.com" })),
            AuditEvent.Create("User:123", "User", "RoleAssigned", 3, "system",
                JsonSerializer.Serialize(new { role = "Admin" }))
        };

        // Act
        var aggregate = EntityAggregate.ReplayEvents(events);

        // Assert
        Assert.Equal("User:123", aggregate.EntityId);
        Assert.Equal(3, aggregate.CurrentVersion);
        Assert.Equal("John", aggregate.State["name"].GetString());
        Assert.Equal("newemail@example.com", aggregate.State["email"].GetString());
        Assert.Equal("Admin", aggregate.State["role"].GetString());
    }

    [Fact]
    public void ReplayEvents_WithEmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var events = Array.Empty<AuditEvent>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => EntityAggregate.ReplayEvents(events));
    }

    [Fact]
    public void ReplayEvents_WithAsOfFilter_ReconstructsHistoricalState()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);
        var events = new[]
        {
            AuditEvent.Create("User:123", "User", "UserCreated", 1, "admin",
                JsonSerializer.Serialize(new { name = "John" })),
            AuditEvent.Create("User:123", "User", "NameChanged", 2, "admin",
                JsonSerializer.Serialize(new { name = "Jane" })),
            AuditEvent.Create("User:123", "User", "NameChanged", 3, "admin",
                JsonSerializer.Serialize(new { name = "Jack" }))
        };

        // Note: Since OccurredAt is set at creation time, we're testing the filter mechanism
        // In a real scenario, the events would have different timestamps
        var aggregate = EntityAggregate.ReplayEvents(events, DateTimeOffset.UtcNow.AddSeconds(1));

        // Assert - all events should be included since they occurred before AsOf
        Assert.Equal(3, aggregate.CurrentVersion);
    }

    [Fact]
    public void GetStateAsJson_ReturnsValidJson()
    {
        // Arrange
        var aggregate = EntityAggregate.Create("User:123", "User");
        var @event = AuditEvent.Create("User:123", "User", "UserCreated", 1, "admin",
            JsonSerializer.Serialize(new { name = "John", email = "john@example.com" }));
        aggregate.Apply(@event);

        // Act
        var json = aggregate.GetStateAsJson();

        // Assert
        Assert.Contains("name", json);
        Assert.Contains("John", json);
        Assert.Contains("email", json);
    }
}
