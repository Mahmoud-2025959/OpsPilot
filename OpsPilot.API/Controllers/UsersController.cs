using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Users.Commands.ChangeUserRoles;
using OpsPilot.Application.Features.Users.Commands.ChangeUserStatus;
using OpsPilot.Application.Features.Users.Commands.CreateUser;
using OpsPilot.Application.Features.Users.Commands.DeleteUser;
using OpsPilot.Application.Features.Users.Commands.UpdateUser;
using OpsPilot.Application.Features.Users.Commands.UpdateUserProfile;
using OpsPilot.Application.Features.Users.Queries.GetAllUsers;
using OpsPilot.Application.Features.Users.Queries.GetCurrentUser;
using OpsPilot.Application.Features.Users.Queries.GetUserById;
using OpsPilot.Application.Features.Users.Queries.GetUserPermissions;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

[Route("api/v1/users")]
public sealed class UsersController : BaseController
{
    /// <summary>The signed-in user's own profile plus the permissions carried by their token.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<Response<CurrentUserDto>>> GetCurrentUser()
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<CurrentUserDto>("Unauthorized.", BusinessCode.Unauthorized));
        }

        var result = await Mediator.Send(new GetCurrentUserQuery(userId));

        if (result is null)
        {
            return NotFound(Failure<CurrentUserDto>(ExceptionMessages.UserNotFound(userId), BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    [HttpGet]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<PagedResponse<UserDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllUsersQuery(search, role, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<UserDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetUserByIdQuery(id));

        if (result is null)
        {
            return NotFound(Failure<UserDto>(ExceptionMessages.UserNotFound(id), BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<Guid>>> Create([FromBody] CreateUserCommand command)
    {
        // The tenant is taken from the token, never from the body: an admin can only add users
        // to their own company (ERD §3, "Identity و Tenancy").
        var result = await Mediator.Send(command with { TenantId = CurrentTenant.TenantId });
        return Ok(Success(result, BusinessCode.Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<UserDto>>> Update(Guid id, [FromBody] UpdateUserCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });

        if (result is null)
        {
            return NotFound(Failure<UserDto>(ExceptionMessages.UserNotFound(id), BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<object?>>> Delete(Guid id)
    {
        await Mediator.Send(new DeleteUserCommand(id));
        return Ok(Success<object?>(null));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<Response<UserDto>>> UpdateProfile([FromBody] UpdateUserProfileCommand command)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<UserDto>("Unauthorized.", BusinessCode.Unauthorized));
        }

        var result = await Mediator.Send(command with { UserId = userId });

        if (result is null)
        {
            return NotFound(Failure<UserDto>(ExceptionMessages.UserNotFound(userId), BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    /// <summary>
    /// The permissions a user actually holds, combined from every role they carry. Retired roles
    /// grant nothing.
    /// </summary>
    [HttpGet("{id:guid}/permissions")]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<UserPermissionsDto>>> GetPermissions(Guid id)
    {
        var result = await Mediator.Send(new GetUserPermissionsQuery(id));

        if (result is null)
        {
            return NotFound(Failure<UserPermissionsDto>(ExceptionMessages.UserNotFound(id), BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    /// <summary>
    /// Replaces the roles a user holds. Requires <c>roles.change</c> rather than
    /// <c>users.manage</c>, because changing what someone can do is a different privilege from
    /// editing their profile (ERD §3).
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = PermissionNames.RolesChange)]
    public async Task<ActionResult<Response<UserDto>>> ChangeRoles(
        Guid id,
        [FromBody] ChangeUserRolesRequest request)
    {
        // The tenant comes from the token, never from the body.
        var result = await Mediator.Send(
            new ChangeUserRolesCommand(CurrentTenant.TenantId, id, request.Roles ?? []));

        return Ok(Success(result));
    }

    /// <summary>Activates or deactivates an account. Sign-in rejects inactive accounts.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = PermissionNames.UsersManage)]
    public async Task<ActionResult<Response<UserDto>>> ChangeStatus(
        Guid id,
        [FromBody] ChangeUserStatusRequest request)
    {
        var result = await Mediator.Send(
            new ChangeUserStatusCommand(CurrentTenant.TenantId, id, request.IsActive));

        return Ok(Success(result));
    }
}
