using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Postmortems.Commands.CreatePostmortem;
using OpsPilot.Application.Features.Postmortems.Commands.PublishPostmortem;
using OpsPilot.Application.Features.Postmortems.Commands.ReturnPostmortemToDraft;
using OpsPilot.Application.Features.Postmortems.Commands.SubmitPostmortemForReview;
using OpsPilot.Application.Features.Postmortems.Commands.UpdatePostmortem;
using OpsPilot.Application.Features.Postmortems.Queries.GetAllPostmortems;
using OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemById;
using OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemByIncident;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The retrospective written after an incident (ERD §3).
/// </summary>
/// <remarks>
/// <para>
/// One postmortem per incident. It is opened against the incident, read either by its own id or
/// by the incident's, and moves through Draft → InReview → Published. A review can be rejected,
/// which returns it to Draft; publication additionally requires the incident itself to be
/// resolved or closed, because a story that is still changing is not a finished one.
/// </para>
/// <para>
/// Writing is split from publishing on purpose: the people who investigate an incident are not
/// necessarily the people trusted to make its account permanent.
/// </para>
/// </remarks>
[Route("api/v1/postmortems")]
public sealed class PostmortemsController : BaseController
{
    /// <summary>Pages the company's retrospectives, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionNames.PostmortemsRead)]
    public async Task<ActionResult<PagedResponse<PostmortemDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? incidentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetAllPostmortemsQuery(status, incidentId, page, pageSize));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.PostmortemsRead)]
    public async Task<ActionResult<Response<PostmortemDto>>> GetById(Guid id)
    {
        // There is no by-id query: the incident is the canonical way in, so this reads through
        // the same repository the incident-scoped endpoint does.
        var result = await Mediator.Send(new GetPostmortemByIdQuery(id));

        if (result is null)
        {
            return NotFound(Failure<PostmortemDto>(
                $"Postmortem with id '{id}' was not found.",
                BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    /// <summary>Reads the retrospective written for an incident.</summary>
    [HttpGet("/api/v1/incidents/{incidentId:guid}/postmortem")]
    [Authorize(Policy = PermissionNames.PostmortemsRead)]
    public async Task<ActionResult<Response<PostmortemDto>>> GetByIncident(Guid incidentId)
    {
        var result = await Mediator.Send(new GetPostmortemByIncidentQuery(incidentId));

        if (result is null)
        {
            return NotFound(Failure<PostmortemDto>(
                $"Incident '{incidentId}' has no postmortem.",
                BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    /// <summary>Opens the incident's retrospective. There can only be one.</summary>
    [HttpPost("/api/v1/incidents/{incidentId:guid}/postmortem")]
    [Authorize(Policy = PermissionNames.PostmortemsWrite)]
    public async Task<ActionResult<Response<PostmortemDto>>> Create(
        Guid incidentId,
        [FromBody] CreatePostmortemRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(Failure<PostmortemDto>("Unauthorized.", BusinessCode.Unauthorized));
        }

        var result = await Mediator.Send(
            new CreatePostmortemCommand(
                CurrentTenant.TenantId,
                incidentId,
                userId,
                request.Title,
                request.Summary,
                request.RootCause,
                request.ActionItems));

        return Ok(Success(result, BusinessCode.Created));
    }

    /// <summary>Rewrites the body of a draft retrospective.</summary>
    [HttpPut("/api/v1/incidents/{incidentId:guid}/postmortem")]
    [Authorize(Policy = PermissionNames.PostmortemsWrite)]
    public async Task<ActionResult<Response<PostmortemDto>>> Update(
        Guid incidentId,
        [FromBody] UpdatePostmortemRequest request)
    {
        var result = await Mediator.Send(
            new UpdatePostmortemCommand(
                incidentId,
                request.Title,
                request.Summary,
                request.RootCause,
                request.ActionItems));

        return Ok(Success(result));
    }

    /// <summary>Sends a draft for review.</summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = PermissionNames.PostmortemsWrite)]
    public async Task<ActionResult<Response<PostmortemDto>>> SubmitForReview(Guid id)
    {
        var result = await Mediator.Send(new SubmitPostmortemForReviewCommand(id));

        return Ok(Success(result));
    }

    /// <summary>Rejects the review and returns the retrospective to its author.</summary>
    [HttpPost("{id:guid}/return-to-draft")]
    [Authorize(Policy = PermissionNames.PostmortemsWrite)]
    public async Task<ActionResult<Response<PostmortemDto>>> ReturnToDraft(Guid id)
    {
        var result = await Mediator.Send(new ReturnPostmortemToDraftCommand(id));

        return Ok(Success(result));
    }

    /// <summary>
    /// Publishes a reviewed retrospective. The incident must be resolved or closed.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = PermissionNames.PostmortemsPublish)]
    public async Task<ActionResult<Response<PostmortemDto>>> Publish(Guid id)
    {
        var result = await Mediator.Send(new PublishPostmortemCommand(id));

        return Ok(Success(result));
    }
}
