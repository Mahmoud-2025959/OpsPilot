using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Events;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the ingestion API end to end: an application authenticates with a key, the event either
/// opens an incident or joins the open one, and the incident comes out with an SLA deadline, a
/// bumped occurrence count and the key stamped on its timeline (plan, UC-06).
/// </summary>
public sealed class EventsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task First_Event_Opens_An_Incident_And_A_Repeat_Correlates()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, ingest, key, _) = await ArrangeAsync(factory, "evt-correlate");

        var first = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new
            {
                service = "Payments",
                environment = "Production",
                severity = "Critical",
                message = "Database connection timeout after 30s",
            });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var firstResult = await first.Content.ReadFromJsonAsync<Response<EventIngestResultDto>>(ApiAuth.Json);

        Assert.NotNull(firstResult?.Data);
        Assert.True(firstResult!.Succeeded, string.Join(", ", firstResult.Errors));
        Assert.False(firstResult.Data.Correlated);

        var incidentId = firstResult.Data.IncidentId;

        // The same fault with a different number in the message: normalization must make the two
        // agree so this joins the existing incident instead of opening a second one.
        var second = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new
            {
                service = "Payments",
                environment = "Production",
                severity = "Critical",
                message = "Database connection timeout after 31s",
            });

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var secondResult = await second.Content.ReadFromJsonAsync<Response<EventIngestResultDto>>(ApiAuth.Json);

        Assert.NotNull(secondResult?.Data);
        Assert.True(secondResult!.Data.Correlated);
        Assert.Equal(incidentId, secondResult.Data.IncidentId);

        // The incident carries the deadline the SLA policy implies and the bumped counter.
        var incident = await owner.GetFromJsonAsync<Response<IncidentDto>>(
            $"/api/v1/incidents/{incidentId}",
            ApiAuth.Json);

        Assert.NotNull(incident?.Data);
        Assert.Equal(2, incident!.Data.OccurrenceCount);
        Assert.Equal("Critical", incident.Data.Severity);
        Assert.NotNull(incident.Data.ResponseDueAt);
        Assert.NotNull(incident.Data.ResolutionDueAt);
        Assert.NotNull(incident.Data.CorrelationKey);

        // Both entries name the key that produced them, which is how a machine event is told
        // apart from a human one.
        var timeline = await owner.GetFromJsonAsync<Response<IReadOnlyList<IncidentEventDto>>>(
            $"/api/v1/incidents/{incidentId}/timeline",
            ApiAuth.Json);

        Assert.NotNull(timeline?.Data);
        Assert.Equal(2, timeline!.Data.Count);
        Assert.All(timeline.Data, e => Assert.Equal(key.Id, e.ApiKeyId));
        Assert.All(timeline.Data, e => Assert.Equal("EventIngested", e.Type));
    }

    [Fact]
    public async Task Different_Services_Do_Not_Correlate()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (_, ingest, _, _) = await ArrangeAsync(factory, "evt-services");

        var payments = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "timeout" });

        var paymentsResult = await payments.Content.ReadFromJsonAsync<Response<EventIngestResultDto>>(ApiAuth.Json);

        var checkout = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Checkout", environment = "Production", severity = "High", message = "timeout" });

        var checkoutResult = await checkout.Content.ReadFromJsonAsync<Response<EventIngestResultDto>>(ApiAuth.Json);

        Assert.False(paymentsResult!.Data!.Correlated);
        Assert.False(checkoutResult!.Data!.Correlated);

        // Same message, different service: two incidents, not one.
        Assert.NotEqual(paymentsResult.Data.IncidentId, checkoutResult.Data.IncidentId);
    }

    [Fact]
    public async Task An_Event_For_An_Unregistered_Service_Is_Unprocessable()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (_, ingest, _, _) = await ArrangeAsync(factory, "evt-unknown");

        var response = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new
            {
                service = "Ghost",
                environment = "Production",
                severity = "High",
                message = "should not open anything",
            });

        // 422, not 404: the request is understood, the service reference is not. The plan (UC-06)
        // also insists the service is not created implicitly.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_Human_Bearer_Token_Cannot_Ingest()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _, _, _) = await ArrangeAsync(factory, "evt-human");

        // The Owner holds every catalogue permission except events.ingest, which is reserved for
        // keys, so a user token is rejected on the permission rather than the credential.
        var response = await owner.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "hello" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_Missing_Key_Is_Unauthorized()
    {
        await using var factory = new IntegrationApiFactory(db);
        await ArrangeAsync(factory, "evt-anon");

        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Registers an owner, two services and a fresh key, and returns a client that authenticates
    /// with that key — the way a company's own tooling would.
    /// </summary>
    private static async Task<(HttpClient Owner, HttpClient Ingest, CreatedApiKeyDto Key, Guid PaymentsId)>
        ArrangeAsync(IntegrationApiFactory factory, string slug)
    {
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, slug);

        var paymentsId = await CreateServiceAsync(owner, "Payments");
        await CreateServiceAsync(owner, "Checkout");

        var keyResponse = await owner.PostAsJsonAsync("/api/v1/api-keys", new { name = "metrics-agent" });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);

        var key = (await keyResponse.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        var ingest = factory.CreateClient();
        ingest.DefaultRequestHeaders.Add("X-API-Key", key.Key);

        return (owner, ingest, key, paymentsId);
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }
}
