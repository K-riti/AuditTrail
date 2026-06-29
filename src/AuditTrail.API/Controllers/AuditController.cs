using AuditTrail.API.Middleware;
using AuditTrail.API.Models;
using AuditTrail.Application.Commands;
using AuditTrail.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditTrail.API.Controllers;

/// <summary>
/// REST API controller for audit trail operations.
/// Supports appending events, querying state, and retrieving history.
/// </summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IMediator mediator, ILogger<AuditController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Append a new event to an entity's event stream.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="request">The event details to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created event details with new version</returns>
    /// <response code="201">Event created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="409">Concurrency conflict - version mismatch</response>
    [HttpPost("{entityId}/events")]
    [ProducesResponseType(typeof(AppendEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AppendEvent(
        [FromRoute] string entityId,
        [FromBody] AppendEventRequest request,
        CancellationToken cancellationToken)
    {
        // Get correlation ID from middleware or request
        var correlationId = request.CorrelationId ?? HttpContext.GetCorrelationId();

        var command = new AppendEventCommand
        {
            EntityId = entityId,
            EntityType = request.EntityType,
            EventType = request.EventType,
            OccurredBy = request.OccurredBy,
            ExpectedVersion = request.ExpectedVersion,
            Payload = request.Payload,
            SchemaVersion = request.SchemaVersion,
            CorrelationId = correlationId,
            CausationId = request.CausationId
        };

        var result = await _mediator.Send(command, cancellationToken);

        var response = new AppendEventResponse
        {
            EventId = result.EventId,
            NewVersion = result.NewVersion,
            CorrelationId = result.CorrelationId
        };

        return CreatedAtAction(
            nameof(GetEntityHistory),
            new { entityId },
            response);
    }

    /// <summary>
    /// Get the current state of an entity by replaying all events.
    /// Optionally specify a point in time to get historical state.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="at">Optional timestamp to get state at a specific point in time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The projected entity state</returns>
    /// <response code="200">State retrieved successfully</response>
    /// <response code="404">Entity not found</response>
    [HttpGet("{entityId}/state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntityState(
        [FromRoute] string entityId,
        [FromQuery] DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var query = new GetEntityStateQuery
        {
            EntityId = entityId,
            AsOf = at
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Entity Not Found",
                Detail = $"Entity '{entityId}' not found"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get the event history for an entity with optional pagination.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="from">Optional start date filter</param>
    /// <param name="to">Optional end date filter</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size (max 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity's event history</returns>
    /// <response code="200">History retrieved successfully</response>
    /// <response code="404">Entity not found</response>
    [HttpGet("{entityId}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntityHistory(
        [FromRoute] string entityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetEntityHistoryQuery
        {
            EntityId = entityId,
            From = from,
            To = to,
            PageNumber = pageNumber,
            PageSize = pageSize.HasValue ? Math.Min(pageSize.Value, 100) : null
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Entity Not Found",
                Detail = $"Entity '{entityId}' not found"
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Search events across all entities with multiple filter criteria.
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of matching events</returns>
    /// <response code="200">Search completed successfully</response>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEvents(
        [FromQuery] SearchEventsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new SearchEventsQuery
        {
            EntityType = request.EntityType,
            EventType = request.EventType,
            OccurredBy = request.OccurredBy,
            EntityId = request.EntityId,
            CorrelationId = request.CorrelationId,
            From = request.From,
            To = request.To,
            PageNumber = request.PageNumber < 1 ? 1 : request.PageNumber,
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
}
