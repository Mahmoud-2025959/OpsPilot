using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Guards the foreign keys that arrive in a request body. Each of these used to reach the
/// database untouched, so a bogus id came back as a constraint violation -- a 500 -- or, worse,
/// silently linked the row to another company. They must answer 404 instead, and a successfully
/// created incident must carry the SLA for its severity from the moment it is opened.
/// </summary>
public sealed class IncidentIntegrityApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static async Task<Guid> ServiceAsync(HttpClient client, string name)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        return created.Data;
    }

    private static async Task<Guid> IncidentAsync(HttpClient client, Guid serviceId, string severity = "High")
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/incidents",
            new
            {
                serviceId,
                title = "Checkout latency spike",
                description = "p99 latency above threshold",
                severity,
            });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        return created.Data;
    }

    [Fact]
    public async Task Opening_An_Incident_Against_An_Unknown_Service_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "incident-unknown-service");

        var response = await client.PostAsJsonAsync(
            "/api/v1/incidents",
            new
            {
                serviceId = Guid.NewGuid(),
                title = "Checkout latency spike",
                description = "p99 latency above threshold",
                severity = "High",
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assigning_An_Incident_To_An_Unknown_User_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "incident-unknown-assignee");

        var serviceId = await ServiceAsync(client, "Payments");
        var incidentId = await IncidentAsync(client, serviceId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/assign",
            new { assigneeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Assigning_An_Incident_To_A_Member_Succeeds()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "incident-assign-member");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "incident-assign-member", "Engineer");

        var serviceId = await ServiceAsync(owner, "Payments");
        var incidentId = await IncidentAsync(owner, serviceId);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/assign",
            new { assigneeId = userId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var incident = await owner.GetFromJsonAsync<Response<IncidentDto>>(
            $"/api/v1/incidents/{incidentId}",
            ApiAuth.Json);

        Assert.Equal(userId, incident?.Data?.AssignedTo);
    }

    [Fact]
    public async Task A_New_Incident_Carries_The_Sla_Deadlines_For_Its_Severity()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "incident-sla");

        var serviceId = await ServiceAsync(client, "Payments");
        var incidentId = await IncidentAsync(client, serviceId, severity: "Critical");

        var incident = await client.GetFromJsonAsync<Response<IncidentDto>>(
            $"/api/v1/incidents/{incidentId}",
            ApiAuth.Json);

        Assert.NotNull(incident?.Data);

        // The seeded Critical policy is 15 / 240 minutes, measured from creation.
        Assert.Equal(incident.Data.CreatedAt.AddMinutes(15), incident.Data.ResponseDueAt);
        Assert.Equal(incident.Data.CreatedAt.AddMinutes(240), incident.Data.ResolutionDueAt);
    }
}
