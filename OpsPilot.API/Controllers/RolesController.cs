using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Roles.Commands.CreateRole;
using OpsPilot.Application.Features.Roles.Commands.DeleteRole;
using OpsPilot.Application.Features.Roles.Commands.SetRolePermissions;
using OpsPilot.Application.Features.Roles.Commands.UpdateRole;
using OpsPilot.Application.Features.Roles.Queries.GetAllRoles;
using OpsPilot.Application.Features.Roles.Queries.GetRoleById;
using OpsPilot.Contracts.Roles;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The company's roles and what each one grants (ERD §3).
/// </summary>
/// <remarks>
/// <para>
/// Reading is open to any signed-in user, so an account can be shown what a role is for
/// before it is asked to hold one. Changing anything requires <c>roles.change</c>.
/// </para>
/// <para>
/// The rules the endpoints enforce, all of them from ERD §3:
/// the five system roles cannot be renamed or retired, the Owner role's permission set is
/// frozen, and nobody may grant a permission they do not hold themselves. Every change is
/// recorded on the audit trail.
/// </para>
/// </remarks>
[Route("api/v1/roles")]
public sealed class RolesController : BaseController
{
    /// <summary>Lists the company's roles, each with the permissions it grants.</summary>
    [HttpGet]
    public async Task<ActionResult<Response<IReadOnlyList<RoleDto>>>> GetAll()
    {
        var result = await Mediator.Send(new GetAllRolesQuery());

        return Ok(Success(result));
    }

    /// <summary>Reads one role together with the permissions it grants.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<RoleDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetRoleByIdQuery(id));

        if (result is null)
        {
            return NotFound(Failure<RoleDto>($"Role with id '{id}' was not found.", BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    /// <summary>
    /// Creates a custom role. The permissions it starts with must be ones the caller holds
    /// themselves, unless the caller is an Owner.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermissionNames.RolesChange)]
    public async Task<ActionResult<Response<RoleDto>>> Create([FromBody] CreateRoleRequest request)
    {
        // The company comes from the token, never from the body.
        var result = await Mediator.Send(
            new CreateRoleCommand(
                CurrentTenant.TenantId,
                request.Name,
                request.Description,
                request.Permissions ?? []));

        return Ok(Success(result, BusinessCode.Created));
    }

    /// <summary>
    /// Renames a custom role and changes its description. System roles are refused.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionNames.RolesChange)]
    public async Task<ActionResult<Response<RoleDto>>> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var result = await Mediator.Send(
            new UpdateRoleCommand(
                CurrentTenant.TenantId,
                id,
                request.Name,
                request.Description));

        return Ok(Success(result));
    }

    /// <summary>
    /// Retires a custom role. The role is kept in the database, so the accounts that held it
    /// stay resolvable; they simply stop holding it.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionNames.RolesChange)]
    public async Task<ActionResult<Response<RoleDto>>> Delete(Guid id)
    {
        var result = await Mediator.Send(
            new DeleteRoleCommand(CurrentTenant.TenantId, id));

        return Ok(Success(result));
    }

    /// <summary>
    /// Replaces the whole permission set of a role. The list is authoritative: permissions
    /// left out are revoked. The Owner role's set is refused, and the permissions granted must
    /// be ones the caller holds themselves, unless the caller is an Owner.
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    [Authorize(Policy = PermissionNames.RolesChange)]
    public async Task<ActionResult<Response<RoleDto>>> SetPermissions(
        Guid id,
        [FromBody] SetRolePermissionsRequest request)
    {
        var result = await Mediator.Send(
            new SetRolePermissionsCommand(
                CurrentTenant.TenantId,
                id,
                request.Permissions ?? []));

        return Ok(Success(result));
    }
}
