using System.Net;
using System.Net.Http.Json;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.Incidents.Commands.AssignIncident;
using OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the notification fan-out (plan, UC-13 and FR-16): assignment notifies the assignee,
/// resolving notifies the reporter, channel preferences decide what actually gets queued, and
/// the two endpoints — the personal inbox and the preference save — hold their contracts.
/// </summary>
public sealed class NotificationsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Assigning_An_Incident_Notifies_The_Assignee_On_Every_Channel()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "assignee@acme.test");

        var incident = company.Company.AnIncident();
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        var notifier = new RecordingIncidentNotifier();

        var handler = new AssignIncidentCommandHandler(
            new IncidentRepository(company.Context, notifier),
            new UserRepository(company.Context),
            new NoopAuditLogWriter(),
            new NotificationService(company.Context, company.Tenants, notifier));

        await handler.Handle(
            new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id),
            CancellationToken.None);

        var queued = company.Context.Notifications
            .Where(n => n.UserId == assignee.Id)
            .ToList();

        // One row per channel: the in-app one is delivered on write, the email one still
        // waits for a mailer that does not exist yet.
        Assert.Equal(2, queued.Count);
        Assert.All(queued, n =>
        {
            Assert.Equal(NotificationType.IncidentAssigned, n.Type);
            Assert.Equal(incident.Id, n.IncidentId);
        });

        var inApp = Assert.Single(queued, n => n.Channel == NotificationChannel.InApp);
        Assert.Equal(NotificationStatus.Sent, inApp.Status);
        Assert.NotNull(inApp.SentAt);

        var email = Assert.Single(queued, n => n.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationStatus.Pending, email.Status);
        Assert.Null(email.SentAt);

        // The realtime channel carries only what was actually delivered: the in-app copy is
        // pushed to the assignee, the mail that nobody has sent yet is not. The incident
        // change rides along so any other watcher can refresh without being told what changed.
        var push = Assert.Single(notifier.Notifications);
        Assert.Equal(assignee.Id, push.UserId);
        Assert.Equal(incident.Id, push.Notification.IncidentId);
        Assert.Equal(NotificationChannel.InApp.ToString(), push.Notification.Channel);

        Assert.Contains(notifier.IncidentUpdates, update => update.IncidentId == incident.Id);
    }

    [Fact]
    public async Task Re_Assigning_To_The_Same_Person_Queues_Nothing_New()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "reassign@acme.test");

        var incident = company.Company.AnIncident();
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        var handler = new AssignIncidentCommandHandler(
            new IncidentRepository(company.Context, new RecordingIncidentNotifier()),
            new UserRepository(company.Context),
            new NoopAuditLogWriter(),
            new NotificationService(company.Context, company.Tenants, new RecordingIncidentNotifier()));

        var command = new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        // Assigning the same person again changes nothing, so it must not nag them again —
        // the same "only real changes leave a trace" rule the audit trail follows.
        Assert.Equal(
            2,
            company.Context.Notifications.Count(n => n.UserId == assignee.Id));
    }

    [Fact]
    public async Task Resolving_An_Incident_Notifies_The_Reporter()
    {
        using var company = await db.CompanyAsync();
        var reporterId = company.Company.User.Id;

        var incident = company.Company.AnIncident();
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        var repository = new IncidentRepository(company.Context, new RecordingIncidentNotifier());
        var audit = new NoopAuditLogWriter();
        var notifications = new NotificationService(
            company.Context,
            company.Tenants,
            new RecordingIncidentNotifier());

        // The state machine only allows Investigating -> Resolved, so the incident is walked
        // there first; the intermediate hops deliberately queue nothing.
        await new AcknowledgeIncidentCommandHandler(repository, audit, notifications)
            .Handle(new AcknowledgeIncidentCommand(incident.Id, reporterId), CancellationToken.None);

        await new StartInvestigatingIncidentCommandHandler(repository, audit, notifications)
            .Handle(new StartInvestigatingIncidentCommand(incident.Id, reporterId), CancellationToken.None);

        Assert.Empty(company.Context.Notifications.Where(n => n.UserId == reporterId));

        await new ResolveIncidentCommandHandler(repository, audit, notifications)
            .Handle(new ResolveIncidentCommand(incident.Id, reporterId), CancellationToken.None);

        var queued = company.Context.Notifications
            .Where(n => n.UserId == reporterId)
            .ToList();

        Assert.Equal(2, queued.Count);
        Assert.All(queued, n => Assert.Equal(NotificationType.IncidentResolved, n.Type));
    }

    [Fact]
    public async Task A_Disabled_Channel_Suppresses_The_Notification()
    {
        using var company = await db.CompanyAsync();
        var assignee = await AddUserAsync(company, "muted@acme.test");

        // The incident is High severity, and this user has opted out of in-app alerts for it.
        var optOut = NotificationPreference.Create(
            company.TenantId,
            assignee.Id,
            NotificationChannel.InApp,
            Severity.High);

        optOut.Disable();
        await company.Context.NotificationPreferences.AddAsync(optOut);
        await company.Context.SaveChangesAsync();

        var incident = company.Company.AnIncident();
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        var notifier = new RecordingIncidentNotifier();

        var handler = new AssignIncidentCommandHandler(
            new IncidentRepository(company.Context, notifier),
            new UserRepository(company.Context),
            new NoopAuditLogWriter(),
            new NotificationService(company.Context, company.Tenants, notifier));

        await handler.Handle(
            new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id),
            CancellationToken.None);

        // The opt-out costs the user the in-app row, not the whole notification: the email
        // channel they never disabled still carries it.
        var queued = Assert.Single(
            company.Context.Notifications.Where(n => n.UserId == assignee.Id).ToList());

        Assert.Equal(NotificationChannel.Email, queued.Channel);
        Assert.Equal(NotificationStatus.Pending, queued.Status);

        // Nothing was delivered in-app, so nothing is pushed to them either: the realtime
        // channel follows the same preference gate as the inbox.
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task The_Inbox_Only_Ever_Returns_The_Caller_s_Own_Notifications()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "notif-inbox");

        var serviceId = await CreateServiceAsync(owner, "Notif Inbox");
        var incidentId = await CreateIncidentAsync(owner, serviceId);
        var (engineer, engineerId) = await ApiAuth.MemberClientAsync(factory, owner, "notif-inbox", "Engineer");

        var assign = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/assign",
            new { assigneeId = engineerId });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var inbox = await engineer.GetFromJsonAsync<PagedResponse<NotificationDto>>(
            "/api/v1/notifications",
            ApiAuth.Json);

        Assert.NotNull(inbox);

        // One row per channel for the same event: the delivered in-app copy and the email
        // that is still waiting for a mailer.
        Assert.Equal(2, inbox!.TotalCount);
        Assert.All(inbox.Data!, n =>
        {
            Assert.Equal(NotificationType.IncidentAssigned.ToString(), n.Type);
            Assert.Equal(incidentId, n.IncidentId);
        });

        Assert.Contains(inbox.Data!, n => n.Channel == "InApp" && n.Status == "Sent");
        Assert.Contains(inbox.Data!, n => n.Channel == "Email" && n.Status == "Pending");

        // The owner who did the assigning has nothing in their own inbox, and a company that
        // took no part in any of this sees nothing either.
        var ownersInbox = await owner.GetFromJsonAsync<PagedResponse<NotificationDto>>(
            "/api/v1/notifications",
            ApiAuth.Json);

        Assert.Empty(ownersInbox!.Data!);

        var (stranger, _) = await ApiAuth.OwnerClientAsync(factory, "notif-stranger");
        var strangersInbox = await stranger.GetFromJsonAsync<PagedResponse<NotificationDto>>(
            "/api/v1/notifications",
            ApiAuth.Json);

        Assert.Empty(strangersInbox!.Data!);
    }

    [Fact]
    public async Task The_Inbox_Filters_By_Whether_Notifications_Are_Read()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "notif-read");

        var serviceId = await CreateServiceAsync(owner, "Notif Read");
        var incidentId = await CreateIncidentAsync(owner, serviceId);
        var (engineer, engineerId) = await ApiAuth.MemberClientAsync(factory, owner, "notif-read", "Engineer");

        var assign = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/assign",
            new { assigneeId = engineerId });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        // Nothing has been marked read yet, so the two filters must split the same pair.
        var unread = await engineer.GetFromJsonAsync<PagedResponse<NotificationDto>>(
            "/api/v1/notifications?isRead=false",
            ApiAuth.Json);

        Assert.Equal(2, unread!.TotalCount);

        var read = await engineer.GetFromJsonAsync<PagedResponse<NotificationDto>>(
            "/api/v1/notifications?isRead=true",
            ApiAuth.Json);

        Assert.Empty(read!.Data!);
    }

    [Fact]
    public async Task Saving_Preferences_Returns_The_Full_Effective_Set()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "notif-prefs");

        var put = await owner.PutAsJsonAsync("/api/v1/notification-preferences", new
        {
            preferences = new[]
            {
                new { severity = "High", channel = "InApp", enabled = false },
            },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await put.Content.ReadFromJsonAsync<Response<IReadOnlyList<NotificationPreferenceDto>>>(
            ApiAuth.Json);

        Assert.NotNull(body?.Data);

        // Every severity and channel comes back, in severity order, with the one opt-out
        // applied and the rest at their enabled default.
        Assert.Equal(8, body!.Data.Count);
        Assert.Equal(
            new[] { "Critical", "Critical", "High", "High", "Medium", "Medium", "Low", "Low" },
            body.Data.Select(p => p.Severity).ToArray());

        Assert.False(body.Data.Single(p => p.Severity == "High" && p.Channel == "InApp").Enabled);
        Assert.True(body.Data.Single(p => p.Severity == "High" && p.Channel == "Email").Enabled);
        Assert.All(
            body.Data.Where(p => p.Severity != "High"),
            p => Assert.True(p.Enabled));
    }

    [Fact]
    public async Task Saving_Preferences_Again_Overrides_The_Same_Combination()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "notif-prefs2");

        var disable = await owner.PutAsJsonAsync("/api/v1/notification-preferences", new
        {
            preferences = new[]
            {
                new { severity = "Critical", channel = "Email", enabled = false },
            },
        });

        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        // Saving the same combination again updates it in place rather than accumulating rows.
        var enable = await owner.PutAsJsonAsync("/api/v1/notification-preferences", new
        {
            preferences = new[]
            {
                new { severity = "Critical", channel = "Email", enabled = true },
            },
        });

        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

        var body = await enable.Content.ReadFromJsonAsync<Response<IReadOnlyList<NotificationPreferenceDto>>>(
            ApiAuth.Json);

        Assert.NotNull(body?.Data);
        Assert.True(body!.Data.Single(p => p.Severity == "Critical" && p.Channel == "Email").Enabled);
    }

    [Fact]
    public async Task Saving_Preferences_Rejects_Malformed_Input()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "notif-prefs-bad");

        var malformed = new (object Body, string Because)[]
        {
            (new { preferences = Array.Empty<object>() }, "an empty batch"),
            (new { preferences = new[] { new { severity = "Urgent", channel = "InApp", enabled = true } } }, "an unknown severity"),
            (new { preferences = new[] { new { severity = "High", channel = "CarrierPigeon", enabled = true } } }, "an unknown channel"),
            (new
            {
                preferences = new[]
                {
                    new { severity = "High", channel = "InApp", enabled = false },
                    new { severity = "High", channel = "InApp", enabled = true },
                },
            }, "a duplicated pair"),
        };

        foreach (var (body, because) in malformed)
        {
            var put = await owner.PutAsJsonAsync("/api/v1/notification-preferences", body);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);

            var payload = await put.Content.ReadFromJsonAsync<Response<IReadOnlyList<NotificationPreferenceDto>>>(
                ApiAuth.Json);

            Assert.False(payload!.Succeeded, $"Expected a rejection for {because}.");
            Assert.Equal(BusinessCode.ValidationError, payload.Code);
        }
    }

    private static async Task<User> AddUserAsync(CompanyScope company, string email)
    {
        var user = User.Create(company.TenantId, email, "Test Recipient");
        await company.Context.Users.AddAsync(user);
        await company.Context.SaveChangesAsync();
        return user;
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }

    private static async Task<Guid> CreateIncidentAsync(HttpClient owner, Guid serviceId)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/incidents", new
        {
            serviceId,
            title = "Checkout is failing",
            description = "5xx on the payment callback",
            severity = "High",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }

    /// <summary>Drops audit entries on the floor, for tests driving handlers directly.</summary>
    private sealed class NoopAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
