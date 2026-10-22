using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Services.Commands.CreateService;
using OpsPilot.Application.Features.Services.Commands.UpdateService;
using OpsPilot.Application.Features.Services.Queries.GetAllServices;
using OpsPilot.Application.Features.Services.Queries.GetServiceById;
using OpsPilot.Contracts.Services;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The service catalogue (ERD §3). Reading is open to any signed-in user; creating and editing
/// require <c>services.manage</c>.
/// </summary>
/// <remarks>
/// Versioned only, like every other controller: the plan's contract is <c>api/v1</c> throughout,
/// so there is no unversioned surface to keep in step.
/// </remarks>
[Route("api/v1/services")]
public sealed class ServicesController : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ServiceDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllServicesQuery(page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<ServiceDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetServiceByIdQuery(id));

        if (result is null)
        {
            return NotFound(Failure<ServiceDto>($"Service with id '{id}' was not found.", BusinessCode.NotFound));
        }

        return Ok(Success(result));
    }

    [HttpPost]
    [Authorize(Policy = PermissionNames.ServicesManage)]
    public async Task<ActionResult<Response<Guid>>> Create([FromBody] CreateServiceRequest request)
    {
        if (!TryParseEnvironment(request.Environment, out var environment))
        {
            return BadRequest(Failure<Guid>(
                "A valid environment is required. Expected one of: Production, Staging, Development.",
                BusinessCode.ValidationError));
        }

        // The tenant comes from the token, never from the body.
        var command = new CreateServiceCommand(
            CurrentTenant.TenantId,
            request.Name,
            environment,
            request.Slug,
            request.Description,
            request.RepositoryUrl,
            request.OwnerTeamId);

        var id = await Mediator.Send(command);
        return Ok(Success(id, BusinessCode.Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionNames.ServicesManage)]
    public async Task<ActionResult<Response<ServiceDto>>> Update(
        Guid id,
        [FromBody] UpdateServiceRequest request)
    {
        if (!TryParseEnvironment(request.Environment, out var environment))
        {
            return BadRequest(Failure<ServiceDto>(
                "A valid environment is required. Expected one of: Production, Staging, Development.",
                BusinessCode.ValidationError));
        }

        var updated = await Mediator.Send(new UpdateServiceCommand(
            id,
            request.Name,
            environment,
            request.Slug,
            request.Description,
            request.RepositoryUrl,
            request.OwnerTeamId));

        return Ok(Success(updated));
    }

    private static bool TryParseEnvironment(string? value, out EnvironmentType environment)
    {
        return Enum.TryParse(value, ignoreCase: true, out environment) && Enum.IsDefined(environment);
    }
}
