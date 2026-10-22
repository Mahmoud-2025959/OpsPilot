using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Teams.Commands.AddTeamMember;
using OpsPilot.Application.Features.Teams.Commands.CreateTeam;
using OpsPilot.Application.Features.Teams.Commands.RemoveTeamMember;
using OpsPilot.Application.Features.Teams.Queries.GetAllTeams;
using OpsPilot.Contracts.Teams;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// Teams and their membership (ERD §3). Reading is open to any signed-in user; creating teams and
/// changing membership require <c>teams.manage</c>.
/// </summary>
/// <remarks>
/// Versioned only, like every other controller: the plan's contract is <c>api/v1</c> throughout,
/// so there is no unversioned surface to keep in step.
/// </remarks>
[Route("api/v1/teams")]
public sealed class TeamsController : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TeamDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllTeamsQuery(page, pageSize));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PermissionNames.TeamsManage)]
    public async Task<ActionResult<Response<Guid>>> Create([FromBody] CreateTeamRequest request)
    {
        // The tenant comes from the token, never from the body.
        var id = await Mediator.Send(
            new CreateTeamCommand(CurrentTenant.TenantId, request.Name, request.Description));

        return Ok(Success(id, BusinessCode.Created));
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = PermissionNames.TeamsManage)]
    public async Task<ActionResult<Response<TeamDto>>> AddMember(
        Guid id,
        [FromBody] AddTeamMemberRequest request)
    {
        if (!TryParseTeamRole(request.Role, out var role))
        {
            return BadRequest(Failure<TeamDto>(
                "A valid team role is required. Expected one of: Lead, Member.",
                BusinessCode.ValidationError));
        }

        var team = await Mediator.Send(new AddTeamMemberCommand(id, request.UserId, role));
        return Ok(Success(team, BusinessCode.Created));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = PermissionNames.TeamsManage)]
    public async Task<ActionResult<Response<TeamDto>>> RemoveMember(Guid id, Guid userId)
    {
        var team = await Mediator.Send(new RemoveTeamMemberCommand(id, userId));
        return Ok(Success(team));
    }

    /// <summary>An omitted role means <see cref="TeamRole.Member"/>.</summary>
    private static bool TryParseTeamRole(string? value, out TeamRole? role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = TeamRole.Member;
            return true;
        }

        if (Enum.TryParse(value, ignoreCase: true, out TeamRole parsed) && Enum.IsDefined(parsed))
        {
            role = parsed;
            return true;
        }

        role = null;
        return false;
    }
}
