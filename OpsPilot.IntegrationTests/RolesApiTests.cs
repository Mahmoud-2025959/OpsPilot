using System.Net;
using System.Net.Http.Json;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Permissions;
using OpsPilot.Contracts.Roles;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Identity;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the role and permission endpoints over HTTP: the catalogue, the company's roles and
/// the rules that protect them (ERD §3).
/// </summary>
/// <remarks>
/// The rules being checked are the ones a request can break: a system role being renamed or
/// retired, the Owner's permission set being rewritten, and an administrator handing out a
/// permission they do not hold themselves. The last one is the interesting case, because the
/// policy check only proves the caller may manage roles -- it says nothing about which
/// permissions they may put into one.
/// </remarks>
public sealed class RolesApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task The_Catalogue_Lists_Every_Known_Permission()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-catalogue");

        var body = await owner.GetFromJsonAsync<Response<IReadOnlyList<PermissionDto>>>(
            "/api/v1/permissions",
            ApiAuth.Json);

        Assert.NotNull(body?.Data);

        var catalogue = body!.Data;

        // The catalogue is the fixed set in code, so the endpoint must expose exactly that.
        Assert.Equal(PermissionCatalog.Names.Count, catalogue.Count);
        Assert.Contains(catalogue, p => p.Name == PermissionNames.IncidentsRead);
        Assert.Contains(catalogue, p => p.Name == PermissionNames.EventsIngest);
        Assert.All(catalogue, p => Assert.False(string.IsNullOrWhiteSpace(p.Category)));
    }

    [Fact]
    public async Task A_New_Company_Starts_With_The_Five_System_Roles()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-seeded");

        var roles = await GetRolesAsync(owner);

        Assert.Equal(SystemRoleNames.All.Count, roles.Count);
        Assert.All(roles, r => Assert.True(r.IsSystem));

        Assert.Equal(
            SystemRoleNames.All.OrderBy(n => n, StringComparer.Ordinal),
            roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));

        // The Owner holds the whole catalogue except ingestion, which belongs to API keys.
        var ownerRole = Assert.Single(roles, r => r.IsOwner);

        Assert.Equal(
            RolePermissionDefaults.For(SystemRoleNames.Owner).Count,
            ownerRole.Permissions.Count);

        Assert.DoesNotContain(ownerRole.Permissions, p => p.Name == PermissionNames.EventsIngest);
    }

    [Fact]
    public async Task A_Custom_Role_Is_Created_Renamed_And_Retired()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-lifecycle");

        var create = await owner.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Support",
            description = "Front line",
            permissions = new[] { PermissionNames.IncidentsRead, PermissionNames.IncidentsComment },
        });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        Assert.False(created!.Data.IsSystem);
        Assert.False(created.Data.IsOwner);
        Assert.Equal(2, created.Data.Permissions.Count);

        var roleId = created.Data.Id;

        var rename = await owner.PutAsJsonAsync(
            $"/api/v1/roles/{roleId}",
            new { name = "Support Lead", description = "Front line, on call" });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        var renamed = await rename.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(renamed?.Data);
        Assert.Equal("Support Lead", renamed!.Data.Name);
        Assert.Equal("Front line, on call", renamed.Data.Description);

        // It sits beside the system roles rather than replacing one.
        Assert.Contains(await GetRolesAsync(owner), r => r.Id == roleId);

        var delete = await owner.DeleteAsync($"/api/v1/roles/{roleId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        Assert.DoesNotContain(await GetRolesAsync(owner), r => r.Id == roleId);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await owner.GetAsync($"/api/v1/roles/{roleId}")).StatusCode);

        // Creating, renaming and retiring are three separate facts on the trail.
        var trail = await GetTrailAsync(owner, roleId);

        Assert.Equal(3, trail.Count);
        Assert.All(trail, e => Assert.Equal(AuditActions.RoleChanged, e.Action));
    }

    [Fact]
    public async Task Setting_Permissions_Replaces_The_Whole_Set()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-permissions");

        var roleId = await CreateRoleAsync(
            owner,
            "OnCall",
            [PermissionNames.IncidentsRead, PermissionNames.IncidentsCreate]);

        var put = await owner.PutAsJsonAsync($"/api/v1/roles/{roleId}/permissions", new
        {
            permissions = new[] { PermissionNames.IncidentsRead },
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var updated = await put.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(updated?.Data);
        Assert.Equal(new[] { PermissionNames.IncidentsRead }, updated!.Data.Permissions.Select(p => p.Name));

        // Read it back: the removal has to be persisted, not just echoed.
        var reloaded = await owner.GetFromJsonAsync<Response<RoleDto>>(
            $"/api/v1/roles/{roleId}",
            ApiAuth.Json);

        Assert.NotNull(reloaded?.Data);
        Assert.Equal(new[] { PermissionNames.IncidentsRead }, reloaded!.Data.Permissions.Select(p => p.Name));

        var trail = await GetTrailAsync(owner, roleId);

        Assert.Contains(trail, e => e.Action == AuditActions.RolePermissionsChanged);
    }

    [Fact]
    public async Task An_Unknown_Permission_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-unknown");

        var create = await owner.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Ghost",
            permissions = new[] { PermissionNames.IncidentsRead, "incidents.teleport" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var body = await create.Content.ReadFromJsonAsync<Response<object?>>(ApiAuth.Json);

        Assert.NotNull(body);
        Assert.False(body!.Succeeded);
        Assert.Contains(body.Errors, e => e.Contains("incidents.teleport", StringComparison.OrdinalIgnoreCase));

        // A typo must not leave a half-built role behind.
        Assert.DoesNotContain(await GetRolesAsync(owner), r => r.Name == "Ghost");
    }

    [Fact]
    public async Task A_Role_Name_Is_Unique_Within_The_Company()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-names");

        await CreateRoleAsync(owner, "Support", []);

        // Case is not a way around the unique index.
        var duplicate = await owner.PostAsJsonAsync("/api/v1/roles", new { name = "support" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // A system role's name is taken by definition.
        var reserved = await owner.PostAsJsonAsync("/api/v1/roles", new { name = SystemRoleNames.Admin });
        Assert.Equal(HttpStatusCode.Conflict, reserved.StatusCode);
    }

    [Fact]
    public async Task System_Roles_Cannot_Be_Renamed_Or_Retired()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-system");

        var engineer = Assert.Single(await GetRolesAsync(owner), r => r.Name == SystemRoleNames.Engineer);

        var rename = await owner.PutAsJsonAsync($"/api/v1/roles/{engineer.Id}", new { name = "Coder" });

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        var delete = await owner.DeleteAsync($"/api/v1/roles/{engineer.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        // Still there, still called Engineer.
        var reloaded = await owner.GetFromJsonAsync<Response<RoleDto>>(
            $"/api/v1/roles/{engineer.Id}",
            ApiAuth.Json);

        Assert.NotNull(reloaded?.Data);
        Assert.Equal(SystemRoleNames.Engineer, reloaded!.Data.Name);
    }

    [Fact]
    public async Task The_Owner_Roles_Permissions_Are_Frozen()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-owner-frozen");

        var ownerRole = Assert.Single(await GetRolesAsync(owner), r => r.IsOwner);

        var put = await owner.PutAsJsonAsync($"/api/v1/roles/{ownerRole.Id}/permissions", new
        {
            permissions = new[] { PermissionNames.IncidentsRead },
        });

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

        var reloaded = await owner.GetFromJsonAsync<Response<RoleDto>>(
            $"/api/v1/roles/{ownerRole.Id}",
            ApiAuth.Json);

        Assert.NotNull(reloaded?.Data);
        Assert.Equal(ownerRole.Permissions.Count, reloaded!.Data.Permissions.Count);
    }

    [Fact]
    public async Task An_Admin_Cannot_Grant_A_Permission_It_Does_Not_Hold()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-escalation");

        // An Admin may manage roles (it holds roles.change) but does not hold tenant.settings.
        var (admin, _) = await ApiAuth.MemberClientAsync(factory, owner, "roles-escalation", "Admin");

        var escalate = await admin.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "AlmostOwner",
            permissions = new[] { PermissionNames.TenantSettings },
        });

        Assert.Equal(HttpStatusCode.Forbidden, escalate.StatusCode);

        // What it does hold, it may delegate.
        var delegated = await admin.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Responder",
            permissions = new[] { PermissionNames.IncidentsRead, PermissionNames.IncidentsComment },
        });

        Assert.Equal(HttpStatusCode.OK, delegated.StatusCode);

        var role = await delegated.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(role?.Data);

        // ... but not extend that role with a permission it lacks.
        var extend = await admin.PutAsJsonAsync($"/api/v1/roles/{role!.Data.Id}/permissions", new
        {
            permissions = new[] { PermissionNames.IncidentsRead, PermissionNames.TenantSettings },
        });

        Assert.Equal(HttpStatusCode.Forbidden, extend.StatusCode);
    }

    [Fact]
    public async Task The_Owner_May_Define_A_Role_With_Any_Permission()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-owner-grant");

        // The Owner is the company's root administrator, so it may hand out anything the
        // catalogue knows -- including permissions the Owner role itself does not carry.
        var create = await owner.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Settings",
            permissions = new[] { PermissionNames.TenantSettings },
        });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var body = await create.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(body?.Data);
        Assert.Equal(
            new[] { PermissionNames.TenantSettings },
            body!.Data.Permissions.Select(p => p.Name));
    }

    [Fact]
    public async Task Reading_Roles_Is_Open_But_Managing_Them_Is_Not()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-permission");
        var (viewer, _) = await ApiAuth.MemberClientAsync(factory, owner, "roles-permission", "Viewer");
        var ownerRole = Assert.Single(await GetRolesAsync(owner), r => r.IsOwner);

        // A Viewer can see what the roles mean...
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/permissions")).StatusCode);

        // ... but cannot change them.
        var create = await viewer.PostAsJsonAsync("/api/v1/roles", new { name = "Nope" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var freeze = await viewer.PutAsJsonAsync($"/api/v1/roles/{ownerRole.Id}/permissions", new
        {
            permissions = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.Forbidden, freeze.StatusCode);
    }

    [Fact]
    public async Task A_Retired_Role_Stops_Being_Granted_But_Its_Holders_Stay_Resolvable()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-retired");

        var roleId = await CreateRoleAsync(owner, "Temp", [PermissionNames.IncidentsRead]);

        // Put a user in it, then try to retire the role — the ERD requires moving them first.
        var email = "temp-holder-roles-retired@ops-pilot.test";

        var createUser = await owner.PostAsJsonAsync("/api/v1/users", new
        {
            email,
            displayName = "Temp Holder",
            password = ApiAuth.Password,
            confirmPassword = ApiAuth.Password,
            roles = new[] { "Temp" },
        });

        Assert.Equal(HttpStatusCode.OK, createUser.StatusCode);

        // The role cannot be retired while users still hold it.
        var deleteWhileHeld = await owner.DeleteAsync($"/api/v1/roles/{roleId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteWhileHeld.StatusCode);

        // Move the user off the role first, then retirement succeeds and the account stays resolvable.
        var usersPaged = await owner.GetFromJsonAsync<PagedResponse<UserDto>>(
            "/api/v1/users?pageSize=100",
            ApiAuth.Json);
        var holder = Assert.Single(usersPaged!.Data!, u => u.Email == email);
        Assert.Contains("Temp", holder.Roles);

        var move = await owner.PutAsJsonAsync(
            $"/api/v1/users/{holder.Id}/roles",
            new { roles = new[] { "Viewer" } });

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await owner.DeleteAsync($"/api/v1/roles/{roleId}")).StatusCode);

        // The account is still there; it no longer holds the retired role.
        var usersAfter = await owner.GetFromJsonAsync<PagedResponse<UserDto>>(
            "/api/v1/users?pageSize=100",
            ApiAuth.Json);

        Assert.NotNull(usersAfter?.Data);
        var holderAfter = Assert.Single(usersAfter!.Data, u => u.Email == email);
        Assert.DoesNotContain("Temp", holderAfter.Roles);
    }

    [Fact]
    public async Task An_Admin_Cannot_Grant_The_Owner_Role()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-owner-granting");
        var (admin, adminId) = await ApiAuth.MemberClientAsync(factory, owner, "roles-owner-granting", "Admin");

        // Creating an account in the Owner role is refused before the account exists.
        var createAsOwner = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = "sneaky-owner-roles-owner-granting@ops-pilot.test",
            displayName = "Sneaky Owner",
            password = ApiAuth.Password,
            confirmPassword = ApiAuth.Password,
            roles = new[] { SystemRoleNames.Owner },
        });

        Assert.Equal(HttpStatusCode.Forbidden, createAsOwner.StatusCode);

        // So is promoting an existing account -- otherwise an Admin holding roles.change could
        // hand itself tenant.settings on its next token.
        var promote = await admin.PutAsJsonAsync(
            $"/api/v1/users/{adminId}/roles",
            new { roles = new[] { SystemRoleNames.Owner } });

        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);

        // The Owner itself may still hand the role out.
        var promoteAsOwner = await owner.PutAsJsonAsync(
            $"/api/v1/users/{adminId}/roles",
            new { roles = new[] { SystemRoleNames.Admin, SystemRoleNames.Owner } });

        Assert.Equal(HttpStatusCode.OK, promoteAsOwner.StatusCode);
    }

    [Fact]
    public async Task User_Reads_Expose_The_Permissions_The_Roles_Grant()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "users-permissions");
        var (_, viewerId) = await ApiAuth.MemberClientAsync(factory, owner, "users-permissions", "Viewer");

        var single = await owner.GetFromJsonAsync<Response<UserDto>>(
            $"/api/v1/users/{viewerId}",
            ApiAuth.Json);

        Assert.NotNull(single?.Data);

        var permissions = single!.Data.Permissions;

        Assert.Contains(PermissionNames.IncidentsRead, permissions);
        Assert.Contains(PermissionNames.AnalyticsRead, permissions);
        Assert.DoesNotContain(PermissionNames.UsersManage, permissions);

        // The dedicated endpoint answers the same question on its own.
        var dedicated = await owner.GetFromJsonAsync<Response<UserPermissionsDto>>(
            $"/api/v1/users/{viewerId}/permissions",
            ApiAuth.Json);

        Assert.NotNull(dedicated?.Data);
        Assert.Equal(viewerId, dedicated!.Data.UserId);
        Assert.Equal(new[] { SystemRoleNames.Viewer }, dedicated.Data.Roles);

        Assert.Equal(
            permissions.OrderBy(p => p, StringComparer.Ordinal),
            dedicated.Data.Permissions.OrderBy(p => p, StringComparer.Ordinal));

        // The list view carries them too, so a client need not fetch each user to show them.
        var paged = await owner.GetFromJsonAsync<PagedResponse<UserDto>>(
            "/api/v1/users?pageSize=100",
            ApiAuth.Json);

        var viewer = Assert.Single(paged!.Data!, u => u.Id == viewerId);

        Assert.Equal(
            permissions.OrderBy(p => p, StringComparer.Ordinal),
            viewer.Permissions.OrderBy(p => p, StringComparer.Ordinal));
    }

    private static async Task<IReadOnlyList<RoleDto>> GetRolesAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<Response<IReadOnlyList<RoleDto>>>(
            "/api/v1/roles",
            ApiAuth.Json);

        Assert.NotNull(body?.Data);

        return body!.Data;
    }

    private static async Task<IReadOnlyList<AuditLogDto>> GetTrailAsync(HttpClient client, Guid roleId)
    {
        var body = await client.GetFromJsonAsync<PagedResponse<AuditLogDto>>(
            $"/api/v1/audit-logs?entityType=Role&entityId={roleId}&pageSize=100",
            ApiAuth.Json);

        Assert.NotNull(body?.Data);

        return body!.Data;
    }

    private static async Task<Guid> CreateRoleAsync(
        HttpClient client,
        string name,
        IReadOnlyList<string> permissions)
    {
        var response = await client.PostAsJsonAsync("/api/v1/roles", new { name, permissions });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response<RoleDto>>(ApiAuth.Json);

        Assert.NotNull(body?.Data);

        return body!.Data.Id;
    }
}
