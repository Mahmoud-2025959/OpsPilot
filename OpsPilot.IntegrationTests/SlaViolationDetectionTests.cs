using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Features.SlaViolations.Commands.DetectSlaViolations;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the SLA breach sweep (plan, UC-12): which incidents count as late, what recording a
/// breach leaves behind, and the idempotency that lets the job re-run without telling the same
/// person twice.
/// </summary>
/// <remarks>
/// The handler is driven directly with the instant it judges against, rather than waiting for a
/// deadline to pass — which is the reason the command carries <c>Now</c> at all.
/// </remarks>
public sealed class SlaViolationDetectionTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResolutionWindow = TimeSpan.FromHours(2);

    [Fact]
    public async Task An_Incident_Past_Both_Deadlines_Is_Reported_Once()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "sla-both@acme.test");

        var incident = await AddIncidentOnSlaAsync(
            company,
            assignee.Id,
            DateTimeOffset.UtcNow.AddHours(-3));

        var now = DateTimeOffset.UtcNow;
        var sweep = BuildSweep(company);

        var detected = await sweep.Handler.Handle(new DetectSlaViolationsCommand(now), default);

        Assert.Equal(2, detected);

        var breaches = await company.Context.SlaViolations
            .Where(v => v.IncidentId == incident.Id)
            .ToListAsync();

        Assert.Equal(2, breaches.Count);
        Assert.All(breaches, breach =>
        {
            Assert.Equal(company.TenantId, breach.TenantId);
            Assert.Equal(now, breach.DetectedAt);
        });

        var response = Assert.Single(breaches, b => b.SlaType == SlaType.Response);
        Assert.Equal(incident.ResponseDueAt, response.DueAt);

        var resolution = Assert.Single(breaches, b => b.SlaType == SlaType.Resolution);
        Assert.Equal(incident.ResolutionDueAt, resolution.DueAt);

        // Each breach is also a line on the timeline, carrying the deadline it refers to so the
        // entry explains itself without a join back to the violation row.
        var timeline = await company.Context.IncidentEvents
            .Where(e => e.IncidentId == incident.Id)
            .ToListAsync();

        Assert.Equal(2, timeline.Count);
        Assert.All(timeline, e => Assert.Equal(IncidentEventType.SlaViolationDetected, e.Type));
        Assert.Single(timeline, e => e.Payload!.Contains("Response", StringComparison.Ordinal));
        Assert.Single(timeline, e => e.Payload!.Contains("Resolution", StringComparison.Ordinal));

        // The reporter and the assignee, on both channels, for both missed deadlines.
        var notifications = await company.Context.Notifications
            .Where(n => n.IncidentId == incident.Id)
            .ToListAsync();

        Assert.Equal(8, notifications.Count);
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.SlaViolation, n.Type);
            Assert.StartsWith("SLA missed", n.Title, StringComparison.Ordinal);
        });

        Assert.Equal(2, notifications.Select(n => n.UserId).Distinct().Count());

        // Only the in-app copies go out live; the email rows wait for a mailer that does not
        // exist yet.
        Assert.Equal(4, sweep.Notifier.Notifications.Count);
        Assert.All(
            sweep.Notifier.Notifications,
            push => Assert.Equal(incident.Id, push.Notification.IncidentId));
    }

    [Fact]
    public async Task A_Second_Sweep_Of_The_Same_Incident_Reports_Nothing()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "sla-repeat@acme.test");

        var incident = await AddIncidentOnSlaAsync(
            company,
            assignee.Id,
            DateTimeOffset.UtcNow.AddHours(-3));

        var now = DateTimeOffset.UtcNow;
        var sweep = BuildSweep(company);

        Assert.Equal(2, await sweep.Handler.Handle(new DetectSlaViolationsCommand(now), default));

        var filesAfterFirstPass = await company.Context.SlaViolations.CountAsync();
        var notificationsAfterFirstPass = await company.Context.Notifications.CountAsync();

        // Judged again a minute later: the breaches are already on record, so this pass finds
        // nothing to report. That is what makes a retry after a failure safe.
        var secondPass = await sweep.Handler.Handle(
            new DetectSlaViolationsCommand(now.AddMinutes(1)),
            default);

        Assert.Equal(0, secondPass);
        Assert.Equal(filesAfterFirstPass, await company.Context.SlaViolations.CountAsync());
        Assert.Equal(2, await company.Context.IncidentEvents.CountAsync());
        Assert.Equal(notificationsAfterFirstPass, await company.Context.Notifications.CountAsync());
    }

    [Fact]
    public async Task An_Incident_Still_Inside_Its_Windows_Is_Left_Alone()
    {
        using var company = await db.CompanyAsync();

        await AddIncidentOnSlaAsync(company, assignee: null, DateTimeOffset.UtcNow.AddMinutes(-10));

        var sweep = BuildSweep(company);

        var detected = await sweep.Handler.Handle(
            new DetectSlaViolationsCommand(DateTimeOffset.UtcNow),
            default);

        Assert.Equal(0, detected);
        Assert.Equal(0, await company.Context.SlaViolations.CountAsync());
        Assert.Equal(0, await company.Context.IncidentEvents.CountAsync());
        Assert.Equal(0, await company.Context.Notifications.CountAsync());
        Assert.Empty(sweep.Notifier.Notifications);
    }

    [Fact]
    public async Task An_Incident_Exactly_At_Its_Deadline_Is_Not_Late_Yet()
    {
        using var company = await db.CompanyAsync();

        var now = DateTimeOffset.UtcNow;

        // Created exactly one response window ago, so the response deadline is *this* instant
        // rather than a past one.
        await AddIncidentOnSlaAsync(company, assignee: null, now - ResponseWindow);

        var sweep = BuildSweep(company);

        Assert.Equal(0, await sweep.Handler.Handle(new DetectSlaViolationsCommand(now), default));
        Assert.Equal(0, await company.Context.SlaViolations.CountAsync());
    }

    [Fact]
    public async Task Acknowledging_In_Time_Leaves_Only_The_Resolution_Breach()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "sla-acknowledged@acme.test");

        var createdAt = DateTimeOffset.UtcNow.AddHours(-3);
        var incident = await AddIncidentOnSlaAsync(company, assignee.Id, createdAt);

        // Acknowledged well inside the response window, but never resolved.
        incident.Acknowledge(createdAt.AddMinutes(10));
        await company.Context.SaveChangesAsync();

        var sweep = BuildSweep(company);

        var detected = await sweep.Handler.Handle(
            new DetectSlaViolationsCommand(DateTimeOffset.UtcNow),
            default);

        Assert.Equal(1, detected);

        var breach = Assert.Single(await company.Context.SlaViolations.ToListAsync());
        Assert.Equal(SlaType.Resolution, breach.SlaType);

        // Two people, two channels, one breach.
        Assert.Equal(4, await company.Context.Notifications.CountAsync());
    }

    [Fact]
    public async Task The_Reporter_Is_Told_Once_When_They_Are_Also_The_Assignee()
    {
        using var company = await db.CompanyAsync();
        var reporter = company.Company.User;

        var incident = await AddIncidentOnSlaAsync(
            company,
            reporter.Id,
            DateTimeOffset.UtcNow.AddHours(-3));

        var sweep = BuildSweep(company);

        await sweep.Handler.Handle(new DetectSlaViolationsCommand(DateTimeOffset.UtcNow), default);

        var notifications = await company.Context.Notifications
            .Where(n => n.IncidentId == incident.Id)
            .ToListAsync();

        // Response and resolution each notify the same single person on both channels; naming
        // them twice would queue a duplicate per breach.
        Assert.Equal(4, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(reporter.Id, n.UserId));
    }

    /// <summary>
    /// Persists an incident with a policy applied and, optionally, an assignee — so both
    /// deadlines land where the test asked them to.
    /// </summary>
    private static async Task<Incident> AddIncidentOnSlaAsync(
        CompanyScope company,
        Guid? assignee,
        DateTimeOffset createdAt)
    {
        var policy = SlaPolicy.Create(
            company.TenantId,
            Severity.High,
            (int)ResponseWindow.TotalMinutes,
            (int)ResolutionWindow.TotalMinutes);

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

        if (assignee is { } id)
        {
            incident.Assign(id);
        }

        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        return incident;
    }

    private static Sweep BuildSweep(CompanyScope company)
    {
        var notifier = new RecordingIncidentNotifier();

        return new Sweep(
            new DetectSlaViolationsCommandHandler(
                new IncidentRepository(company.Context, notifier),
                new SlaViolationRepository(company.Context),
                new ServiceRepository(company.Context),
                new NotificationService(company.Context, company.Tenants, notifier)),
            notifier);
    }

    private static async Task<User> AddUserAsync(CompanyScope company, string email)
    {
        var user = User.Create(company.TenantId, email, "Test Assignee");
        await company.Context.Users.AddAsync(user);
        await company.Context.SaveChangesAsync();

        return user;
    }

    private sealed record Sweep(
        DetectSlaViolationsCommandHandler Handler,
        RecordingIncidentNotifier Notifier);
}
