using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Permissions.Queries.GetAllPermissions;
using OpsPilot.Contracts.Permissions;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The permission catalogue (ERD §1). Reading it is open to any signed-in user: the list is
/// global, fixed in code and shared by every company, and it is the same set of names the
/// caller's own token already carries.
/// </summary>
/// <remarks>
/// The catalogue is read-only over HTTP. Permissions are seeded from
/// <c>PermissionCatalog</c> whenever a company is provisioned, so a permission that is not in
/// the catalogue does not exist for anybody -- there is nothing here to create.
/// </remarks>
[Route("api/v1/permissions")]
public sealed class PermissionsController : BaseController
{
    /// <summary>Lists every permission the build knows about, grouped by category.</summary>
    [HttpGet]
    public async Task<ActionResult<Response<IReadOnlyList<PermissionDto>>>> GetAll()
    {
        var result = await Mediator.Send(new GetAllPermissionsQuery());

        return Ok(Success(result));
    }
}
