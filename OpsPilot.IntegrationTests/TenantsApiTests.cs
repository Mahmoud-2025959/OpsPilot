using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Tenants;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the company's own settings: reading it, renaming it, closing it and bringing it
/// back (ERD §1).
/// </summary>
/// <remarks>
/// The rules being checked are the ones a request can break: only the Owner may change the
/// company, a company with no usable Owner cannot be closed, and a closed company stops
/// admitting its people — while still admitting the Owner, who is the one who can reopen it.
/// </remarks>
public sealed class TenantsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task The_Owner_Can_Read_Their_Own_Company()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "tenant-read");

        var body = await owner.GetFromJsonAsync<Response<TenantDto>>("/api/v1/tenant", Json);

        Assert.NotNull(body?.Data);
        Assert.True(body!.Succeeded, string.Join(", ", body.Errors));
        Assert.Equal("tenant-read", body.Data.Slug);
        Assert.Equal("tenant-read", body.Data.Name);
        Assert.True(body.Data.IsActive);
    }

    [Fact]
    public async Task The_Company_Can_Be_Renamed()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "tenant-rename");

        var renamed = await owner.PutAsJsonAsync(
            "/api/v1/tenant",
            new { name = "A Better Name" });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var body = await renamed.Content.ReadFromJsonAsync<Response<TenantDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.Equal("A Better Name", body!.Data.Name);

        // The handle is not changeable: it is already in the tokens people are holding.
        Assert.Equal("tenant-rename", body.Data.Slug);

        // And it reads back, so the change is persisted rather than echoed.
        var reloaded = await owner.GetFromJsonAsync<Response<TenantDto>>("/api/v1/tenant", Json);

        Assert.NotNull(reloaded?.Data);
        Assert.Equal("A Better Name", reloaded!.Data.Name);
    }

    [Fact]
    public async Task Nobody_But_The_Owner_Can_Change_The_Company()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "tenant-permissions");
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "tenant-permissions", "Engineer");

        var read = await engineer.GetAsync("/api/v1/tenant");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var rename = await engineer.PutAsJsonAsync("/api/v1/tenant", new { name = "Not allowed" });
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        var deactivate = await engineer.PostAsync("/api/v1/tenant/deactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
    }

    [Fact]
    public async Task The_Company_Can_Be_Closed_And_Brought_Back()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "tenant-lifecycle");

        var closed = await owner.PostAsync("/api/v1/tenant/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var closedBody = await closed.Content.ReadFromJsonAsync<Response<TenantDto>>(Json);
        Assert.NotNull(closedBody?.Data);
        Assert.False(closedBody!.Data.IsActive);

        var reopened = await owner.PostAsync("/api/v1/tenant/activate", null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);

        var reopenedBody = await reopened.Content.ReadFromJsonAsync<Response<TenantDto>>(Json);
        Assert.NotNull(reopenedBody?.Data);
        Assert.True(reopenedBody!.Data.IsActive);
    }

    [Fact]
    public async Task A_Closed_Company_Refuses_New_Sign_In_But_Still_Admits_Its_Owner()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "tenant-closed-signin");
        var (member, _) = await ApiAuth.MemberClientAsync(factory, owner, "tenant-closed-signin", "Engineer");

        var closed = await owner.PostAsync("/api/v1/tenant/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        // The people of a closed company are locked out...
        var stranger = factory.CreateClient();
        var refused = await stranger.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = $"engineer-tenant-closed-signin@ops-pilot.test", password = ApiAuth.Password });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // ... but the Owner is the one who can bring the company back, so they are let in.
        var ownerAgain = factory.CreateClient();
        var allowed = await ownerAgain.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = $"owner-tenant-closed-signin@ops-pilot.test", password = ApiAuth.Password });

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task A_Company_With_No_Owner_Cannot_Be_Closed()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "tenant-no-owner");

        // The API refuses to remove the last Owner, so the company is left without one at the
        // database level: the point of the guard is exactly the state the API forbids. The
        // account stays usable — it is the role assignment that is taken away — so the
        // request still reaches the endpoint instead of being refused at the door.
        var tenants = new TenantContext();
        using var entered = tenants.Enter(auth.TenantId);
        await using var context = db.CreateContext(tenants);

        var removed = await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserRoles WHERE UserId = {0}",
            auth.Id);

        Assert.Equal(1, removed);

        var closed = await owner.PostAsync("/api/v1/tenant/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);

        var body = await closed.Content.ReadFromJsonAsync<Response<TenantDto>>(Json);

        Assert.NotNull(body);
        Assert.Equal(BusinessCode.Conflict, body!.Code);
    }

    [Fact]
    public async Task Every_Change_Is_Recorded_On_The_Audit_Trail()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "tenant-audit");

        await owner.PutAsJsonAsync("/api/v1/tenant", new { name = "Renamed" });
        await owner.PostAsync("/api/v1/tenant/deactivate", null);

        var trail = await owner.GetFromJsonAsync<PagedResponse<AuditLogDto>>(
            $"/api/v1/audit-logs?entityType=Tenant&entityId={auth.TenantId}&pageSize=100",
            Json);

        Assert.NotNull(trail?.Data);
        Assert.Contains(trail.Data, e => e.Action == AuditActions.TenantRenamed);
        Assert.Contains(trail.Data, e => e.Action == AuditActions.TenantStatusChanged);
    }
}
