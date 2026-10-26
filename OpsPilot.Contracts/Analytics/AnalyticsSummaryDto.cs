namespace OpsPilot.Contracts.Analytics;

/// <summary>Incident count for one severity band.</summary>
public sealed record SeverityCountDto(string Severity, int Count);

/// <summary>Incident count for one service.</summary>
public sealed record ServiceCountDto(Guid ServiceId, string ServiceName, int Count);

/// <summary>
/// Incident count for one owning team. <see cref="TeamId"/> is null for incidents whose
/// service nobody owns yet, which the dashboard shows as its own bucket rather than dropping.
/// </summary>
public sealed record TeamCountDto(Guid? TeamId, string TeamName, int Count);

/// <summary>
/// The dashboard's headline numbers (plan, UC-13 and FR-19).
/// </summary>
/// <remarks>
/// Rates and averages are null rather than zero when nothing is measurable — a brand-new
/// company has no resolved incidents, and reporting "0 minutes average resolution" or
/// "0% compliance" for it would turn the absence of data into the worst possible reading.
/// <see cref="SlaComplianceRate"/> is a fraction between 0 and 1, not a percentage.
/// </remarks>
public sealed record AnalyticsSummaryDto(
    int TotalIncidents,
    IReadOnlyList<SeverityCountDto> BySeverity,
    IReadOnlyList<ServiceCountDto> ByService,
    IReadOnlyList<TeamCountDto> ByTeam,
    double? AvgResponseMinutes,
    double? AvgResolutionMinutes,
    double? SlaComplianceRate);
