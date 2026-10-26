using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Services;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the service catalogue over HTTP, including the reason it exists at all: an incident has
/// to point at a service, so before <c>/api/v1/services</c> was reachable nobody could open one
/// from the API.
/// </summary>
public sealed class ServicesApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Creating_A_Service_Unblocks_Opening_An_Incident()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-unblock");

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        Assert.True(created!.Succeeded, string.Join(", ", created.Errors));

        // The whole point: take the id from the response and use it as incident.serviceId, with
        // nothing seeded behind the API's back.
        var incident = await client.PostAsJsonAsync(
            "/api/v1/incidents",
            new
            {
                serviceId = created.Data,
                title = "Checkout latency spike",
                description = "p99 latency above threshold",
                severity = "High",
            });

        Assert.Equal(HttpStatusCode.OK, incident.StatusCode);

        var incidentId = await incident.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        Assert.NotNull(incidentId?.Data);
        Assert.NotEqual(Guid.Empty, incidentId!.Data);
    }

    [Fact]
    public async Task Created_Service_Comes_Back_From_The_List_And_By_Id()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-read");

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new
            {
                name = "Identity",
                environment = "Staging",
                slug = "identity-staging",
                description = "Sign-in and tokens",
            });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);
        var serviceId = created!.Data;

        var byId = await client.GetFromJsonAsync<Response<ServiceDto>>(
            $"/api/v1/services/{serviceId}",
            ApiAuth.Json);

        Assert.NotNull(byId?.Data);
        Assert.Equal("Identity", byId.Data.Name);
        Assert.Equal("Staging", byId.Data.Environment);
        Assert.Equal("identity-staging", byId.Data.Slug);
        Assert.True(byId.Data.IsActive);

        var list = await client.GetFromJsonAsync<PagedResponse<ServiceDto>>(
            "/api/v1/services?page=1&pageSize=50",
            ApiAuth.Json);

        Assert.NotNull(list?.Data);
        Assert.Contains(list.Data, s => s.Id == serviceId);
    }

    [Fact]
    public async Task The_Same_Name_In_A_Different_Environment_Is_Allowed()
    {
        // ERD §3: one company legitimately runs "Payments" in more than one environment.
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-envs");

        var production = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        var staging = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Staging" });

        Assert.Equal(HttpStatusCode.OK, production.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staging.StatusCode);
    }

    [Fact]
    public async Task The_Same_Name_In_The_Same_Environment_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-dupe");

        var first = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var payload = await second.Content.ReadFromJsonAsync<Response<string>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.Conflict, payload.Code);
    }

    [Fact]
    public async Task An_Unknown_Environment_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-badenv");

        var response = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Pluto" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Response<string>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.ValidationError, payload.Code);
    }

    [Fact]
    public async Task A_Viewer_May_Read_Services_But_Not_Create_One()
    {
        // The plan grants reading to everyone and writing to services.manage.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "svc-viewer");

        var seeded = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        var (viewer, _) = await ApiAuth.MemberClientAsync(factory, owner, "svc-viewer", "Viewer");

        var read = await viewer.GetAsync("/api/v1/services");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await viewer.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Billing", environment = "Production" });

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Updating_A_Service_Moves_It_Between_Environments()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-update");

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Staging" });

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);
        var serviceId = created!.Data;

        var update = await client.PutAsJsonAsync(
            $"/api/v1/services/{serviceId}",
            new { name = "Payments", environment = "Production", description = "Moved to prod" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<Response<ServiceDto>>(ApiAuth.Json);

        Assert.NotNull(updated?.Data);
        Assert.Equal("Production", updated.Data.Environment);
        Assert.Equal("Moved to prod", updated.Data.Description);
    }

    [Fact]
    public async Task Updating_Only_The_Description_Does_Not_Conflict_With_Itself()
    {
        // The most common edit: leave name and environment alone and change something else. The
        // uniqueness check must exclude the row being edited, otherwise every such edit would
        // report a conflict with itself.
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-self-edit");

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);
        var serviceId = created!.Data;

        var update = await client.PutAsJsonAsync(
            $"/api/v1/services/{serviceId}",
            new { name = "Payments", environment = "Production", description = "Now with more detail" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<Response<ServiceDto>>(ApiAuth.Json);

        Assert.NotNull(updated?.Data);
        Assert.Equal("Now with more detail", updated.Data.Description);
        Assert.Equal("Production", updated.Data.Environment);
    }

    [Fact]
    public async Task Renaming_A_Service_Onto_A_Taken_Name_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-rename-clash");

        await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        var second = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Billing", environment = "Production" });

        var billingId = (await second.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;

        // Moving "Billing" onto "Payments" in the same environment collides with the other row.
        var update = await client.PutAsJsonAsync(
            $"/api/v1/services/{billingId}",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Updating_An_Unknown_Service_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-missing");

        var update = await client.PutAsJsonAsync(
            $"/api/v1/services/{Guid.NewGuid()}",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task A_Service_Can_Be_Assigned_To_A_Team()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-owner-team");

        var team = await client.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });

        Assert.Equal(HttpStatusCode.OK, team.StatusCode);

        var teamId = (await team.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production", ownerTeamId = teamId });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var serviceId = (await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;

        var byId = await client.GetFromJsonAsync<Response<ServiceDto>>(
            $"/api/v1/services/{serviceId}",
            ApiAuth.Json);

        Assert.Equal(teamId, byId!.Data!.OwnerTeamId);
    }

    [Fact]
    public async Task Assigning_A_Service_To_An_Unknown_Team_Is_Not_Found()
    {
        // OwnerTeamId is a foreign key, so without this check a bogus id would come back as a 500
        // from the constraint rather than a readable answer.
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "svc-ghost-team");

        var create = await client.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production", ownerTeamId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }
}
