using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Analytics;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Analytics.Queries.GetAnalyticsSummary;

public sealed class GetAnalyticsSummaryQueryHandler
    : IRequestHandler<GetAnalyticsSummaryQuery, AnalyticsSummaryDto>
{
    /// <summary>Shown for incidents whose service has no owner team.</summary>
    private const string UnassignedTeam = "Unassigned";

    private readonly IAnalyticsRepository _repository;

    public GetAnalyticsSummaryQueryHandler(IAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<AnalyticsSummaryDto> Handle(
        GetAnalyticsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetSummaryAsync(
            request.From,
            request.To,
            cancellationToken);

        // Enum members are stored as strings, so SQL would order the bands alphabetically
        // (Critical, High, Low, Medium). The numeric order only survives if it is applied
        // here, and a band with no incidents has to appear with a count of zero -- a dashboard
        // that silently drops a severity is a dashboard that looks broken.
        var bySeverity = Enum.GetValues<Severity>()
            .OrderBy(severity => severity)
            .Select(severity => new SeverityCountDto(
                severity.ToString(),
                snapshot.CountsBySeverity.TryGetValue(severity, out var count) ? count : 0))
            .ToList();

        var byService = snapshot.CountsByService
            .OrderByDescending(service => service.Count)
            .ThenBy(service => service.ServiceName, StringComparer.Ordinal)
            .Select(service => new ServiceCountDto(service.ServiceId, service.ServiceName, service.Count))
            .ToList();

        var byTeam = snapshot.CountsByTeam
            .OrderByDescending(team => team.Count)
            .ThenBy(team => team.TeamName, StringComparer.Ordinal)
            .Select(team => new TeamCountDto(team.TeamId, team.TeamName, team.Count))
            .ToList();

        return new AnalyticsSummaryDto(
            snapshot.TotalIncidents,
            bySeverity,
            byService,
            byTeam,
            Round(snapshot.AvgResponseMinutes, 2),
            Round(snapshot.AvgResolutionMinutes, 2),
            Round(snapshot.SlaComplianceRate, 4));
    }

    /// <summary>Two decimals reads cleanly on a dashboard; the extra digits below that are noise.</summary>
    private static double? Round(double? value, int decimals) =>
        value is { } v ? Math.Round(v, decimals) : null;
}
