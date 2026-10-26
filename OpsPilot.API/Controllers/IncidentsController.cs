using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Incidents.Commands;
using OpsPilot.Application.Features.Incidents.Commands.AddIncidentComment;
using OpsPilot.Application.Features.Incidents.Commands.AssignIncident;
using OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;
using OpsPilot.Application.Features.Incidents.Commands.UpdateIncident;
using OpsPilot.Application.Features.Incidents.Queries.GetAllIncidents;
using OpsPilot.Application.Features.Incidents.Queries.GetIncidentById;
using OpsPilot.Application.Features.Incidents.Queries.GetIncidentComments;
using OpsPilot.Application.Features.Incidents.Queries.GetIncidentEvents;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The incident lifecycle (ERD §3). Every endpoint declares the permission it needs through a
/// policy named after that permission; the state transition endpoint additionally checks the
/// permission required by the <em>target</em> status, which is why it authorizes imperatively.
/// </summary>
[Route("api/v1/incidents")]
public sealed class IncidentsController : BaseController
{
    private readonly IAuthorizationService _authorization;

    public IncidentsController(IAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    [HttpGet]
    [Authorize(Policy = PermissionNames.IncidentsRead)]
    public async Task<ActionResult<PagedResponse<IncidentDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] Guid? serviceId,
        [FromQuery] Guid? assignedTo,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetAllIncidentsQuery(status, severity, serviceId, assignedTo, from, to, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.IncidentsRead)]
    public async Task<ActionResult<Response<IncidentDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetIncidentByIdQuery(id));

        if (result is null)
        {
            return NotFound(Failure<IncidentDto>($"Incident with id '{id}' was not found.", BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionNames.IncidentsCreate)]
    public async Task<ActionResult<Response<Guid>>> Create([FromBody] CreateIncidentRequest request)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<Guid>("Unauthorized.", BusinessCode.Unauthorized));
        }

        if (!TryParseSeverity(request.Severity, out var severity))
        {
            return BadRequest(Failure<Guid>("A valid severity is required.", BusinessCode.ValidationError));
        }

        // Tenant and reporter come from the token, never from the body.
        var command = new CreateIncidentCommand(
            CurrentTenant.TenantId,
            request.ServiceId,
            request.Title,
            request.Description,
            severity,
            userId,
            request.CorrelationKey);

        var id = await Mediator.Send(command);
        return Ok(Success(id, BusinessCode.Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionNames.IncidentsUpdate)]
    public async Task<ActionResult<Response<object?>>> Update(Guid id, [FromBody] UpdateIncidentRequest request)
    {
        if (!TryParseSeverity(request.Severity, out var severity))
        {
            return BadRequest(Failure<object?>("A valid severity is required.", BusinessCode.ValidationError));
        }

        await Mediator.Send(new UpdateIncidentCommand(id, request.Title, request.Description, severity));
        return Ok(Success<object?>(null));
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = PermissionNames.IncidentsAssign)]
    public async Task<ActionResult<Response<object?>>> Assign(Guid id, [FromBody] AssignIncidentRequest request)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<object?>("Unauthorized.", BusinessCode.Unauthorized));
        }

        await Mediator.Send(new AssignIncidentCommand(id, request.AssigneeId, userId));
        return Ok(Success<object?>(null));
    }

    /// <summary>
    /// Moves an incident to a new state. The permission required depends on the target state:
    /// everyday moves need <c>incidents.update</c>, resolving needs <c>incidents.resolve</c>,
    /// closing needs <c>incidents.close</c>, reopening needs <c>incidents.update</c> (ERD §3).
    /// Returns the incident as it stands after the move.
    /// </summary>
    [HttpPost("{id:guid}/transitions")]
    public async Task<ActionResult<Response<IncidentDto>>> Transition(Guid id, [FromBody] TransitionIncidentRequest request)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<IncidentDto>("Unauthorized.", BusinessCode.Unauthorized));
        }

        IncidentDto incident;

        switch (request.ToStatus?.Trim().ToLowerInvariant())
        {
            case "acknowledged":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsUpdate)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new AcknowledgeIncidentCommand(id, userId, request.Note));
                break;

            case "investigating":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsUpdate)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new StartInvestigatingIncidentCommand(id, userId, request.Note));
                break;

            case "mitigated":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsUpdate)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new MitigateIncidentCommand(id, userId, request.Note));
                break;

            case "resolved":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsResolve)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new ResolveIncidentCommand(id, userId, request.Note));
                break;

            case "closed":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsClose)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new CloseIncidentCommand(id, userId, request.Note));
                break;

            case "open":
                if (!await IsAuthorizedAsync(PermissionNames.IncidentsUpdate)) return Forbidden<IncidentDto>();
                incident = await Mediator.Send(new ReopenIncidentCommand(id, userId, request.Note));
                break;

            default:
                return BadRequest(Failure<IncidentDto>(
                    "Unknown target status. Expected one of: Open, Acknowledged, Investigating, Mitigated, Resolved, Closed.",
                    BusinessCode.ValidationError));
        }

        return Ok(Success(incident));
    }

    [HttpGet("{id:guid}/comments")]
    [Authorize(Policy = PermissionNames.IncidentsRead)]
    public async Task<ActionResult<Response<IReadOnlyList<IncidentCommentDto>>>> GetComments(Guid id)
    {
        var result = await Mediator.Send(new GetIncidentCommentsQuery(id));
        return Ok(Success(result));
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize(Policy = PermissionNames.IncidentsComment)]
    public async Task<ActionResult<Response<object?>>> AddComment(Guid id, [FromBody] AddIncidentCommentRequest request)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<object?>("Unauthorized.", BusinessCode.Unauthorized));
        }

        await Mediator.Send(new AddIncidentCommentCommand(id, userId, request.Body));
        return Ok(Success<object?>(null, BusinessCode.Created));
    }

    [HttpGet("{id:guid}/timeline")]
    [Authorize(Policy = PermissionNames.IncidentsRead)]
    public async Task<ActionResult<Response<IReadOnlyList<IncidentEventDto>>>> GetTimeline(Guid id)
    {
        var result = await Mediator.Send(new GetIncidentEventsQuery(id));
        return Ok(Success(result));
    }

    private async Task<bool> IsAuthorizedAsync(string permission)
    {
        var result = await _authorization.AuthorizeAsync(User, permission);
        return result.Succeeded;
    }

    private ActionResult<Response<T>> Forbidden<T>()
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            Failure<T>("You do not have permission to perform this transition.", BusinessCode.Forbidden));
    }

    private static bool TryParseSeverity(string? value, out Severity severity)
    {
        return Enum.TryParse(value, ignoreCase: true, out severity) && Enum.IsDefined(severity);
    }
}
