using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Auth.Commands.ChangePassword;
using OpsPilot.Application.Features.Auth.Commands.Login;
using OpsPilot.Application.Features.Auth.Commands.Logout;
using OpsPilot.Application.Features.Auth.Commands.RefreshToken;
using OpsPilot.Application.Features.Auth.Commands.RegisterTenant;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API.Controllers;

// The versioned prefix is the contract from the plan, and it is the only surface: there are no
// unversioned aliases anywhere, so a route is reachable at exactly one address.
[Route("api/v1/auth")]
public sealed class AuthController : BaseController
{
    /// <summary>
    /// Self-serve sign-up. Creates the company, seeds its roles/permissions/SLA policies and
    /// returns the new Owner signed in.
    /// </summary>
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<AuthResponseDto>>> RegisterTenant([FromBody] RegisterTenantCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(Success(result, BusinessCode.Created));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<AuthResponseDto>>> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(Success(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<RefreshTokenResponseDto>>> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(Success(result));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<Response<object?>>> Logout([FromBody] LogoutCommand command)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(Failure<object?>("Unauthorized.", BusinessCode.Unauthorized));
        }

        command.UserId = userId;
        await Mediator.Send(command);
        return Ok(Success<object?>(null));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<Response<object?>>> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(Failure<object?>("Unauthorized.", BusinessCode.Unauthorized));
        }

        command.UserId = userId;
        await Mediator.Send(command);
        return Ok(Success<object?>(null));
    }
}
