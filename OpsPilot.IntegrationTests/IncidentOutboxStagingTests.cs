using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Incidents.Commands;
using OpsPilot.Application.Features.Incidents.Commands.AddIncidentComment;
using OpsPilot.Application.Features.Incidents.Commands.AssignIncident;
using OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Security;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the incident paths that used to be silent: a manual create, a state change, a comment
/// and an assignment all leave an outbox message and one webhook call per active endpoint.
/// </summary>
/// <remarks>
/// Assertions are row-scoped to this test's own endpoint because the fixture shares one database
/// and webhook staging walks every company in it.
/// </remarks>
public sealed class IncidentOutboxStagingTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private const string Url = "https://hooks.example.test/incidents";

    [Fact]
    public async Task Creating_An_Incident_Stages_A_Created_Entry_And_Announcement()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company);

        var handler = new CreateIncidentCommandHandler(
            RepositoryFor(company),
            new ServiceRepository(company.Context),
            new SlaPolicyRepository(company.Context));

        var incidentId = await handler.Handle(
            new CreateIncidentCommand(
                company.TenantId,
                company.Company.Service.Id,
                "API is down",
                "5xx on /v1/events",
                Severity.High,
                company.Company.User.Id),
            CancellationToken.None);

        var entry = Assert.Single(
            await company.Context.IncidentEvents
                .Where(e => e.IncidentId == incidentId)
                .ToListAsync());

        Assert.Equal(IncidentEventType.Created, entry.Type);

        var message = Assert.Single(await company.Context.OutboxMessages.ToListAsync());

        Assert.Equal(IntegrationEventTypes.IncidentCreated, message.Type);

        var delivery = await SingleDeliveryAsync(company, endpoint);

        Assert.Equal(IntegrationEventTypes.IncidentCreated, delivery.EventType);
        Assert.Contains("\"eventType\":\"Created\"", delivery.Payload, StringComparison.Ordinal);
        Assert.Contains(entry.Id.ToString(), delivery.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_State_Change_Stages_An_EventAdded_Announcement()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company);

        var incident = await OpenIncidentAsync(company);

        await new AcknowledgeIncidentCommandHandler(
                RepositoryFor(company),
                new NoopAuditLogWriter(),
                new NoopNotificationService())
            .Handle(new AcknowledgeIncidentCommand(incident.Id, company.Company.User.Id), CancellationToken.None);

        var message = Assert.Single(await company.Context.OutboxMessages.ToListAsync());

        Assert.Equal(IntegrationEventTypes.IncidentEventAdded, message.Type);

        var delivery = await SingleDeliveryAsync(company, endpoint);

        Assert.Equal(IntegrationEventTypes.IncidentEventAdded, delivery.EventType);
        Assert.Contains("\"eventType\":\"Acknowledged\"", delivery.Payload, StringComparison.Ordinal);

        var entry = Assert.Single(
            await company.Context.IncidentEvents
                .Where(e => e.IncidentId == incident.Id)
                .ToListAsync());

        Assert.Equal(IncidentEventType.Acknowledged, entry.Type);
    }

    [Fact]
    public async Task A_Comment_Stages_An_EventAdded_Announcement()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company);

        var incident = await OpenIncidentAsync(company);

        await new AddIncidentCommentCommandHandler(RepositoryFor(company))
            .Handle(
                new AddIncidentCommentCommand(incident.Id, company.Company.User.Id, "Rolled back the deploy."),
                CancellationToken.None);

        Assert.Single(
            await company.Context.IncidentComments
                .Where(c => c.IncidentId == incident.Id)
                .ToListAsync());

        var delivery = await SingleDeliveryAsync(company, endpoint);

        Assert.Equal(IntegrationEventTypes.IncidentEventAdded, delivery.EventType);
        Assert.Contains("\"eventType\":\"CommentAdded\"", delivery.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assigning_To_Someone_Else_Stages_An_Assigned_Announcement()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company);

        var incident = await OpenIncidentAsync(company);
        var assignee = await AddUserAsync(company);

        await new AssignIncidentCommandHandler(
                RepositoryFor(company),
                new UserRepository(company.Context),
                new NoopAuditLogWriter(),
                new NoopNotificationService())
            .Handle(
                new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id),
                CancellationToken.None);

        var delivery = await SingleDeliveryAsync(company, endpoint);

        Assert.Equal(IntegrationEventTypes.IncidentEventAdded, delivery.EventType);
        Assert.Contains("\"eventType\":\"Assigned\"", delivery.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reassigning_To_The_Same_Person_Records_The_Entry_But_Announces_Nothing()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company);

        var incident = await OpenIncidentAsync(company);
        var assignee = await AddUserAsync(company);

        var repository = RepositoryFor(company);
        var assignments = new AssignIncidentCommandHandler(
            repository,
            new UserRepository(company.Context),
            new NoopAuditLogWriter(),
            new NoopNotificationService());

        await assignments.Handle(
            new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id),
            CancellationToken.None);

        var afterFirst = await company.Context.OutboxMessages.CountAsync();

        // The second call names the same assignee, so it changes nothing worth telling anyone
        // about -- but the call still happened and the timeline says so.
        await assignments.Handle(
            new AssignIncidentCommand(incident.Id, assignee.Id, company.Company.User.Id),
            CancellationToken.None);

        Assert.Equal(afterFirst, await company.Context.OutboxMessages.CountAsync());

        var entries = await company.Context.IncidentEvents
            .Where(e => e.IncidentId == incident.Id && e.Type == IncidentEventType.Assigned)
            .ToListAsync();

        Assert.Equal(2, entries.Count);

        var deliveries = await company.Context.WebhookDeliveries
            .Where(d => d.EndpointId == endpoint.Id)
            .ToListAsync();

        Assert.Single(deliveries);
    }

    private IncidentRepository RepositoryFor(CompanyScope company) =>
        new(company.Context, new RecordingIncidentNotifier());

    private static async Task<Incident> OpenIncidentAsync(CompanyScope company)
    {
        var incident = company.Company.AnIncident();

        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        return incident;
    }

    private static async Task<User> AddUserAsync(CompanyScope company)
    {
        var user = User.Create(
            company.TenantId,
            $"assignee-{Guid.NewGuid():N}@acme.test"[..30],
            "Assignee");

        await company.Context.Users.AddAsync(user);
        await company.Context.SaveChangesAsync();

        return user;
    }

    private static async Task<WebhookEndpoint> RegisterAsync(CompanyScope company)
    {
        var endpoint = WebhookEndpoint.Create(
            company.TenantId,
            Url,
            new string('a', 64),
            Protector().Protect("whsec_test_secret"));

        await company.Context.WebhookEndpoints.AddAsync(endpoint);
        await company.Context.SaveChangesAsync();

        return endpoint;
    }

    private static async Task<WebhookDelivery> SingleDeliveryAsync(
        CompanyScope company,
        WebhookEndpoint endpoint)
    {
        var deliveries = await company.Context.WebhookDeliveries
            .Where(d => d.EndpointId == endpoint.Id)
            .ToListAsync();

        return Assert.Single(deliveries);
    }

    private static AesGcmApiKeySecretProtector Protector() =>
        new(Options.Create(new ApiKeyEncryptionOptions
        {
            Key = "KioqKioqKioqKioqKioqKioqKioqKioqKioqKioqKio="
        }));

    /// <summary>Drops audit entries on the floor, for tests driving handlers directly.</summary>
    private sealed class NoopAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>Queues nothing, for tests driving handlers directly.</summary>
    private sealed class NoopNotificationService : INotificationService
    {
        public Task NotifyAsync(
            IReadOnlyCollection<Guid> userIds,
            NotificationType type,
            Severity severity,
            string title,
            string body,
            Guid? incidentId = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
