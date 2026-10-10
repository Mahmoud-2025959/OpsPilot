using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the filtered unique index that decides whether a new event opens a new incident
/// or joins an existing one (ERD section 3).
/// </summary>
/// <remarks>
/// This rule is enforced entirely by the database, so it is the one most likely to be
/// silently wrong. A mis-specified filter either lets duplicates through -- a second page
/// gets a second incident for one outage -- or blocks legitimate re-correlation after an
/// incident closes. Both directions are asserted here.
/// </remarks>
public sealed class CorrelationKeyTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task SecondOpenIncident_WithSameCorrelationKey_IsRejected()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        context.Incidents.Add(company.Company.AnIncident(correlationKey: "db-timeout-42"));
        await context.SaveChangesAsync();

        context.Incidents.Add(company.Company.AnIncident(correlationKey: "db-timeout-42"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CorrelationKey_CanBeReused_AfterTheIncidentIsClosed()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        context.Incidents.Add(company.Company.AnIncident(correlationKey: "disk-full-7"));
        await context.SaveChangesAsync();

        // Walk the full lifecycle so the first incident leaves the filtered index.
        var now = DateTimeOffset.UtcNow;
        var first = await context.Incidents.SingleAsync();
        first.Acknowledge(now);
        first.StartInvestigating(now);
        first.Mitigate(now);
        first.Resolve(now);
        first.Close(now);
        await context.SaveChangesAsync();

        context.Incidents.Add(company.Company.AnIncident(correlationKey: "disk-full-7"));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Incidents.CountAsync());
    }

    [Fact]
    public async Task MultipleUncorrelatedIncidents_CanCoexist()
    {
        // SQL Server treats NULLs as equal inside a unique index, so rows with no
        // correlation key must be excluded by the filter or they would collide with each
        // other. This is the regression guard for that IS NOT NULL clause.
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        context.Incidents.Add(company.Company.AnIncident("Manual one", correlationKey: null));
        context.Incidents.Add(company.Company.AnIncident("Manual two", correlationKey: null));
        context.Incidents.Add(company.Company.AnIncident("Manual three", correlationKey: null));
        await context.SaveChangesAsync();

        Assert.Equal(3, await context.Incidents.CountAsync());
    }

    [Fact]
    public async Task SameCorrelationKey_InDifferentTenants_IsIndependent()
    {
        await using var first = await fixture.CompanyAsync("First");
        await using var second = await fixture.CompanyAsync("Second");

        first.Context.Incidents.Add(first.Company.AnIncident(correlationKey: "shared-key"));
        await first.Context.SaveChangesAsync();

        // The unique index leads with TenantId, so a different tenant may reuse the key.
        second.Context.Incidents.Add(second.Company.AnIncident(correlationKey: "shared-key"));
        await second.Context.SaveChangesAsync();

        Assert.Single(await first.Context.Incidents.ToListAsync());
        Assert.Single(await second.Context.Incidents.ToListAsync());
    }
}

/// <summary>
/// Covers the row-version column on Incident (ERD section 1) and the cascade rules from
/// ERD section 4. Both only exist at the database level.
/// </summary>
public sealed class ConcurrencyAndCascadeTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task ConcurrentEdits_LeaveOnlyOneWinner()
    {
        await using var company = await fixture.CompanyAsync();

        company.Context.Incidents.Add(company.Company.AnIncident());
        await company.Context.SaveChangesAsync();

        // Two requests read the same incident before either writes.
        await using var first = fixture.CreateContext(company.Tenants);
        await using var second = fixture.CreateContext(company.Tenants);

        var firstCopy = await first.Incidents.SingleAsync();
        var secondCopy = await second.Incidents.SingleAsync();

        firstCopy.Acknowledge(DateTimeOffset.UtcNow);
        await first.SaveChangesAsync();

        secondCopy.Assign(company.Company.User.Id);

        // The row version moved under the second reader, so its write must be refused
        // rather than silently clobbering the first.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAnIncident_RemovesItsCommentsAndEvents()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        var incident = company.Company.AnIncident();
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();

        context.IncidentComments.Add(IncidentComment.Create(
            company.TenantId, incident.Id, company.Company.User.Id, "looking into it", DateTimeOffset.UtcNow));

        context.IncidentEvents.Add(IncidentEvent.Create(
            company.TenantId, incident.Id, IncidentEventType.Acknowledged, DateTimeOffset.UtcNow));

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.IncidentComments.CountAsync());
        Assert.Equal(1, await context.IncidentEvents.CountAsync());

        context.Incidents.Remove(incident);
        await context.SaveChangesAsync();

        Assert.Equal(0, await context.Incidents.CountAsync());
        Assert.Equal(0, await context.IncidentComments.CountAsync());
        Assert.Equal(0, await context.IncidentEvents.CountAsync());
    }
}
