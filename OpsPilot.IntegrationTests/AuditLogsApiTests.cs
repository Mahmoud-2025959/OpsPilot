using System.Net;
using System.Net.Http.Json;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers recording the sensitive actions named in FR-18 — severity change, assignment,
/// status moves (which include resolving), and role change — and reading the trail back
/// through <c>GET /api/v1/audit-logs</c> (plan, UC-14).
/// </summary>
public sealed class AuditLogsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Severity_Change_Is_Recorded_With_Both_Values_And_The_Actor()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "audit-severity");

        var serviceId = await CreateServiceAsync(owner, "Audit Severity");
        var incidentId = await CreateIncidentAsync(owner, serviceId, severity: "Medium");

        var put = await owner.PutAsJsonAsync($"/api/v1/incidents/{incidentId}", new
        {
            title = "API is down",
            description = "5xx on the public API",
            severity = "High",
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var entries = await GetEntriesAsync(owner, $"entityType=Incident&entityId={incidentId}");

        var entry = Assert.Single(entries, e => e.Action == AuditActions.IncidentSeverityChanged);

        // Both ends of the change are on the record, and so is who made it.
        Assert.Contains("Medium", entry.OldValues, StringComparison.Ordinal);
        Assert.Contains("High", entry.NewValues, StringComparison.Ordinal);
        Assert.Equal(auth.Id, entry.ActorUserId);
    }

    [Fact]
    public async Task Assignment_Is_Recorded()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "audit-assign");

        var serviceId = await CreateServiceAsync(owner, "Audit Assign");
        var incidentId = await CreateIncidentAsync(owner, serviceId);
        var (engineer, engineerId) = await ApiAuth.MemberClientAsync(factory, owner, "audit-assign", "Engineer");

        var assign = await owner.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/assign", new
        {
            assigneeId = engineerId,
        });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var entries = await GetEntriesAsync(owner, $"entityType=Incident&entityId={incidentId}");

        var entry = Assert.Single(entries, e => e.Action == AuditActions.IncidentAssigned);

        Assert.Contains(engineerId.ToString(), entry.NewValues, StringComparison.Ordinal);
        Assert.Equal(auth.Id, entry.ActorUserId);
    }

    [Fact]
    public async Task Resolving_An_Incident_Is_Recorded_As_A_Status_Change()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "audit-resolve");

        var serviceId = await CreateServiceAsync(owner, "Audit Resolve");
        var incidentId = await CreateIncidentAsync(owner, serviceId);

        // The state machine only allows Investigating -> Resolved, so the incident is walked
        // there first; each intermediate hop records its own entry.
        foreach (var hop in new[] { "acknowledged", "investigating" })
        {
            var step = await owner.PostAsJsonAsync(
                $"/api/v1/incidents/{incidentId}/transitions",
                new { toStatus = hop });

            Assert.Equal(HttpStatusCode.OK, step.StatusCode);
        }

        var transition = await owner.PostAsJsonAsync($"/api/v1/incidents/{incidentId}/transitions", new
        {
            toStatus = "resolved",
            note = "Restarted the pool",
        });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        var entries = await GetEntriesAsync(owner, $"entityType=Incident&entityId={incidentId}");

        // One entry per hop; the resolving one carries Investigating on the way out.
        Assert.Equal(3, entries.Count(e => e.Action == AuditActions.IncidentStatusChanged));

        var entry = Assert.Single(entries, e =>
            e.Action == AuditActions.IncidentStatusChanged
            && e.NewValues!.Contains("Resolved", StringComparison.Ordinal));

        // The trail records the move, not just the destination: Investigating -> Resolved.
        Assert.Contains("Investigating", entry.OldValues, StringComparison.Ordinal);
        Assert.Contains("Resolved", entry.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_Change_Is_Recorded_With_Both_Role_Sets()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "audit-roles");

        var (_, memberId) = await ApiAuth.MemberClientAsync(factory, owner, "audit-roles", "Engineer");

        var put = await owner.PutAsJsonAsync($"/api/v1/users/{memberId}/roles", new
        {
            roles = new[] { "Viewer" },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var entries = await GetEntriesAsync(owner, $"entityType=User&entityId={memberId}");

        var entry = Assert.Single(entries, e => e.Action == AuditActions.UserRoleChanged);

        Assert.Contains("Engineer", entry.OldValues, StringComparison.Ordinal);
        Assert.Contains("Viewer", entry.NewValues, StringComparison.Ordinal);
        Assert.Equal(auth.Id, entry.ActorUserId);
    }

    [Fact]
    public async Task Saving_The_Same_Severity_Twice_Leaves_One_Entry()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "audit-unchanged");

        var serviceId = await CreateServiceAsync(owner, "Audit Unchanged");
        var incidentId = await CreateIncidentAsync(owner, serviceId, severity: "High");

        // The same severity again, with a corrected title: not a severity change, so no entry.
        var put = await owner.PutAsJsonAsync($"/api/v1/incidents/{incidentId}", new
        {
            title = "A clearer title",
            description = "5xx on the public API",
            severity = "High",
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var entries = await GetEntriesAsync(owner, $"entityType=Incident&entityId={incidentId}");

        Assert.DoesNotContain(entries, e => e.Action == AuditActions.IncidentSeverityChanged);
    }

    [Fact]
    public async Task Reading_The_Trail_Requires_The_Audit_Permission()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "audit-perm");

        // An Engineer works incidents but does not review the trail.
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "audit-perm", "Engineer");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await engineer.GetAsync("/api/v1/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Entries_Are_Filtered_By_Entity_And_Paged_Newest_First()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "audit-paging");

        var serviceId = await CreateServiceAsync(owner, "Audit Paging");
        var first = await CreateIncidentAsync(owner, serviceId, severity: "Low");
        var second = await CreateIncidentAsync(owner, serviceId, severity: "Low");

        // Both incidents get a severity change; the filter must return only the one asked for.
        foreach (var incidentId in new[] { first, second })
        {
            var put = await owner.PutAsJsonAsync($"/api/v1/incidents/{incidentId}", new
            {
                title = "API is down",
                description = "5xx",
                severity = "Critical",
            });

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }

        var forFirst = await GetEntriesAsync(owner, $"entityType=Incident&entityId={first}");
        Assert.Single(forFirst);

        // A page of one shows the newest entry and reports the real total.
        var page = await owner.GetFromJsonAsync<PagedResponse<AuditLogDto>>(
            $"/api/v1/audit-logs?entityType=Incident&pageSize=1&page=1",
            ApiAuth.Json);

        Assert.NotNull(page);
        Assert.Equal(second.ToString(), page!.Data!.Single().EntityId);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task Another_Company_Cannot_See_The_Trail()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (acme, _) = await ApiAuth.OwnerClientAsync(factory, "audit-tenanta");

        var serviceId = await CreateServiceAsync(acme, "Audit Tenant");
        var incidentId = await CreateIncidentAsync(acme, serviceId);

        var put = await acme.PutAsJsonAsync($"/api/v1/incidents/{incidentId}", new
        {
            title = "API is down",
            description = "5xx",
            severity = "Critical",
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var (globex, _) = await ApiAuth.OwnerClientAsync(factory, "audit-tenantb");

        var entries = await GetEntriesAsync(globex, "pageSize=100");

        Assert.Empty(entries);
    }

    private static async Task<PagedResponse<AuditLogDto>> GetPageAsync(
        HttpClient client,
        string queryString)
    {
        var response = await client.GetAsync($"/api/v1/audit-logs?{queryString}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<AuditLogDto>>(ApiAuth.Json);

        Assert.NotNull(body);

        return body!;
    }

    private static async Task<IReadOnlyList<AuditLogDto>> GetEntriesAsync(
        HttpClient client,
        string queryString)
        => (await GetPageAsync(client, queryString)).Data!;

    private static async Task<Guid> CreateServiceAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }

    private static async Task<Guid> CreateIncidentAsync(
        HttpClient owner,
        Guid serviceId,
        string severity = "Medium")
    {
        var response = await owner.PostAsJsonAsync("/api/v1/incidents", new
        {
            serviceId,
            title = "API is down",
            description = "5xx on the public API",
            severity,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }
}
