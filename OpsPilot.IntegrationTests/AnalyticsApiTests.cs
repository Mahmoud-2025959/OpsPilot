using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Features.Analytics.Queries.GetAnalyticsSummary;
using OpsPilot.Contracts.Analytics;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the dashboard summary (plan, UC-13 and FR-19): severity bands are zero-filled and
/// ordered by severity, services and owning teams carry the counts, the averages and the SLA
/// compliance fraction come out of the timestamps, and the optional window limits all of it.
/// </summary>
public sealed class AnalyticsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task An_Empty_Company_Summarises_To_Zeros_And_Nulls()
    {
        using var company = await db.CompanyAsync();

        var summary = await SummariseAsync(company);

        Assert.Equal(0, summary.TotalIncidents);

        // Every band is present with a count of zero -- a dashboard that silently drops a
        // severity looks broken -- and nothing measurable reports as zero, which would read
        // as the worst possible score.
        Assert.Equal(
            new[] { "Critical", "High", "Medium", "Low" },
            summary.BySeverity.Select(s => s.Severity).ToArray());
        Assert.All(summary.BySeverity, s => Assert.Equal(0, s.Count));

        Assert.Empty(summary.ByService);
        Assert.Empty(summary.ByTeam);
        Assert.Null(summary.AvgResponseMinutes);
        Assert.Null(summary.AvgResolutionMinutes);
        Assert.Null(summary.SlaComplianceRate);
    }

    [Fact]
    public async Task Severities_Are_Zero_Filled_And_Ordered_By_Severity_Not_Alphabetically()
    {
        using var company = await db.CompanyAsync();
        var service = company.Company.Service;

        await SeedIncidentAsync(company, service, Severity.High, MinutesAgo(60));
        await SeedIncidentAsync(company, service, Severity.High, MinutesAgo(50));
        await SeedIncidentAsync(company, service, Severity.Critical, MinutesAgo(40));
        await SeedIncidentAsync(company, service, Severity.Low, MinutesAgo(30));

        var summary = await SummariseAsync(company);

        Assert.Equal(4, summary.TotalIncidents);
        Assert.Equal(
            [("Critical", 1), ("High", 2), ("Medium", 0), ("Low", 1)],
            summary.BySeverity.Select(s => (s.Severity, s.Count)).ToArray());
    }

    [Fact]
    public async Task Services_And_Owning_Teams_Carry_The_Counts()
    {
        using var company = await db.CompanyAsync();

        var alpha = Team.Create(company.TenantId, "Alpha Ops");
        var beta = Team.Create(company.TenantId, "Beta Ops");
        await company.Context.Teams.AddRangeAsync(alpha, beta);
        await company.Context.SaveChangesAsync();

        // The seeded service comes from a setup context that is already disposed, so it is
        // detached -- mutating it would be silently dropped by the save. Loading it through
        // this scope's own context puts it under tracking before it is reassigned.
        var payments = await company.Context.Services
            .SingleAsync(s => s.Id == company.Company.Service.Id);
        payments.AssignOwnerTeam(alpha.Id);

        var search = Service.Create(company.TenantId, "Search", "search-analytics", EnvironmentType.Production);
        search.AssignOwnerTeam(beta.Id);

        var billing = Service.Create(company.TenantId, "Billing", "billing-analytics", EnvironmentType.Production);

        await company.Context.Services.AddRangeAsync(search, billing);
        await company.Context.SaveChangesAsync();

        await SeedIncidentAsync(company, payments, Severity.High, MinutesAgo(60));
        await SeedIncidentAsync(company, payments, Severity.Medium, MinutesAgo(50));
        await SeedIncidentAsync(company, search, Severity.Low, MinutesAgo(40));
        await SeedIncidentAsync(company, billing, Severity.Low, MinutesAgo(30));

        var summary = await SummariseAsync(company);

        // Busiest first; ties break by name so the list is stable between calls.
        Assert.Equal(
            [("Payments", 2), ("Billing", 1), ("Search", 1)],
            summary.ByService.Select(s => (s.ServiceName, s.Count)).ToArray());

        // The unowned service lands in its own bucket rather than vanishing from the roll-up.
        Assert.Equal(
            [("Alpha Ops", 2), ("Beta Ops", 1), ("Unassigned", 1)],
            summary.ByTeam.Select(t => (t.TeamName, t.Count)).ToArray());

        Assert.Null(Assert.Single(summary.ByTeam, t => t.TeamName == "Unassigned").TeamId);
    }

    [Fact]
    public async Task Averages_And_Compliance_Come_From_The_Timestamps()
    {
        using var company = await db.CompanyAsync();
        var service = company.Company.Service;

        var policy = SlaPolicy.Create(company.TenantId, Severity.High, 5, 10);
        await company.Context.SlaPolicies.AddAsync(policy);
        await company.Context.SaveChangesAsync();

        // The breach: acknowledged at minute 9 (inside the 5-minute target? no -- response
        // target is 5, so 9 already breaches it) and resolved at minute 50, far past the
        // 10-minute resolution target.
        var late = await SeedIncidentAsync(company, service, Severity.High, MinutesAgo(100));
        late.ApplySlaPolicy(policy.Id, policy.ResponseWindow, policy.ResolutionWindow);
        late.Acknowledge(MinutesAgo(91));
        late.StartInvestigating(MinutesAgo(80));
        late.Resolve(MinutesAgo(50));

        // The clean one: acknowledged at minute 3, resolved at minute 5, both inside target.
        var clean = await SeedIncidentAsync(company, service, Severity.High, MinutesAgo(30));
        clean.ApplySlaPolicy(policy.Id, policy.ResponseWindow, policy.ResolutionWindow);
        clean.Acknowledge(MinutesAgo(27));
        clean.StartInvestigating(MinutesAgo(26));
        clean.Resolve(MinutesAgo(25));

        await company.Context.SaveChangesAsync();

        var summary = await SummariseAsync(company);

        Assert.Equal(2, summary.TotalIncidents);
        Assert.NotNull(summary.AvgResponseMinutes);
        Assert.Equal(6d, summary.AvgResponseMinutes!.Value, precision: 2);
        Assert.NotNull(summary.AvgResolutionMinutes);
        Assert.Equal(27.5d, summary.AvgResolutionMinutes!.Value, precision: 2);

        // One of the two beat its deadline: the rate is a fraction, not a percentage.
        Assert.NotNull(summary.SlaComplianceRate);
        Assert.Equal(0.5d, summary.SlaComplianceRate!.Value, precision: 4);
    }

    [Fact]
    public async Task The_Window_Limits_What_Is_Summarised()
    {
        using var company = await db.CompanyAsync();
        var service = company.Company.Service;

        await SeedIncidentAsync(company, service, Severity.Critical, MinutesAgo(300));
        await SeedIncidentAsync(company, service, Severity.Low, MinutesAgo(10));

        var summary = await SummariseAsync(
            company,
            new GetAnalyticsSummaryQuery(From: MinutesAgo(60)));

        Assert.Equal(1, summary.TotalIncidents);
        Assert.Equal([("Critical", 0), ("High", 0), ("Medium", 0), ("Low", 1)],
            summary.BySeverity.Select(s => (s.Severity, s.Count)).ToArray());
    }

    [Fact]
    public async Task A_Viewer_Can_Read_The_Summary()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "analytics-viewer");

        // Viewer is the least privileged role and still holds analytics.read -- the dashboard
        // is the one screen the whole company may look at.
        var (viewer, _) = await ApiAuth.MemberClientAsync(factory, owner, "analytics-viewer", "Viewer");

        var response = await viewer.GetAsync("/api/v1/analytics/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Response<AnalyticsSummaryDto>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.True(payload!.Succeeded, string.Join(", ", payload.Errors));
        Assert.NotNull(payload.Data);
        Assert.Equal(0, payload.Data.TotalIncidents);
    }

    private static DateTimeOffset MinutesAgo(int minutes) => DateTimeOffset.UtcNow.AddMinutes(-minutes);

    private static Task<AnalyticsSummaryDto> SummariseAsync(
        CompanyScope company,
        GetAnalyticsSummaryQuery? query = null)
        => new GetAnalyticsSummaryQueryHandler(new AnalyticsRepository(company.Context))
            .Handle(query ?? new GetAnalyticsSummaryQuery(), CancellationToken.None);

    private static async Task<Incident> SeedIncidentAsync(
        CompanyScope company,
        Service service,
        Severity severity,
        DateTimeOffset createdAt)
    {
        var incident = Incident.Create(
            company.TenantId,
            service.Id,
            $"Incident at {createdAt:HH:mm}",
            "Seeded for the analytics summary.",
            severity,
            company.Company.User.Id,
            createdAt);

        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();
        return incident;
    }
}
