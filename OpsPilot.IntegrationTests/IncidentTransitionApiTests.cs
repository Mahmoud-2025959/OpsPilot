using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Features.Incidents.Commands.TransitionIncident;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the incident state machine end to end: the endpoint has to answer with the incident
/// as it now stands, reject moves the domain forbids with a conflict rather than a validation
/// error, and carry the caller's free-text note onto the timeline.
/// </summary>
public sealed class IncidentTransitionApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Note = "Connection pool size increased";

    [Fact]
    public async Task Transition_Returns_The_Incident_In_Its_New_Status()
    {
        // Exercised through the handlers first: the endpoint's contract is the DTO, so the
        // status and the timestamp both have to come back from the same call that applied it.
        using var company = await db.CompanyAsync();

        var incident = company.Company.AnIncident();
        await company.Context.Incidents.AddAsync(incident);
        await company.Context.SaveChangesAsync();

        var notifier = new RecordingIncidentNotifier();
        var repository = new IncidentRepository(company.Context, notifier);
        var actorId = company.Company.User.Id;

        // The trail is covered end to end over HTTP in AuditLogsApiTests; here it would only
        // add noise to assertions about the state machine itself.
        var audit = new NoopAuditLogWriter();
        var notifications = new NoopNotificationService();

        var acknowledged = await new AcknowledgeIncidentCommandHandler(repository, audit, notifications)
            .Handle(new AcknowledgeIncidentCommand(incident.Id, actorId), CancellationToken.None);

        Assert.Equal(IncidentStatus.Acknowledged.ToString(), acknowledged.Status);
        Assert.NotNull(acknowledged.AcknowledgedAt);

        var investigating = await new StartInvestigatingIncidentCommandHandler(repository, audit, notifications)
            .Handle(new StartInvestigatingIncidentCommand(incident.Id, actorId), CancellationToken.None);

        Assert.Equal(IncidentStatus.Investigating.ToString(), investigating.Status);
        Assert.NotNull(investigating.InvestigatingAt);

        var mitigated = await new MitigateIncidentCommandHandler(repository, audit, notifications)
            .Handle(new MitigateIncidentCommand(incident.Id, actorId), CancellationToken.None);

        Assert.Equal(IncidentStatus.Mitigated.ToString(), mitigated.Status);

        var resolved = await new ResolveIncidentCommandHandler(repository, audit, notifications)
            .Handle(new ResolveIncidentCommand(incident.Id, actorId), CancellationToken.None);

        Assert.Equal(IncidentStatus.Resolved.ToString(), resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        // Every hop in the walk announced the incident once, from the repository rather than
        // from each handler — which is what keeps the fan-out from drifting when a new
        // transition is added.
        Assert.Equal(4, notifier.IncidentUpdates.Count);
        Assert.All(notifier.IncidentUpdates, update => Assert.Equal(incident.Id, update.IncidentId));
    }

    [Fact]
    public async Task Transition_Over_Http_Returns_The_Updated_Incident()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "globex-transitions");

        var transition = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/transitions",
            new { toStatus = "acknowledged" });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        var payload = await transition.Content.ReadFromJsonAsync<Response<IncidentDto>>(Json);

        Assert.NotNull(payload?.Data);
        Assert.True(payload.Succeeded, string.Join(", ", payload.Errors));
        Assert.Equal(incidentId, payload.Data.Id);
        Assert.Equal(IncidentStatus.Acknowledged.ToString(), payload.Data.Status);
        Assert.NotNull(payload.Data.AcknowledgedAt);
    }

    [Fact]
    public async Task Transition_The_Domain_Forbids_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "globex-conflict");

        // Open may only become Acknowledged, so skipping straight to Resolved has to be
        // refused as a conflict with the incident's current state -- not as a bad request.
        var transition = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/transitions",
            new { toStatus = "resolved" });

        Assert.Equal(HttpStatusCode.Conflict, transition.StatusCode);

        var payload = await transition.Content.ReadFromJsonAsync<Response<IncidentDto>>(Json);

        Assert.NotNull(payload);
        Assert.False(payload.Succeeded);
        Assert.Equal(BusinessCode.Conflict, payload.Code);
        Assert.Contains(
            "Cannot move incident from status 'Open' to 'Resolved'.",
            payload.Errors,
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Transition_Note_Is_Recorded_On_The_Timeline()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "globex-note");

        var transition = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/transitions",
            new { toStatus = "acknowledged", note = Note });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        var timeline = await client.GetFromJsonAsync<Response<IReadOnlyList<IncidentEventDto>>>(
            $"/api/v1/incidents/{incidentId}/timeline",
            Json);

        Assert.NotNull(timeline?.Data);

        var recorded = Assert.Single(timeline.Data, e => e.Type == IncidentEventType.Acknowledged.ToString());

        Assert.NotNull(recorded.Payload);
        Assert.Contains(Note, recorded.Payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// Signs a company up over HTTP, registers a service through the catalogue and opens an
    /// incident through the API, so the tests start from the same state a person would.
    /// </summary>
    private async Task<(HttpClient Client, Guid IncidentId)> RegisterOwnerWithOpenIncidentAsync(
        IntegrationApiFactory factory,
        string slug)
    {
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, slug);

        var serviceResponse = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production", slug = $"payments-{slug}" });

        Assert.Equal(HttpStatusCode.OK, serviceResponse.StatusCode);

        var service = await serviceResponse.Content.ReadFromJsonAsync<Response<Guid>>(Json);

        Assert.NotNull(service?.Data);

        var created = await client.PostAsJsonAsync(
            "/api/v1/incidents",
            new
            {
                serviceId = service!.Data,
                title = "Checkout latency spike",
                description = "p99 latency above threshold",
                severity = "High",
            });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var incident = await created.Content.ReadFromJsonAsync<Response<Guid>>(Json);

        Assert.NotNull(incident?.Data);

        return (client, incident.Data);
    }

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
