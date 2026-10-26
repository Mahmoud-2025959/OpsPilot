using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Tenants.Commands.ActivateTenant;
using OpsPilot.Application.Features.Tenants.Commands.DeactivateTenant;
using OpsPilot.Application.Features.Tenants.Commands.RenameTenant;
using OpsPilot.Application.Features.Tenants.Queries.GetCurrentTenant;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The company's own settings (ERD §1).
/// </summary>
/// <remarks>
/// <para>
/// There is one company per request, so the endpoints act on the caller's own company and take
/// no id: there is no way to address somebody else's. The permission they need,
/// <c>tenant.settings</c>, is held by the Owner alone.
/// </para>
/// <para>
/// Deactivating closes the company: its people can no longer sign in, and its data stays where
/// it is. It cannot be deactivated while it has no usable Owner, because a company nobody can
/// sign into is one that can never be brought back.
/// </para>
/// </remarks>
[Route("api/v1/tenant")]
public sealed class TenantsController : BaseController
{
    /// <summary>Reads the caller's company.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionNames.TenantSettings)]
    public async Task<ActionResult<Response<TenantDto>>> Get()
    {
        var result = await Mediator.Send(new GetCurrentTenantQuery(CurrentTenant.TenantId));

        return Ok(Success(result));
    }

    /// <summary>Renames the caller's company.</summary>
    [HttpPut]
    [Authorize(Policy = PermissionNames.TenantSettings)]
    public async Task<ActionResult<Response<TenantDto>>> Rename(
        [FromBody] RenameTenantRequest request)
    {
        var result = await Mediator.Send(
            new RenameTenantCommand(CurrentTenant.TenantId, request.Name));

        return Ok(Success(result));
    }

    /// <summary>Closes the caller's company.</summary>
    [HttpPost("deactivate")]
    [Authorize(Policy = PermissionNames.TenantSettings)]
    public async Task<ActionResult<Response<TenantDto>>> Deactivate()
    {
        var result = await Mediator.Send(new DeactivateTenantCommand(CurrentTenant.TenantId));

        return Ok(Success(result));
    }

    /// <summary>Brings the caller's company back into use.</summary>
    [HttpPost("activate")]
    [Authorize(Policy = PermissionNames.TenantSettings)]
    public async Task<ActionResult<Response<TenantDto>>> Activate()
    {
        var result = await Mediator.Send(new ActivateTenantCommand(CurrentTenant.TenantId));

        return Ok(Success(result));
    }
}
