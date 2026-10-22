using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Features.SlaViolations.Commands.DetectSlaViolations;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the escalation half of the SLA sweep (plan, UC-12 follow-up): a breach that has been
/// left standing also has to reach the people accountable for the service, not only the people
/// already looking at the incident.
/// </summary>
public sealed class SlaViolationEscalationTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResolutionWindow = TimeSpan.FromHours(2);

    [Fact]
    public async Task The_Owning_Teams_Lead_Is_Escalated_To_As_Well()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "escalation-assignee@acme.test");
        var lead = await AddUserAsync(company, "escalation-lead@acme.test");

        await OwnServiceAsync(company, lead.Id);

        var incident = await AddOverdueIncidentAsync(company, assignee.Id);

        var sweep = BuildSweep(company);
        await sweep.Handler.Handle(new DetectSlaViolationsCommand(DateTimeOffset.UtcNow), default);

        var notifications = await company.Context.Notifications
            .Where(n => n.IncidentId == incident.Id)
            .ToListAsync();

        // The two people already looking at the incident get the ordinary breach notice, and
        // the lead is escalated to on top of that - on both channels, for both missed
        // deadlines, so six rows in all.
        var escalated = notifications
            .Where(n => n.Type == NotificationType.SlaViolationEscalated)
            .ToList();

        Assert.Equal(4, escalated.Count);
        Assert.All(escalated, n => Assert.Equal(lead.Id, n.UserId));
        Assert.All(
            escalated,
            n => Assert.StartsWith("SLA escalation", n.Title, StringComparison.Ordinal));

        Assert.Equal(2, notifications.Count(n => n.Type == NotificationType.SlaViolation
            && n.UserId == assignee.Id
            && n.Channel == NotificationChannel.Email));

        // The reporter is told about the breach but is not escalated to: they are already a
        // direct recipient, and the escalation is aimed at whoever owns the service.
        Assert.DoesNotContain(
            notifications,
            n => n.Type == NotificationType.SlaViolationEscalated
                && n.UserId == company.Company.User.Id);
    }

    [Fact]
    public async Task An_Unowned_Service_Escalates_To_Nobody()
    {
        using var company = await db.CompanyAsync();

        // The service the seed creates has no owner team, so there are no leads to reach.
        var incident = await AddOverdueIncidentAsync(company, assignee: null);

        var sweep = BuildSweep(company);
        await sweep.Handler.Handle(new DetectSlaViolationsCommand(DateTimeOffset.UtcNow), default);

        var notifications = await company.Context.Notifications
            .Where(n => n.IncidentId == incident.Id)
            .ToListAsync();

        // The sweep still records the breach and still tells the reporter; it simply has no one
        // to escalate to, which must not be an error. One recipient, two breaches, two channels.
        Assert.Equal(4, notifications.Count);
        Assert.All(
            notifications,
            n => Assert.Equal(NotificationType.SlaViolation, n.Type));
    }

    [Fact]
    public async Task A_Lead_Who_Is_Already_The_Assignee_Is_Not_Told_Twice()
    {
        using var company = await db.CompanyAsync();
        var lead = await AddUserAsync(company, "escalation-self-lead@acme.test");

        await OwnServiceAsync(company, lead.Id);

        // The person who owns the service is also the person carrying the incident.
        var incident = await AddOverdueIncidentAsync(company, lead.Id);

        var sweep = BuildSweep(company);
        await sweep.Handler.Handle(new DetectSlaViolationsCommand(DateTimeOffset.UtcNow), default);

        var notifications = await company.Context.Notifications
            .Where(n => n.IncidentId == incident.Id)
            .ToListAsync();

        // Two recipients (the reporter and the lead-as-assignee), two breaches, two channels -
        // and no escalation copy on top, because the lead already received the breach notice.
        Assert.Equal(8, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(NotificationType.SlaViolation, n.Type));
    }

    /// <summary>Attaches an owner team to the seeded service and puts <paramref name="leadId"/> in it as a lead.</summary>
    private static async Task OwnServiceAsync(CompanyScope company, Guid leadId)
    {
        var team = Team.Create(company.TenantId, "Payments on-call");
        company.Context.Teams.Add(team);

        var service = await company.Context.Services.SingleAsync(s => s.Id == company.Company.Service.Id);
        service.AssignOwnerTeam(team.Id);

        await company.Context.SaveChangesAsync();

        company.Context.TeamMembers.Add(
            TeamMember.Create(team.Id, leadId, company.TenantId, TeamRole.Lead));

        await company.Context.SaveChangesAsync();
    }

    private static async Task<Incident> AddOverdueIncidentAsync(CompanyScope company, Guid? assignee)
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
            DateTimeOffset.UtcNow.AddHours(-3));

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
        var user = User.Create(company.TenantId, email, "Escalation Test User");
        await company.Context.Users.AddAsync(user);
        await company.Context.SaveChangesAsync();

        return user;
    }

    private sealed record Sweep(
        DetectSlaViolationsCommandHandler Handler,
        RecordingIncidentNotifier Notifier);
}
