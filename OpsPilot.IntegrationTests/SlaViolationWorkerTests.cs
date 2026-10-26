using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.BackgroundJobs;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the scheduled side of the SLA sweep (plan, UC-12): that it is hosted when switched
/// on, and that one pass visits every company without letting them see each other's work.
/// </summary>
public sealed class SlaViolationWorkerTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task The_Sweep_Is_Hosted_When_It_Is_Switched_On()
    {
        await using var factory = new IntegrationApiFactory(db, slaWorkerEnabled: true);

        var hosted = factory.Services
            .GetServices<IHostedService>()
            .OfType<SlaViolationDetectionWorker>();

        Assert.Single(hosted);
    }

    [Fact]
    public async Task One_Pass_Sweeps_Every_Company_And_Only_Their_Own_Incidents()
    {
        await using var factory = new IntegrationApiFactory(db, slaWorkerEnabled: true);

        using var acme = await db.CompanyAsync("Acme");
        using var globex = await db.CompanyAsync("Globex");

        var createdAt = DateTimeOffset.UtcNow.AddHours(-3);

        var acmeIncident = await AddOverdueIncidentAsync(acme, createdAt);
        var globexIncident = await AddOverdueIncidentAsync(globex, createdAt);

        var worker = new SlaViolationDetectionWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedTime(createdAt.AddHours(3)),
            Options.Create(new SlaViolationWorkerOptions()),
            NullLogger<SlaViolationDetectionWorker>.Instance);

        var detected = await worker.SweepOnceAsync();

        // Response and resolution, for both companies: the two are simply added up, while each
        // company's rows stay behind its own query filter.
        Assert.Equal(4, detected);

        var acmeBreaches = await acme.Context.SlaViolations.ToListAsync();

        Assert.Equal(2, acmeBreaches.Count);
        Assert.All(acmeBreaches, breach =>
        {
            Assert.Equal(acme.TenantId, breach.TenantId);
            Assert.Equal(acmeIncident.Id, breach.IncidentId);
        });

        var globexBreaches = await globex.Context.SlaViolations.ToListAsync();

        Assert.Equal(2, globexBreaches.Count);
        Assert.All(globexBreaches, breach =>
        {
            Assert.Equal(globex.TenantId, breach.TenantId);
            Assert.Equal(globexIncident.Id, breach.IncidentId);
        });
    }

    /// <summary>Persists an incident that is past both of its deadlines.</summary>
    private static async Task<Incident> AddOverdueIncidentAsync(
        CompanyScope company,
        DateTimeOffset createdAt)
    {
        var policy = SlaPolicy.Create(company.TenantId, Severity.High, 30, 120);
        await company.Context.SlaPolicies.AddAsync(policy);

        var incident = Incident.Create(
            company.TenantId,
            company.Company.Service.Id,
            "Checkout latency spike",
            "p99 latency above threshold",
            Severity.High,
            company.Company.User.Id,
            createdAt);

        incident.ApplySlaPolicy(policy.Id, policy.ResponseWindow, policy.ResolutionWindow);

        company.Context.Incidents.Add(incident);
        await company.Context.SaveChangesAsync();

        return incident;
    }

    /// <summary>Pins "now" so a pass is judged at a moment the test chose.</summary>
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
