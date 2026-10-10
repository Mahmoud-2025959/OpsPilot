using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Users.Commands.CreateUser;
using OpsPilot.Application.Features.Users.Commands.DeleteUser;
using OpsPilot.Application.Features.Users.Commands.UpdateUser;
using OpsPilot.Application.Features.Users.Commands.UpdateUserProfile;
using OpsPilot.Application.Features.Users.Queries.GetAllUsers;
using OpsPilot.Application.Features.Users.Queries.GetUserById;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API.Controllers;

[Route("api/users")]
public sealed class UsersController : BaseController
{
    [HttpGet]
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
    public async Task<ActionResult<Response<Guid>>> Create([FromBody] CreateUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(Success(result, BusinessCode.Created));
    }

    [HttpPut("{id:guid}")]
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
    public async Task<ActionResult<Response<object?>>> Delete(Guid id)
    {
        await Mediator.Send(new DeleteUserCommand(id));
        return Ok(Success<object?>(null));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<Response<UserDto>>> UpdateProfile([FromBody] UpdateUserProfileCommand command)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
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
}
