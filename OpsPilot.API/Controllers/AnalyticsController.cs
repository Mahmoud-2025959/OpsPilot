using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Analytics.Queries.GetAnalyticsSummary;
using OpsPilot.Contracts.Analytics;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The dashboard's headline numbers (plan, UC-13 and FR-19).
/// </summary>
/// <remarks>
/// Read-only and gated by <c>analytics.read</c>, which every role holds: the dashboard is the
/// one screen the whole company is meant to be able to look at.
/// </remarks>
[Route("api/v1/analytics")]
public sealed class AnalyticsController : BaseController
{
    [HttpGet("summary")]
    [Authorize(Policy = PermissionNames.AnalyticsRead)]
    public async Task<ActionResult<Response<AnalyticsSummaryDto>>> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var result = await Mediator.Send(new GetAnalyticsSummaryQuery(from, to));
        return Ok(Success(result));
    }
}
