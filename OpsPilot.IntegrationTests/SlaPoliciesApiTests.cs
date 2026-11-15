using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.SlaPolicies;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers reading and retuning the company's SLA targets (plan, UC-05 and FR-14), including the
/// point of the whole feature: a retuned window is what a newly opened incident is measured
/// against.
/// </summary>
public sealed class SlaPoliciesApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Get_Returns_The_Four_Seeded_Policies_In_Severity_Order()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-defaults");

        var policies = await GetPoliciesAsync(owner);

        Assert.Equal(
            new[] { "Critical", "High", "Medium", "Low" },
            policies.Select(p => p.Severity));

        // The seeded targets (DefaultSlaPolicies): 15/240, 30/480, 120/1440, 480/4320.
        var critical = policies.Single(p => p.Severity == "Critical");
        Assert.Equal(15, critical.ResponseMinutes);
        Assert.Equal(240, critical.ResolutionMinutes);

        var low = policies.Single(p => p.Severity == "Low");
        Assert.Equal(480, low.ResponseMinutes);
        Assert.Equal(4320, low.ResolutionMinutes);
    }

    [Fact]
    public async Task Put_Retunes_Only_The_Supplied_Severities()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-put");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Critical", responseMinutes = 5, resolutionMinutes = 60 },
            },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await put.Content.ReadFromJsonAsync<Response<IReadOnlyList<SlaPolicyDto>>>(ApiAuth.Json);

        Assert.NotNull(body?.Data);
        Assert.Equal(5, body!.Data.Single(p => p.Severity == "Critical").ResponseMinutes);
        Assert.Equal(60, body.Data.Single(p => p.Severity == "Critical").ResolutionMinutes);

        // A severity that was not in the batch keeps its seeded value.
        Assert.Equal(30, body.Data.Single(p => p.Severity == "High").ResponseMinutes);

        // And the change is persisted, not just echoed back.
        var persisted = await GetPoliciesAsync(owner);
        Assert.Equal(60, persisted.Single(p => p.Severity == "Critical").ResolutionMinutes);
    }

    [Fact]
    public async Task Put_Can_Retune_Several_Severities_At_Once()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-batch");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Critical", responseMinutes = 10, resolutionMinutes = 120 },
                new { severity = "Medium", responseMinutes = 60, resolutionMinutes = 720 },
            },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var persisted = await GetPoliciesAsync(owner);

        Assert.Equal(10, persisted.Single(p => p.Severity == "Critical").ResponseMinutes);
        Assert.Equal(720, persisted.Single(p => p.Severity == "Medium").ResolutionMinutes);
    }

    [Fact]
    public async Task Resolution_Not_Greater_Than_Response_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-invalid-order");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Critical", responseMinutes = 120, resolutionMinutes = 60 },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task An_Empty_Batch_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-empty");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new { policies = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task An_Unknown_Severity_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-bad-severity");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Urgent", responseMinutes = 5, resolutionMinutes = 60 },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Severities_Are_Rejected()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-duplicate");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Critical", responseMinutes = 5, resolutionMinutes = 60 },
                new { severity = "Critical", responseMinutes = 10, resolutionMinutes = 90 },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Policies_Require_The_Manage_Permission()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-perm");

        // An Engineer works incidents but does not administer SLA targets.
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "sla-perm", "Engineer");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await engineer.GetAsync("/api/v1/sla-policies")).StatusCode);

        var put = await engineer.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "Critical", responseMinutes = 5, resolutionMinutes = 60 },
            },
        });

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task A_Retuned_Window_Is_What_A_New_Incident_Is_Measured_Against()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "sla-applied");

        var put = await owner.PutAsJsonAsync("/api/v1/sla-policies", new
        {
            policies = new[]
            {
                new { severity = "High", responseMinutes = 7, resolutionMinutes = 90 },
            },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var serviceId = await CreateServiceAsync(owner, "Payments");

        var create = await owner.PostAsJsonAsync("/api/v1/incidents", new
        {
            serviceId,
            title = "API is down",
            description = "5xx on the public API",
            severity = "High",
        });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var incidentId = (await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;

        var incident = await owner.GetFromJsonAsync<Response<IncidentDto>>(
            $"/api/v1/incidents/{incidentId}",
            ApiAuth.Json);

        Assert.NotNull(incident?.Data);

        var data = incident!.Data;
        Assert.Equal("High", data.Severity);
        Assert.NotNull(data.ResponseDueAt);
        Assert.NotNull(data.ResolutionDueAt);

        // Deadlines are the incident's creation moment plus the retuned windows.
        Assert.Equal(TimeSpan.FromMinutes(7), data.ResponseDueAt!.Value - data.CreatedAt);
        Assert.Equal(TimeSpan.FromMinutes(90), data.ResolutionDueAt!.Value - data.CreatedAt);
    }

    private static async Task<IReadOnlyList<SlaPolicyDto>> GetPoliciesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/sla-policies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response<IReadOnlyList<SlaPolicyDto>>>(ApiAuth.Json);

        Assert.NotNull(body?.Data);

        return body!.Data;
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
