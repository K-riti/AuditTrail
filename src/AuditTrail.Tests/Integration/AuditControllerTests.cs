using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditTrail.API.Models;
using AuditTrail.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuditTrail.Tests.Integration;

public class AuditControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuditControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AppendEvent_NewEntity_ReturnsCreated()
    {
        // Arrange
        var request = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin@company.com",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\",\"email\":\"john@example.com\"}"),
            SchemaVersion = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/audit/User:test123/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AppendEventResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.Equal(1, result.NewVersion);
    }

    [Fact]
    public async Task AppendEvent_ConcurrencyConflict_ReturnsConflict()
    {
        // Arrange - First create an event
        var entityId = "User:conflict" + Guid.NewGuid().ToString("N");
        var firstRequest = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin@company.com",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\"}"),
            SchemaVersion = 1
        };
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", firstRequest);

        // Arrange - Try to append with wrong expected version
        var conflictRequest = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "EmailChanged",
            OccurredBy = "admin@company.com",
            ExpectedVersion = 0,  // Should be 1
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"email\":\"new@email.com\"}"),
            SchemaVersion = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", conflictRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetEntityState_ExistingEntity_ReturnsState()
    {
        // Arrange - Create an entity with events
        var entityId = "User:state" + Guid.NewGuid().ToString("N");

        var createRequest = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\",\"email\":\"john@example.com\"}"),
            SchemaVersion = 1
        };
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", createRequest);

        // Act
        var response = await _client.GetAsync($"/api/audit/{entityId}/state");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("John", content);
        Assert.Contains("john@example.com", content);
    }

    [Fact]
    public async Task GetEntityState_NonExistentEntity_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/audit/NonExistent:xyz/state");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEntityHistory_ExistingEntity_ReturnsHistory()
    {
        // Arrange - Create an entity with multiple events
        var entityId = "User:history" + Guid.NewGuid().ToString("N");

        var createRequest = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\"}"),
            SchemaVersion = 1
        };
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", createRequest);

        var updateRequest = new AppendEventRequest
        {
            EntityType = "User",
            EventType = "EmailChanged",
            OccurredBy = "admin",
            ExpectedVersion = 1,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"email\":\"john@new.com\"}"),
            SchemaVersion = 1
        };
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", updateRequest);

        // Act
        var response = await _client.GetAsync($"/api/audit/{entityId}/history");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("UserCreated", content);
        Assert.Contains("EmailChanged", content);
        Assert.Contains("\"totalEvents\":2", content);
    }

    [Fact]
    public async Task GetEntityHistory_NonExistentEntity_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/audit/NonExistent:abc/history");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MultipleEvents_ReplayCorrectly()
    {
        // Arrange
        var entityId = "User:replay" + Guid.NewGuid().ToString("N");

        // Create initial user
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", new AppendEventRequest
        {
            EntityType = "User",
            EventType = "UserCreated",
            OccurredBy = "admin",
            ExpectedVersion = 0,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"John\",\"email\":\"old@email.com\"}"),
            SchemaVersion = 1
        });

        // Change email
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", new AppendEventRequest
        {
            EntityType = "User",
            EventType = "EmailChanged",
            OccurredBy = "admin",
            ExpectedVersion = 1,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"email\":\"new@email.com\"}"),
            SchemaVersion = 1
        });

        // Assign role
        await _client.PostAsJsonAsync($"/api/audit/{entityId}/events", new AppendEventRequest
        {
            EntityType = "User",
            EventType = "RoleAssigned",
            OccurredBy = "system",
            ExpectedVersion = 2,
            Payload = JsonSerializer.Deserialize<JsonElement>("{\"role\":\"Admin\"}"),
            SchemaVersion = 1
        });

        // Act
        var response = await _client.GetAsync($"/api/audit/{entityId}/state");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("John", content);  // Original name preserved
        Assert.Contains("new@email.com", content);  // Email updated
        Assert.Contains("Admin", content);  // Role added
        Assert.Contains("\"currentVersion\":3", content);
    }
}

/// <summary>
/// Custom WebApplicationFactory that configures InMemory database and bypasses auth for testing.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "AuditTrailTestDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuditDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove the DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AuditDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add InMemory database for testing
            services.AddDbContext<AuditDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Configure test authentication that bypasses JWT
            services.AddAuthentication("Test")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                    "Test", options => { });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .Build();
            });
        });

        builder.UseEnvironment("Development");
    }
}

/// <summary>
/// Test authentication handler that always authenticates requests.
/// </summary>
public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "TestUser"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");

        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
