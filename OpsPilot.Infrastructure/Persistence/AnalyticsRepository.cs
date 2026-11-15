using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Aggregates the incident set for the dashboard (plan, UC-13 and FR-19).
/// </summary>
/// <remarks>
/// Every number is computed in the database rather than pulled into memory and summed here:
/// a company's incident history is the largest table in the system, and a dashboard that
/// loads it whole to count it is a dashboard that gets slower every month.
/// </remarks>
public sealed class AnalyticsRepository : IAnalyticsRepository
{
    /// <summary>Shown for incidents whose service has no owner team, rather than dropped.</summary>
    private const string UnassignedTeamName = "Unassigned";

    private readonly AppDbContext _context;

    public AnalyticsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AnalyticsSnapshot> GetSummaryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // the set to the ambient tenant.
        var incidents = _context.Incidents.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            incidents = incidents.Where(i => i.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            incidents = incidents.Where(i => i.CreatedAt <= to.Value);
        }

        var total = await incidents.CountAsync(cancellationToken);

        var severityRows = await incidents
            .GroupBy(i => i.Severity)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var serviceRows = await incidents
            .GroupBy(i => i.ServiceId)
            .Select(group => new { ServiceId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countsByService = serviceRows
            .Select(row => new ServiceCount(row.ServiceId, string.Empty, row.Count))
            .ToList();

        var byService = await NameServicesAsync(countsByService, cancellationToken);
        var byTeam = await BucketByOwnerTeamAsync(countsByService, cancellationToken);

        // Response is measured from creation to acknowledgement; incidents nobody has picked
        // up yet are not late by these numbers -- the SLA job reports that separately. The
        // difference goes through DateDiffMinute because DateTimeOffset arithmetic does not
        // translate to SQL; whole minutes are the right grain for a dashboard anyway.
        var avgResponse = await incidents
            .Where(i => i.AcknowledgedAt != null)
            .AverageAsync(
                i => (double?)EF.Functions.DateDiffMinute(i.CreatedAt, i.AcknowledgedAt!.Value),
                cancellationToken);

        var avgResolution = await incidents
            .Where(i => i.ResolvedAt != null)
            .AverageAsync(
                i => (double?)EF.Functions.DateDiffMinute(i.CreatedAt, i.ResolvedAt!.Value),
                cancellationToken);

        // Compliance is measurable only where both ends exist: an incident with no deadline
        // was never under an SLA, and one still open has not missed (or beaten) it yet.
        var measurable = await incidents.CountAsync(
            i => i.ResolvedAt != null && i.ResolutionDueAt != null,
            cancellationToken);

        var withinSla = measurable == 0
            ? 0
            : await incidents.CountAsync(
                i => i.ResolvedAt != null
                    && i.ResolutionDueAt != null
                    && i.ResolvedAt <= i.ResolutionDueAt,
                cancellationToken);

        double? complianceRate = measurable == 0 ? null : (double)withinSla / measurable;

        return new AnalyticsSnapshot(
            total,
            severityRows.ToDictionary(row => row.Key, row => row.Count),
            byService,
            byTeam,
            avgResponse,
            avgResolution,
            complianceRate);
    }

    /// <summary>Attaches each bucket's service name, from one lookup query for all of them.</summary>
    private async Task<IReadOnlyList<ServiceCount>> NameServicesAsync(
        IReadOnlyList<ServiceCount> buckets,
        CancellationToken cancellationToken)
    {
        var ids = buckets.Select(bucket => bucket.ServiceId).ToList();

        var services = await _context.Services
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        var namesById = services.ToDictionary(s => s.Id, s => s.Name);

        // A service deleted since the incidents were filed still deserves a row: the count is
        // real history, and silently folding it away would make the totals stop adding up.
        return buckets
            .Select(bucket => new ServiceCount(
                bucket.ServiceId,
                namesById.TryGetValue(bucket.ServiceId, out var name) ? name : "Unknown service",
                bucket.Count))
            .ToList();
    }

    /// <summary>Rolls the per-service counts up onto the teams that own those services.</summary>
    private async Task<IReadOnlyList<TeamCount>> BucketByOwnerTeamAsync(
        IReadOnlyList<ServiceCount> buckets,
        CancellationToken cancellationToken)
    {
        var ids = buckets.Select(bucket => bucket.ServiceId).ToList();

        var services = await _context.Services
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.OwnerTeamId })
            .ToListAsync(cancellationToken);

        var ownerByService = services.ToDictionary(s => s.Id, s => s.OwnerTeamId);

        var teamIds = ownerByService.Values
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var teams = await _context.Teams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(cancellationToken);

        var namesById = teams.ToDictionary(t => t.Id, t => t.Name);

        var rolled = new Dictionary<Guid, (string Name, int Count)>();

        foreach (var bucket in buckets)
        {
            // Guid.Empty stands in for "no owner team": a Dictionary refuses a null key even
            // when the value type would allow one, and the sentinel costs nothing here.
            var teamId = ownerByService.TryGetValue(bucket.ServiceId, out var owner) && owner.HasValue
                ? owner.Value
                : Guid.Empty;

            var name = teamId == Guid.Empty
                ? UnassignedTeamName
                : namesById.TryGetValue(teamId, out var teamName) ? teamName : "Unknown team";

            var current = rolled.GetValueOrDefault(teamId);
            rolled[teamId] = (name, current.Count + bucket.Count);
        }

        return rolled
            .Select(pair => new TeamCount(
                pair.Key == Guid.Empty ? null : pair.Key,
                pair.Value.Name,
                pair.Value.Count))
            .ToList();
    }
}
