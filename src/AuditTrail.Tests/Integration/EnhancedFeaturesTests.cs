using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditTrail.API.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuditTrail.Tests.Integration;

public class EnhancedFeaturesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EnhancedFeaturesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_Live_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task HealthCheck_Ready_ReturnsStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert - May return 503 if DB not available in test, but should not throw
        Assert.True(response.StatusCode == HttpStatusCode.OK || 
                    response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SearchEvents_WithEntityTypeFilter_ReturnsFilteredResults()
    {
        // Arrange - Create events of different types
        var entityId1 = "User:search" + Guid.NewGuid().ToString("N");
        var entityId2 = "Order:search" + Guid.NewGuid().ToString("N");

        await _client.PostAsJsonAsync($"/api/audit/{entityId1}/events", new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"Test\"}"),
            SchemaVersion = 1
        });

        await _client.PostAsJsonAsync($"/api/audit/{entityId2}/events", new AppendEventRequest
        {
            EntityType = "Order",
            EventType = "OrderCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"total\":100}"),
            SchemaVersion = 1
        });

        // Act - Search for User events only
        var response = await _client.GetAsync("/api/audit/search?entityType=User");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User", content);
    }

    [Fact]
    public async Task SearchEvents_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange - Create multiple events
        var entityId = "User:pagination" + Guid.NewGuid().ToString("N");

        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", new AppendEventRequest
            {
                EntityType = "User",
                EventType = $"Event{i}",
                OccurredBy = "admin",
                ExpectedVersion = i,
                Payload = JsonSerializer.Deserialize<JsonElement>($"{{\"index\":{i}}}"),
                SchemaVersion = 1
            });
        }

        // Act - Get paginated results
        var response = await _client.GetAsync($"/api/audit/search?entityId={entityId}&pageNumber=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"pageNumber\":1", content);
        Assert.Contains("\"pageSize\":2", content);
    }

    [Fact]
    public async Task GetEntityHistory_WithPagination_ReturnsPaginatedHistory()
    {
        // Arrange
        var entityId = "User:historypage" + Guid.NewGuid().ToString("N");

        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", new AppendEventRequest
            {
                EntityType = "User",
                EventType = $"Event{i}",
                OccurredBy = "admin",
                ExpectedVersion = i,
                Payload = JsonSerializer.Deserialize<JsonElement>($"{{\"index\":{i}}}"),
                SchemaVersion = 1
            });
        }

        // Act
        var response = await _client.GetAsync($"/api/audit/{entityId}/history?pageNumber=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"pageNumber\":1", content);
        Assert.Contains("\"totalEvents\":5", content);
    }

    [Fact]
    public async Task AppendEvent_WithCorrelationId_IncludesCorrelationInResponse()
    {
        // Arrange
        var entityId = "User:correlation" + Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString("N");

        var request = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"Test\"}"),
            SchemaVersion = 1,
            CorrelationId = correlationId
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(correlationId, content);
    }

    [Fact]
    public async Task SearchEvents_ByCorrelationId_ReturnsRelatedEvents()
    {
        // Arrange - Create events with same correlation ID
        var correlationId = Guid.NewGuid().ToString("N");
        var entityId1 = "User:corr1" + Guid.NewGuid().ToString("N");
        var entityId2 = "Order:corr2" + Guid.NewGuid().ToString("N");

        await _client.PostAsJsonAsync($"/api/audit/{entityId1}/events", new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"Test\"}"),
            SchemaVersion = 1,
            CorrelationId = correlationId
        });

        await _client.PostAsJsonAsync($"/api/audit/{entityId2}/events", new AppendEventRequest
        {
            EntityType = "Order",
            EventType = "OrderCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"total\":100}"),
            SchemaVersion = 1,
            CorrelationId = correlationId,
            CausationId = entityId1
        });

        // Act - Search by correlation ID
        var response = await _client.GetAsync($"/api/audit/search?correlationId={correlationId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User", content);
        Assert.Contains("Order", content);
    }

    [Fact]
    public async Task GenerateToken_ReturnsValidToken()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", new
        {
            Username = "testuser",
            Role = "Admin"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", content);
        Assert.Contains("Bearer", content);
    }
}
