using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the tenant isolation rule from ERD section 1. A data leak across tenants is the
/// worst failure this system can have, so the tests assert both directions: a tenant sees
/// its own rows and never another tenant's.
/// </summary>
public sealed class TenantIsolationTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task TenantScopedReads_ReturnOnlyTheCurrentTenantsRows()
    {
        await using var alpha = await fixture.CompanyAsync("Alpha");
        await using var beta = await fixture.CompanyAsync("Beta");

        await alpha.Context.Incidents.AddAsync(alpha.Company.AnIncident("Alpha incident"));
        await alpha.Context.SaveChangesAsync();

        await beta.Context.Incidents.AddAsync(beta.Company.AnIncident("Beta incident"));
        await beta.Context.SaveChangesAsync();

        var alphaTitles = await alpha.Context.Incidents.Select(i => i.Title).ToListAsync();
        var betaTitles = await beta.Context.Incidents.Select(i => i.Title).ToListAsync();

        Assert.Equal(["Alpha incident"], alphaTitles);
        Assert.Equal(["Beta incident"], betaTitles);
    }

    [Fact]
    public async Task TenantScopedReads_ReturnNothingWhenNoTenantIsResolved()
    {
        await using var company = await fixture.CompanyAsync();
        await company.Context.Incidents.AddAsync(company.Company.AnIncident("Confidential"));
        await company.Context.SaveChangesAsync();

        // A context with no tenant established must fail closed. Returning every tenant's
        // rows would be a silent data breach, so the filter has to match nothing instead.
        var unresolved = new TenantContext();
        await using var context = fixture.CreateContext(unresolved);

        Assert.False(unresolved.IsResolved);
        Assert.Equal(0, await context.Incidents.CountAsync());
    }

    [Fact]
    public async Task CrossTenantReads_RequireDeliberatelyIgnoringTheFilter()
    {
        await using var first = await fixture.CompanyAsync("First");
        await using var second = await fixture.CompanyAsync("Second");

        await first.Context.Incidents.AddAsync(first.Company.AnIncident("First incident"));
        await second.Context.Incidents.AddAsync(second.Company.AnIncident("Second incident"));
        await first.Context.SaveChangesAsync();
        await second.Context.SaveChangesAsync();

        Assert.Equal(1, await first.Context.Incidents.CountAsync());

        // Opting out is explicit at the call site, so it is greppable in review.
        var all = await first.Context.Incidents.IgnoreQueryFilters().CountAsync();
        Assert.Equal(2, all);
    }

    [Fact]
    public async Task SoftDeletedRows_AreHiddenFromOrdinaryReads()
    {
        await using var company = await fixture.CompanyAsync();
        var incident = company.Company.AnIncident("Recurring failure");
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        incident.SoftDelete();
        await company.Context.SaveChangesAsync();

        // A fresh context proves the row is filtered at query time, not just in this tracker.
        await using var verify = fixture.CreateContext(company.Tenants);
        Assert.Equal(0, await verify.Incidents.CountAsync());
        Assert.Equal(1, await verify.Incidents.IgnoreQueryFilters().CountAsync());
    }
}

/// <summary>
/// Covers the append-only guarantee for the audit trail and the incident timeline (ERD
/// section 1): once written, never updated and never deleted.
/// </summary>
public sealed class AppendOnlyTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task AuditLog_CannotBeUpdated()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        var log = AuditLog.Create(
            company.TenantId,
            "IncidentResolved",
            nameof(Incident),
            Guid.NewGuid().ToString(),
            company.Company.User.Id);

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Mutate a private setter through the change tracker, exactly as a bug or a
        // well-meaning refactor would.
        context.Entry(log).Property(a => a.Action).CurrentValue = "Tampered";

        var error = await Assert.ThrowsAsync<AppendOnlyEntityModifiedException>(
            () => context.SaveChangesAsync());

        Assert.Contains(nameof(AuditLog), error.Message);
    }

    [Fact]
    public async Task AuditLog_CannotBeDeleted()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        var log = AuditLog.Create(
            company.TenantId,
            "RoleChanged",
            nameof(Role),
            Guid.NewGuid().ToString(),
            company.Company.User.Id);

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        context.AuditLogs.Remove(log);

        await Assert.ThrowsAsync<AppendOnlyEntityModifiedException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task IncidentEvent_CannotBeDeleted()
    {
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        var incident = company.Company.AnIncident();
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();

        var evt = IncidentEvent.Create(
            company.TenantId,
            incident.Id,
            IncidentEventType.Acknowledged,
            DateTimeOffset.UtcNow);

        context.IncidentEvents.Add(evt);
        await context.SaveChangesAsync();

        context.IncidentEvents.Remove(evt);

        await Assert.ThrowsAsync<AppendOnlyEntityModifiedException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task OrdinaryEntities_AreStillUpdatableAndDeletable()
    {
        // The guard must not be so broad that it blocks normal editing.
        await using var company = await fixture.CompanyAsync();
        var context = company.Context;

        var incident = company.Company.AnIncident("Original title");
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();

        incident.Assign(company.Company.User.Id);
        await context.SaveChangesAsync();

        Assert.Equal(company.Company.User.Id, (await context.Incidents.SingleAsync()).AssignedTo);

        context.Incidents.Remove(incident);
        await context.SaveChangesAsync();

        Assert.Empty(await context.Incidents.ToListAsync());
    }
}
