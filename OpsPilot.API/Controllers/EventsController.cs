using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsPilot.API.RateLimiting;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Events.Commands.IngestEvent;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Events;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The ingestion API an application uses to report operational events (plan, §2.3 and UC-06).
/// </summary>
/// <remarks>
/// <para>
/// Authenticated by <c>X-API-Key</c>, not by a bearer token: applications have their own security
/// path (plan, §2.3). No user role is granted <c>events.ingest</c>, so the policy below admits
/// exactly the keys and nobody else.
/// </para>
/// <para>
/// The tenant comes from the key, never from the body — the caller is never allowed to name the
/// company it writes into.
/// </para>
/// </remarks>
[Route("api/v1/events")]
public sealed class EventsController : BaseController
{
    // Ingestion is the only surface a machine hammers, so it is the only one carrying the
    // plan's limit: a fixed window per key, 429 past it (plan, UC-06).
    [HttpPost]
    [EnableRateLimiting(IngestionRateLimitingExtensions.IngestionPolicy)]
    [Authorize(Policy = PermissionNames.EventsIngest)]
    public async Task<ActionResult<Response<EventIngestResultDto>>> Ingest(
        [FromBody] IngestEventRequest request)
    {
        if (!TryParseEnvironment(request.Environment, out var environment))
        {
            return BadRequest(Failure<EventIngestResultDto>(
                ValidationMessages.ServiceEnvironmentInvalid,
                BusinessCode.ValidationError));
        }

        if (!TryParseSeverity(request.Severity, out var severity))
        {
            return BadRequest(Failure<EventIngestResultDto>(
                ValidationMessages.IngestedSeverityInvalid,
                BusinessCode.ValidationError));
        }

        if (!TryGetApiKeyId(out var apiKeyId) || CurrentUserId is not { } reportedBy)
        {
            return Unauthorized(Failure<EventIngestResultDto>(
                ExceptionMessages.ApiKeyUnusable,
                BusinessCode.Unauthorized));
        }

        var result = await Mediator.Send(new IngestEventCommand(
            CurrentTenant.TenantId,
            apiKeyId,
            reportedBy,
            request.Service,
            environment,
            severity,
            request.Message,
            request.Timestamp));

        // The event is recorded and its incident resolved, so there is nothing left to wait for;
        // 202 signals that it was accepted for processing (plan, UC-06).
        return Accepted(Success(result));
    }

    private bool TryGetApiKeyId(out Guid apiKeyId) =>
        Guid.TryParse(User.FindFirstValue(AuthClaimTypes.ApiKeyId), out apiKeyId)
        && apiKeyId != Guid.Empty;

    private static bool TryParseEnvironment(string? value, out EnvironmentType environment) =>
        Enum.TryParse(value, ignoreCase: true, out environment) && Enum.IsDefined(environment);

    private static bool TryParseSeverity(string? value, out Severity severity) =>
        Enum.TryParse(value, ignoreCase: true, out severity) && Enum.IsDefined(severity);
}
