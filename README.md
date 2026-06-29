# AuditTrail

> A tamper-evident, event-sourced audit logging microservice built with ASP.NET Core 8, CQRS via MediatR, and an append-only EF Core store. Every state change is stored as an immutable event — entities can be replayed to any point in time.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4)](https://learn.microsoft.com/en-us/aspnet/core/)
[![MediatR](https://img.shields.io/badge/MediatR-CQRS-512BD4)](https://github.com/jbogard/MediatR)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## Overview

AuditTrail implements **event sourcing** — instead of storing the current state of an entity, it stores every change as an ordered, immutable event. The current state is always a projection of those events replayed in sequence. This makes the audit log inherently tamper-evident: you cannot update or delete history, only append to it.

The API is built with **CQRS (Command Query Responsibility Segregation)** via MediatR: commands append events, queries project state. All writes go through a pipeline with validation and optimistic concurrency, all reads hit a separate read model.

### Key Features

- **Append-only event store** — no UPDATE or DELETE on event records, ever
- **Full event replay** — reconstruct any entity's state at any timestamp
- **CQRS via MediatR** — clean separation of write and read paths
- **Optimistic concurrency** — version-based conflict detection on writes
- **Event versioning** — schema evolution without breaking old events
- **JWT Bearer authentication** — secure API access with role-based authorization
- **Health checks** — Kubernetes-ready liveness and readiness probes
- **Global exception handling** — RFC 7807 ProblemDetails responses
- **Correlation ID tracking** — distributed tracing support via `X-Correlation-ID` header
- **Correlation & Causation IDs** — full event lineage tracking across services
- **Pagination & Search** — filter events by type, actor, date range, correlation ID
- **REST API with full OpenAPI (Swagger) documentation** — including JWT auth support
- **xUnit test suite** — 29 tests covering unit and integration scenarios

---

## Architecture

### CQRS + Event Sourcing Overview

```
                        CLIENT
                           │
                    REST API request
                           │
              ┌────────────▼────────────┐
              │    ASP.NET Core 8       │
              │    Web API layer        │
              └────────────┬────────────┘
                           │ IMediator.Send()
              ┌────────────▼────────────────────────────────────┐
              │              MediatR Pipeline                    │
              │                                                  │
              │  ┌─────────────────┐    ┌──────────────────────┐│
              │  │  Validation     │    │  Logging behaviour   ││
              │  │  behaviour      │    │  (structured trace)  ││
              │  │  (FluentValid.) │    └──────────────────────┘│
              │  └─────────────────┘                            │
              └────────────┬────────────────────────────────────┘
                           │
           ┌───────────────┴─────────────────┐
           │                                 │
    COMMAND PATH                        QUERY PATH
    (write side)                        (read side)
           │                                 │
  ┌────────▼──────────┐          ┌───────────▼──────────┐
  │  Command Handler  │          │   Query Handler       │
  │                   │          │                       │
  │ 1. Load aggregate │          │ 1. Load all events    │
  │    events from    │          │    for entity         │
  │    event store    │          │ 2. Replay events →    │
  │ 2. Apply domain   │          │    projected state    │
  │    logic          │          │ 3. Return read model  │
  │ 3. Raise new      │          └───────────────────────┘
  │    DomainEvent    │
  │ 4. Check version  │
  │    (optimistic    │
  │    concurrency)   │
  │ 5. Append to      │
  │    event store    │
  └────────┬──────────┘
           │
  ┌────────▼──────────────────────────────────┐
  │           EVENT STORE (EF Core)           │
  │                                           │
  │  AuditEvents table (append-only)          │
  │  ┌──────┬──────────┬──────────┬─────────┐ │
  │  │ Id   │ EntityId │ Version  │ Payload │ │
  │  │ (Guid│ (string) │ (int,    │ (JSON,  │ │
  │  │ PK)  │          │ seq per  │ typed   │ │
  │  │      │          │ entity)  │ event)  │ │
  │  └──────┴──────────┴──────────┴─────────┘ │
  │                                           │
  │  EF Core migration: no Update/Delete      │
  │  SQL constraint: Version is unique per    │
  │  EntityId (prevents concurrent writes)    │
  └───────────────────────────────────────────┘
```

### Event Sourcing — State Reconstruction

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as REST API
    participant QH as QueryHandler
    participant ES as Event Store (DB)

    C->>API: GET /entities/{id}/state?at=2025-06-01T10:00:00Z
    API->>QH: GetEntityStateQuery {EntityId, AsOf}
    QH->>ES: SELECT * FROM AuditEvents WHERE EntityId=? ORDER BY Version ASC
    ES->>QH: [Event v1, Event v2, Event v3, Event v4]
    QH->>QH: Filter events where OccurredAt <= AsOf
    QH->>QH: Replay: initialState.Apply(v1).Apply(v2).Apply(v3)
    QH->>API: EntityStateReadModel {current projected state}
    API->>C: 200 OK {state at that point in time}
```

### Write Path — Optimistic Concurrency

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as REST API
    participant CH as CommandHandler
    participant ES as Event Store

    C->>API: POST /entities/{id}/events\nBody: {eventType, payload, expectedVersion: 3}
    API->>CH: AppendEventCommand {EntityId, Event, ExpectedVersion=3}
    CH->>ES: SELECT MAX(Version) WHERE EntityId=?
    ES->>CH: CurrentVersion = 3

    alt versions match — no conflict
        CH->>ES: INSERT AuditEvent {Version=4, Payload=...}
        ES->>CH: Success
        CH->>API: AppendEventResult {NewVersion: 4}
        API->>C: 201 Created
    else conflict — another writer got there first
        CH->>API: ConcurrencyConflictException
        API->>C: 409 Conflict {message: "Expected version 3 but current is 4. Retry with latest."}
    end
```

### Event Schema & Versioning

```
AuditEvent (stored in DB)
├── Id              Guid          Primary key
├── EntityId        string        "User:abc123", "Order:xyz789"
├── EntityType      string        "User", "Order"
├── EventType       string        "UserCreated", "EmailChanged"
├── Version         int           Monotonically increasing per EntityId
├── OccurredAt      DateTimeOffset UTC timestamp
├── OccurredBy      string        Actor who caused the event
├── SchemaVersion   int           For event schema evolution
├── CorrelationId   string?       Groups related events across services
├── CausationId     string?       References the causing event
└── Payload         string        JSON-serialised event body

Example events for User:abc123:
  v1  UserCreated      {name:"Kriti", email:"old@email.com"}  2025-01-01
  v2  EmailChanged     {from:"old@email.com", to:"new@email.com"}  2025-03-15
  v3  RoleAssigned     {role:"Admin", assignedBy:"system"}  2025-04-01

Replay v1→v3 gives current state:
  {name:"Kriti", email:"new@email.com", role:"Admin"}
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8, ASP.NET Core 8 |
| CQRS | MediatR — IRequest, IRequestHandler, IPipelineBehavior |
| Validation | FluentValidation via MediatR pipeline behaviour |
| Persistence | EF Core 8, SQL Server (append-only, no deletes) |
| Authentication | JWT Bearer tokens with ASP.NET Core Identity |
| Serialisation | System.Text.Json, polymorphic event deserialisation |
| API Docs | Swashbuckle (OpenAPI / Swagger UI) with JWT support |
| Health Checks | Microsoft.Extensions.Diagnostics.HealthChecks + SQL Server |
| Testing | xUnit, Moq, EF Core InMemory, WebApplicationFactory |
| Concurrency | Optimistic locking via Version column + unique constraint |
| Observability | Correlation IDs, structured logging, ProblemDetails |

---

## Project Structure

```
AuditTrail/
├── src/
│   ├── AuditTrail.API/                  # ASP.NET Core entry point
│   │   ├── Controllers/
│   │   │   ├── AuditController.cs       # Main audit endpoints
│   │   │   ├── AuthController.cs        # JWT token generation
│   │   │   └── HealthController.cs      # Health check endpoints
│   │   ├── Middleware/
│   │   │   ├── GlobalExceptionHandlerMiddleware.cs
│   │   │   └── CorrelationIdMiddleware.cs
│   │   ├── Models/
│   │   │   └── ApiModels.cs             # Request/response DTOs
│   │   └── Program.cs
│   ├── AuditTrail.Application/          # CQRS — commands, queries, handlers
│   │   ├── Commands/
│   │   │   ├── AppendEventCommand.cs
│   │   │   └── AppendEventCommandHandler.cs
│   │   ├── Queries/
│   │   │   ├── GetEntityStateQuery.cs
│   │   │   ├── GetEntityStateQueryHandler.cs
│   │   │   ├── GetEntityHistoryQuery.cs
│   │   │   ├── GetEntityHistoryQueryHandler.cs
│   │   │   ├── SearchEventsQuery.cs
│   │   │   └── SearchEventsQueryHandler.cs
│   │   ├── Behaviours/
│   │   │   ├── ValidationBehaviour.cs
│   │   │   └── LoggingBehaviour.cs
│   │   └── Models/
│   │       ├── ReadModels.cs
│   │       └── PagedResult.cs
│   ├── AuditTrail.Domain/               # Pure domain — no infra deps
│   │   ├── Events/
│   │   │   └── AuditEvent.cs            # Immutable event with correlation/causation
│   │   ├── Aggregates/
│   │   │   └── EntityAggregate.cs       # Apply() + replay logic
│   │   └── Exceptions/
│   │       └── ConcurrencyConflictException.cs
│   ├── AuditTrail.Infrastructure/       # EF Core, migrations
│   │   ├── Persistence/
│   │   │   ├── AuditDbContext.cs        # Append-only enforcement
│   │   │   ├── AuditEventRepository.cs  # Search, paging, CRUD
│   │   │   └── Migrations/
│   │   └── DependencyInjection.cs
│   └── AuditTrail.Tests/
│       ├── Unit/
│       │   ├── AuditEventTests.cs
│       │   ├── EntityAggregateTests.cs
│       │   └── AppendEventCommandHandlerTests.cs
│       └── Integration/
│           ├── AuditControllerTests.cs   # WebApplicationFactory
│           └── EnhancedFeaturesTests.cs  # Health, search, pagination, auth
├── docker-compose.yml                    # API + SQL Server
├── Dockerfile                            # Multi-stage build
└── README.md
```

---

## API Reference

### Authentication

All audit endpoints require JWT Bearer authentication. First, obtain a token:

```http
POST /api/auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

Response `200 OK`:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-06-29T13:00:00Z",
  "tokenType": "Bearer"
}
```

Use the token in subsequent requests:
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Append an event

```http
POST /api/audit/{entityId}/events
Authorization: Bearer {token}
Content-Type: application/json
X-Correlation-ID: optional-correlation-id

{
  "entityType": "User",
  "eventType": "EmailChanged",
  "occurredBy": "admin@company.com",
  "expectedVersion": 2,
  "payload": {
    "from": "old@email.com",
    "to": "new@email.com"
  },
  "correlationId": "req-123",
  "causationId": "event-456"
}
```

Response `201 Created`:
```json
{
  "eventId": "d3f1a2b4-...",
  "newVersion": 3,
  "correlationId": "req-123"
}
```

### Get current state

```http
GET /api/audit/{entityId}/state
GET /api/audit/{entityId}/state?at=2025-03-01T00:00:00Z
Authorization: Bearer {token}
```

Response `200 OK`:
```json
{
  "entityId": "User:abc123",
  "entityType": "User",
  "currentVersion": 3,
  "state": {
    "name": "Kriti",
    "email": "new@email.com",
    "role": "Admin"
  },
  "lastModifiedAt": "2025-04-01T10:00:00Z",
  "lastModifiedBy": "system"
}
```

### Get full history (with pagination)

```http
GET /api/audit/{entityId}/history
GET /api/audit/{entityId}/history?from=2025-01-01&to=2025-06-01&pageNumber=1&pageSize=20
Authorization: Bearer {token}
```

Response `200 OK`:
```json
{
  "entityId": "User:abc123",
  "entityType": "User",
  "totalEvents": 50,
  "currentVersion": 50,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "events": [
    {
      "id": "...",
      "eventType": "UserCreated",
      "version": 1,
      "occurredAt": "2025-01-01T00:00:00Z",
      "occurredBy": "admin@company.com",
      "correlationId": "req-001",
      "payload": { "name": "Kriti", "email": "old@email.com" }
    }
  ]
}
```

### Search events (cross-entity)

```http
GET /api/audit/search?entityType=User&eventType=EmailChanged&occurredBy=admin@company.com&from=2025-01-01&to=2025-06-01&correlationId=req-123&pageNumber=1&pageSize=20
Authorization: Bearer {token}
```

Response `200 OK`:
```json
{
  "items": [...],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Health Checks

```http
GET /health/live    # Liveness probe (always healthy if running)
GET /health/ready   # Readiness probe (checks SQL Server connectivity)
GET /health         # Detailed health status
```

Response `200 OK`:
```json
{
  "status": "Healthy",
  "timestamp": "2025-06-29T12:00:00Z",
  "checks": {
    "sqlserver": "Healthy"
  }
}
```

---

## Getting Started

### Option 1: Run with Docker (recommended)

```bash
git clone https://github.com/K-riti/AuditTrail.git
cd AuditTrail

# Start API + SQL Server
docker-compose up --build

# Swagger UI
open http://localhost:5000/swagger
```

### Option 2: Run locally (in-memory database)

```bash
git clone https://github.com/K-riti/AuditTrail.git
cd AuditTrail

# Build the solution
dotnet build

# Run with in-memory database (Development mode)
cd src/AuditTrail.API
dotnet run --urls http://localhost:5000

# Swagger UI
open http://localhost:5000/swagger
```

### Run tests

```bash
dotnet test
```

### Apply migrations (SQL Server)

```bash
dotnet ef database update --project src/AuditTrail.Infrastructure --startup-project src/AuditTrail.API
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AuditTrail;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyAtLeast32Characters!",
    "Issuer": "AuditTrail",
    "Audience": "AuditTrailAPI",
    "ExpirationMinutes": 60
  },
  "UseInMemoryDatabase": false
}
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | - |
| `Jwt__SecretKey` | JWT signing key (min 32 chars) | - |
| `Jwt__Issuer` | JWT issuer | AuditTrail |
| `Jwt__Audience` | JWT audience | AuditTrailAPI |
| `Jwt__ExpirationMinutes` | Token expiration time | 60 |
| `UseInMemoryDatabase` | Use in-memory DB for dev/testing | false |

---

## Cross-Cutting Concerns

### Correlation IDs

Every request can include an `X-Correlation-ID` header for distributed tracing. If not provided, one is generated automatically. The correlation ID is:
- Echoed back in response headers
- Included in log entries
- Stored with events (if provided in the request body)

### Global Exception Handling

All exceptions are converted to RFC 7807 ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Concurrency Conflict",
  "status": 409,
  "detail": "Expected version 2 but current version is 3. Retry with the latest version.",
  "traceId": "00-abc123...",
  "instance": "/api/audit/User:abc123/events"
}
```

### Event Lineage

Events support `correlationId` and `causationId` for tracking:
- **Correlation ID**: Groups related events across services (e.g., all events from one user request)
- **Causation ID**: Points to the event that caused this event (parent-child relationship)

---

## Why Append-Only?

The EF Core `DbContext` is configured to throw if any `UPDATE` or `DELETE` is attempted on `AuditEvents`:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<AuditEvent>(e =>
    {
        e.HasKey(x => x.Id);
        // Composite unique — enforces optimistic concurrency at DB level
        e.HasIndex(x => new { x.EntityId, x.Version }).IsUnique();
        // No update/delete — append only enforced at application layer
    });
}
```

---

## License

MIT — see [LICENSE](LICENSE) for details.

---

## Testing Summary

The project includes **29 tests** covering:

| Category | Tests |
|----------|-------|
| Unit Tests | AuditEvent creation, EntityAggregate replay, command handler logic |
| Integration Tests | Full API flow via WebApplicationFactory |
| Enhanced Features | Health checks, search, pagination, correlation IDs, JWT auth |

Run all tests:
```bash
dotnet test --verbosity normal
```
