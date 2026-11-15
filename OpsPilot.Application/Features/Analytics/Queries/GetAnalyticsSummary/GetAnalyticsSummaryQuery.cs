using MediatR;
using OpsPilot.Contracts.Analytics;

namespace OpsPilot.Application.Features.Analytics.Queries.GetAnalyticsSummary;

/// <summary>
/// Reads the dashboard's headline numbers (plan, UC-13 and FR-19). The window is optional:
/// with no dates the whole incident history is summarised, and the tenant is never supplied
/// by the caller — the global query filter guarantees it.
/// </summary>
public sealed record GetAnalyticsSummaryQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IRequest<AnalyticsSummaryDto>;
