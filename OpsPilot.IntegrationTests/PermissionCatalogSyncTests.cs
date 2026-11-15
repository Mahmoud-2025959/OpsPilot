using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Contracts.Roles;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the startup pass that brings an existing installation up to the permission set the
/// build defines (ERD §3, the catalogue that is otherwise only seeded at provisioning time).
/// </summary>
/// <remarks>
/// The case that matters is an installation that predates a permission: provisioning already
/// seeded whatever existed then, and it never runs again. Without this pass a new permission
/// would be understood by the policies and held by nobody in every company that already
/// exists — including the Owner, whose set is deliberately closed to callers.
/// </remarks>
public sealed class PermissionCatalogSyncTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_Permission_Removed_From_A_System_Role_Is_Restored()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "perm-sync-role");

        var engineer = Assert.Single(
            await GetRolesAsync(owner),
            r => r.Name == SystemRoleNames.Engineer);

        // A narrowed role is the shape the regression takes in practice: the row must come back.
        var narrowed = engineer.Permissions
            .Select(p => p.Name)
            .Where(name => name != PermissionNames.IncidentsResolve)
            .ToArray();

        var put = await owner.PutAsJsonAsync(
            $"/api/v1/roles/{engineer.Id}/permissions",
            new { permissions = narrowed });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var afterNarrowing = Assert.Single(
            await GetRolesAsync(owner),
            r => r.Id == engineer.Id);

        Assert.DoesNotContain(
            afterNarrowing.Permissions,
            p => p.Name == PermissionNames.IncidentsResolve);

        var granted = await factory.Services
            .GetRequiredService<PermissionCatalogSynchronizer>()
            .SynchronizeAsync();

        Assert.True(granted > 0);

        var afterSync = Assert.Single(
            await GetRolesAsync(owner),
            r => r.Id == engineer.Id);

        Assert.Contains(afterSync.Permissions, p => p.Name == PermissionNames.IncidentsResolve);
    }

    [Fact]
    public async Task The_Owner_Is_Caught_Up_With_A_Permission_It_Lacks()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "perm-sync-owner");

        var ownerRole = Assert.Single(await GetRolesAsync(owner), r => r.IsOwner);

        // The Owner's set is frozen against the API by design, so the gap a deployment leaves
        // behind is produced at the database instead: the role is missing a permission the
        // catalogue defines.
        var tenants = new TenantContext();
        using var entered = tenants.Enter(ownerRole.TenantId);
        await using var context = db.CreateContext(tenants);

        var removed = await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM RolePermissions WHERE RoleId = {0} AND PermissionsId = " +
            "(SELECT Id FROM Permissions WHERE Name = {1})",
            ownerRole.Id,
            PermissionNames.IncidentsRead);

        Assert.Equal(1, removed);

        var granted = await factory.Services
            .GetRequiredService<PermissionCatalogSynchronizer>()
            .SynchronizeAsync();

        Assert.True(granted > 0);

        var afterSync = Assert.Single(await GetRolesAsync(owner), r => r.IsOwner);

        Assert.Contains(afterSync.Permissions, p => p.Name == PermissionNames.IncidentsRead);

        // Still not the ingestion permission: catching up must not widen the Owner past the
        // defaults the role was created from.
        Assert.DoesNotContain(afterSync.Permissions, p => p.Name == PermissionNames.EventsIngest);
    }

    [Fact]
    public async Task A_Custom_Role_Is_Left_Alone()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "perm-sync-custom");

        var create = await owner.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "OnCall",
            permissions = new[] { PermissionNames.IncidentsRead },
        });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        await factory.Services
            .GetRequiredService<PermissionCatalogSynchronizer>()
            .SynchronizeAsync();

        var custom = Assert.Single(await GetRolesAsync(owner), r => r.Name == "OnCall");

        // There is no default for a custom role to be brought up to, so its set is untouched.
        Assert.Equal(
            new[] { PermissionNames.IncidentsRead },
            custom.Permissions.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static async Task<IReadOnlyList<RoleDto>> GetRolesAsync(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<Response<IReadOnlyList<RoleDto>>>(
            "/api/v1/roles",
            ApiAuth.Json);

        Assert.NotNull(body?.Data);

        return body!.Data;
    }
}
