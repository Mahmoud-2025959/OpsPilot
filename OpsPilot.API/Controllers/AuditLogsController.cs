using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.AuditLogs.Queries.GetAuditLogs;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// Reads the company's audit trail (plan, UC-14 and FR-18).
/// </summary>
/// <remarks>
/// Read-only by design: the trail is append-only at the persistence boundary, so there is
/// deliberately no endpoint here that could amend or erase a record. Requires
/// <c>audit.read</c>, which only Owner and Admin hold.
/// </remarks>
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController : BaseController
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.AuditRead)]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetAll(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetAuditLogsQuery(action, entityType, entityId, actorUserId, from, to, page, pageSize));

        return Ok(result);
    }
}
