using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

/// <summary>Incident count for one service, as read back from the store.</summary>
public sealed record ServiceCount(Guid ServiceId, string ServiceName, int Count);

/// <summary>Incident count for one owning team; a null team means an unowned service.</summary>
public sealed record TeamCount(Guid? TeamId, string TeamName, int Count);

/// <summary>
/// Raw aggregation behind the dashboard (plan, FR-19). Severities come back only for the
/// bands that actually have incidents and in no particular order — zero-filling and ordering
/// are presentation concerns and stay in the handler.
/// </summary>
public sealed record AnalyticsSnapshot(
    int TotalIncidents,
    IReadOnlyDictionary<Severity, int> CountsBySeverity,
    IReadOnlyList<ServiceCount> CountsByService,
    IReadOnlyList<TeamCount> CountsByTeam,
    double? AvgResponseMinutes,
    double? AvgResolutionMinutes,
    double? SlaComplianceRate);

/// <summary>Aggregates the incident set for the dashboard (plan, UC-13 and FR-19).</summary>
public interface IAnalyticsRepository
{
    Task<AnalyticsSnapshot> GetSummaryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
