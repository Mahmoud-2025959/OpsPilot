using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the retrospective endpoints over HTTP: the single-postmortem-per-incident rule, the
/// draft → review → published walk, the rejection path, and the rule that an incident must be
/// finished before its account of what happened can be made permanent.
/// </summary>
public sealed class PostmortemsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_Postmortem_Is_Written_For_An_Incident_And_Read_Back()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-write");

        var created = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new
            {
                title = "Checkout outage",
                summary = "Payments was unreachable for 12 minutes.",
                rootCause = "The connection pool was exhausted.",
                actionItems = "Raise the pool ceiling and alert on saturation.",
            });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.True(body!.Succeeded, string.Join(", ", body.Errors));
        Assert.Equal(PostmortemStatus.Draft.ToString(), body.Data.Status);
        Assert.Equal(incidentId, body.Data.IncidentId);
        Assert.Equal("The connection pool was exhausted.", body.Data.RootCause);
        Assert.Null(body.Data.PublishedAt);

        // It reads back the same way through the incident.
        var byIncident = await client.GetFromJsonAsync<Response<PostmortemDto>>(
            $"/api/v1/incidents/{incidentId}/postmortem",
            Json);

        Assert.NotNull(byIncident?.Data);
        Assert.Equal(body.Data.Id, byIncident!.Data.Id);

        // ... and through its own id.
        var byId = await client.GetFromJsonAsync<Response<PostmortemDto>>(
            $"/api/v1/postmortems/{body.Data.Id}",
            Json);

        Assert.NotNull(byId?.Data);
        Assert.Equal(body.Data.Id, byId!.Data.Id);
    }

    [Fact]
    public async Task An_Incident_Can_Only_Have_One_Postmortem()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-duplicate");

        var first = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "First", summary = "First summary" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The unique index would surface a second one as an unhandled database error; the
        // endpoint has to answer with a conflict instead.
        var second = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Second", summary = "Second summary" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body);
        Assert.False(body!.Succeeded);
        Assert.Equal(BusinessCode.Conflict, body.Code);
    }

    [Fact]
    public async Task A_Draft_Can_Be_Rewritten_But_A_Reviewed_One_Cannot()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-edit");

        var postmortem = await CreatePostmortemAsync(client, incidentId, "First title");

        var update = await client.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Second title", summary = "Second summary" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(updated?.Data);
        Assert.Equal("Second title", updated!.Data.Title);

        // Once it is under review the text under review must be the text that gets published.
        var review = await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/review", null);

        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var blocked = await client.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Third title", summary = "Third summary" });

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task A_Review_Can_Be_Rejected_And_The_Draft_Come_Back()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-reject");

        var postmortem = await CreatePostmortemAsync(client, incidentId, "A draft");

        await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/review", null);

        var returned = await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/return-to-draft", null);

        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        var body = await returned.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.Equal(PostmortemStatus.Draft.ToString(), body!.Data.Status);

        // And it can be worked on again.
        var rewrite = await client.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Rewritten", summary = "Rewritten summary" });

        Assert.Equal(HttpStatusCode.OK, rewrite.StatusCode);
    }

    [Fact]
    public async Task A_Postmortem_Cannot_Be_Published_Before_Its_Incident_Is_Resolved()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-early-publish");

        var postmortem = await CreatePostmortemAsync(client, incidentId, "A draft");

        await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/review", null);

        var publish = await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);

        var body = await publish.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body);
        Assert.Equal(BusinessCode.Conflict, body!.Code);
    }

    [Fact]
    public async Task The_Full_Life_Cycle_Ends_With_A_Published_Postmortem()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-lifecycle");

        var postmortem = await CreatePostmortemAsync(client, incidentId, "A draft");

        await ResolveIncidentAsync(client, incidentId);

        var review = await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/review", null);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var publish = await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var body = await publish.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.Equal(PostmortemStatus.Published.ToString(), body!.Data.Status);
        Assert.NotNull(body.Data.PublishedAt);

        // A published retrospective is final: no further writes are accepted.
        var edit = await client.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Too late", summary = "Too late" });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    [Fact]
    public async Task A_Viewer_Can_Read_A_Postmortem_But_Not_Write_One()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-viewer");
        var (viewer, _) = await ApiAuth.MemberClientAsync(factory, owner, "pm-viewer", "Viewer");

        var postmortem = await CreatePostmortemAsync(owner, incidentId, "A draft");

        var read = await viewer.GetFromJsonAsync<Response<PostmortemDto>>(
            $"/api/v1/postmortems/{postmortem.Id}",
            Json);

        Assert.NotNull(read?.Data);

        var write = await viewer.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Not allowed", summary = "Not allowed" });

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        var edit = await viewer.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Not allowed", summary = "Not allowed" });

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);

        var publish = await viewer.PostAsync($"/api/v1/postmortems/{postmortem.Id}/publish", null);

        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task Another_Company_Cannot_Reach_A_Postmortem()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-isolated-a");
        var (stranger, _) = await ApiAuth.OwnerClientAsync(factory, "pm-isolated-b");

        var postmortem = await CreatePostmortemAsync(owner, incidentId, "A draft");

        var read = await stranger.GetAsync($"/api/v1/postmortems/{postmortem.Id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        // ... and the incident it belongs to is equally out of reach.
        var byIncident = await stranger.GetAsync($"/api/v1/incidents/{incidentId}/postmortem");
        Assert.Equal(HttpStatusCode.NotFound, byIncident.StatusCode);
    }

    [Fact]
    public async Task The_Postmortems_Are_Paged_And_Can_Be_Narrowed_By_State()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-paging");

        await CreatePostmortemAsync(client, incidentId, "A draft");

        var all = await client.GetFromJsonAsync<PagedResponse<PostmortemDto>>(
            "/api/v1/postmortems?pageSize=100",
            Json);

        Assert.NotNull(all?.Data);
        Assert.Equal(1, all!.TotalCount);

        var drafts = await client.GetFromJsonAsync<PagedResponse<PostmortemDto>>(
            "/api/v1/postmortems?status=Draft&pageSize=100",
            Json);

        Assert.NotNull(drafts?.Data);
        Assert.Equal(1, drafts!.TotalCount);

        var published = await client.GetFromJsonAsync<PagedResponse<PostmortemDto>>(
            "/api/v1/postmortems?status=Published&pageSize=100",
            Json);

        Assert.NotNull(published?.Data);
        Assert.Equal(0, published!.TotalCount);
    }

    [Fact]
    public async Task Every_Change_Is_Recorded_On_The_Audit_Trail()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, incidentId) = await RegisterOwnerWithOpenIncidentAsync(factory, "pm-audit");

        var postmortem = await CreatePostmortemAsync(client, incidentId, "A draft");

        await client.PutAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title = "Rewritten", summary = "Rewritten summary" });

        await client.PostAsync($"/api/v1/postmortems/{postmortem.Id}/review", null);

        var trail = await client.GetFromJsonAsync<PagedResponse<AuditLogDto>>(
            $"/api/v1/audit-logs?entityType=Postmortem&entityId={postmortem.Id}&pageSize=100",
            Json);

        Assert.NotNull(trail?.Data);
        Assert.Contains(trail.Data, e => e.Action == AuditActions.PostmortemCreated);
        Assert.Contains(trail.Data, e => e.Action == AuditActions.PostmortemUpdated);
        Assert.Contains(trail.Data, e => e.Action == AuditActions.PostmortemStatusChanged);
    }

    private async Task<PostmortemDto> CreatePostmortemAsync(HttpClient client, Guid incidentId, string title)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/postmortem",
            new { title, summary = $"{title} summary" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response<PostmortemDto>>(Json);

        Assert.NotNull(body?.Data);

        return body!.Data;
    }

    private async Task ResolveIncidentAsync(HttpClient client, Guid incidentId)
    {
        foreach (var status in new[] { "acknowledged", "investigating", "resolved" })
        {
            var transition = await client.PostAsJsonAsync(
                $"/api/v1/incidents/{incidentId}/transitions",
                new { toStatus = status });

            Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
        }
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
}
