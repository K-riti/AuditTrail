using System.Text.Json;
using AuditTrail.Application.Commands;
using AuditTrail.Application.Interfaces;
using AuditTrail.Domain.Events;
using AuditTrail.Domain.Exceptions;
using Moq;

namespace AuditTrail.Tests.Unit;

public class AppendEventCommandHandlerTests
{
    private readonly Mock<IAuditEventRepository> _repositoryMock;
    private readonly AppendEventCommandHandler _handler;

    public AppendEventCommandHandlerTests()
    {
        _repositoryMock = new Mock<IAuditEventRepository>();
        _handler = new AppendEventCommandHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithMatchingVersion_AppendsEvent()
    {
        // Arrange
        var command = new AppendEventCommand
        {
            EntityId = "User:123",
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin@company.com",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\"}"),
            SchemaVersion = 1
        };

        _repositoryMock
            .Setup(r => r.GetCurrentVersionAsync("User:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        AuditEvent? savedEvent = null;
        _repositoryMock
            .Setup(r => r.AppendAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((e, _) => savedEvent = e)
            .ReturnsAsync((AuditEvent e, CancellationToken _) => e);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.Equal(1, result.NewVersion);

        _repositoryMock.Verify(r => r.AppendAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(savedEvent);
        Assert.Equal("User:123", savedEvent.EntityId);
        Assert.Equal("User", savedEvent.EntityType);
        Assert.Equal("UserCreated", savedEvent.EventType);
        Assert.Equal(1, savedEvent.Version);
        Assert.Equal("admin@company.com", savedEvent.OccurredBy);
    }

    [Fact]
    public async Task Handle_WithVersionMismatch_ThrowsConcurrencyConflict()
    {
        // Arrange
        var command = new AppendEventCommand
        {
            EntityId = "User:123",
            EntityType = "User",
            EventType = "EmailChanged",
            OccurredBy = "admin@company.com",
            ExpectedVersion = 2,  // Expecting version 2
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"email\":\"new@email.com\"}"),
            SchemaVersion = 1
        };

        _repositoryMock
            .Setup(r => r.GetCurrentVersionAsync("User:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);  // But current version is 5

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("User:123", exception.EntityId);
        Assert.Equal(2, exception.ExpectedVersion);
        Assert.Equal(5, exception.CurrentVersion);

        _repositoryMock.Verify(r => r.AppendAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingEntity_IncrementsVersion()
    {
        // Arrange
        var command = new AppendEventCommand
        {
            EntityId = "User:123",
            EntityType = "User",
            EventType = "RoleAssigned",
            OccurredBy = "system",
            ExpectedVersion = 3,  // Current version is 3
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"role\":\"Admin\"}"),
            SchemaVersion = 1
        };

        _repositoryMock
            .Setup(r => r.GetCurrentVersionAsync("User:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _repositoryMock
            .Setup(r => r.AppendAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditEvent e, CancellationToken _) => e);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.NewVersion);  // Should be 4 (3 + 1)
    }

    [Fact]
    public async Task Handle_SerializesPayloadCorrectly()
    {
        // Arrange
        var payload = new { name = "John", email = "john@example.com", age = 30 };
        var command = new AppendEventCommand
        {
            EntityId = "User:123",
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload)),
            SchemaVersion = 1
        };

        _repositoryMock
            .Setup(r => r.GetCurrentVersionAsync("User:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        AuditEvent? savedEvent = null;
        _repositoryMock
            .Setup(r => r.AppendAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((e, _) => savedEvent = e)
            .ReturnsAsync((AuditEvent e, CancellationToken _) => e);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedEvent);
        Assert.Contains("name", savedEvent.Payload);
        Assert.Contains("John", savedEvent.Payload);
        Assert.Contains("email", savedEvent.Payload);
    }
}
