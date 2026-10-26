using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.ApiKeys.Commands.CreateApiKey;
using OpsPilot.Application.Features.ApiKeys.Commands.RevokeApiKey;
using OpsPilot.Application.Features.ApiKeys.Commands.RotateApiKey;
using OpsPilot.Application.Features.ApiKeys.Queries.GetAllApiKeys;
using OpsPilot.Application.Features.ApiKeys.Queries.GetApiKey;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// Issue, list, read and revoke the keys applications use to ingest events (plan, FR-13). Every
/// endpoint requires <c>apikeys.manage</c>, including the list, because the prefix of a key is
/// still sensitive enough to keep behind an administrative permission.
/// </summary>
/// <remarks>
/// The raw secret is stored encrypted and read back through these endpoints, so a key that was
/// issued and then lost can be recovered instead of having to be revoked and reissued. The
/// list therefore carries the secret of every key it returns; that is deliberate, and it is
/// why the permission is required on it.
/// </remarks>
[Route("api/v1/api-keys")]
public sealed class ApiKeysController : BaseController
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.ApiKeysManage)]
    public async Task<ActionResult<PagedResponse<ApiKeyDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllApiKeysQuery(page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.ApiKeysManage)]
    public async Task<ActionResult<Response<ApiKeyDto>>> GetById(Guid id)
    {
        var key = await Mediator.Send(new GetApiKeyQuery(id));
        return Ok(Success(key));
    }

    [HttpPost]
    [Authorize(Policy = PermissionNames.ApiKeysManage)]
    public async Task<ActionResult<Response<CreatedApiKeyDto>>> Create(
        [FromBody] CreateApiKeyRequest request)
    {
        var created = await Mediator.Send(
            new CreateApiKeyCommand(CurrentTenant.TenantId, request.Name, request.ExpiresAt));

        return Ok(Success(created, BusinessCode.Created));
    }

    /// <summary>
    /// Issues a new secret for an existing key. The previous secret stops working immediately.
    /// This is the way to give a key a readable secret when it was issued before the secret
    /// began being stored.
    /// </summary>
    [HttpPost("{id:guid}/rotate")]
    [Authorize(Policy = PermissionNames.ApiKeysManage)]
    public async Task<ActionResult<Response<CreatedApiKeyDto>>> Rotate(Guid id)
    {
        var rotated = await Mediator.Send(new RotateApiKeyCommand(id));
        return Ok(Success(rotated, BusinessCode.Created));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionNames.ApiKeysManage)]
    public async Task<ActionResult<Response<object?>>> Revoke(Guid id)
    {
        await Mediator.Send(new RevokeApiKeyCommand(id));
        return Ok(Success<object?>(null));
    }
}
