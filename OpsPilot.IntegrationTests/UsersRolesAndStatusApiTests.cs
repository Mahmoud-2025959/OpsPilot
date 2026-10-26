using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers role assignment and account status over HTTP. Role changes need <c>roles.change</c>
/// rather than <c>users.manage</c>, and both paths have to leave the company with at least one
/// usable Owner.
/// </summary>
public sealed class UsersRolesAndStatusApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Changing_Roles_Replaces_The_Whole_Set()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-replace");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "roles-replace", "Engineer");

        var change = await owner.PutAsJsonAsync(
            $"/api/v1/users/{userId}/roles",
            new { roles = new[] { "Viewer" } });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var payload = await change.Content.ReadFromJsonAsync<Response<UserDto>>(ApiAuth.Json);

        Assert.NotNull(payload?.Data);
        Assert.Equal(new[] { "Viewer" }, payload.Data.Roles);

        // Read it back so the change is proven to be persisted, not just echoed.
        var reloaded = await owner.GetFromJsonAsync<Response<UserDto>>(
            $"/api/v1/users/{userId}",
            ApiAuth.Json);

        Assert.NotNull(reloaded?.Data);
        Assert.Equal(new[] { "Viewer" }, reloaded.Data.Roles);
    }

    [Fact]
    public async Task An_Unknown_Role_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-unknown");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "roles-unknown", "Engineer");

        var change = await owner.PutAsJsonAsync(
            $"/api/v1/users/{userId}/roles",
            new { roles = new[] { "Wizard" } });

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }

    [Fact]
    public async Task Demoting_The_Last_Owner_Is_A_Conflict()
    {
        // A company with no Owner can never be administered again, so the last one keeps the
        // role even when an admin explicitly asks for it to go.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "roles-last-owner");

        var change = await owner.PutAsJsonAsync(
            $"/api/v1/users/{auth.Id}/roles",
            new { roles = new[] { "Engineer" } });

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);

        var payload = await change.Content.ReadFromJsonAsync<Response<UserDto>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.Conflict, payload.Code);
    }

    [Fact]
    public async Task The_Last_Owner_May_Be_Demoted_Once_Another_Owner_Exists()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "roles-two-owners");
        var (_, secondId) = await ApiAuth.MemberClientAsync(factory, owner, "roles-two-owners", "Engineer");

        var promote = await owner.PutAsJsonAsync(
            $"/api/v1/users/{secondId}/roles",
            new { roles = new[] { "Owner" } });

        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var demote = await owner.PutAsJsonAsync(
            $"/api/v1/users/{auth.Id}/roles",
            new { roles = new[] { "Admin" } });

        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);
    }

    [Fact]
    public async Task Deactivating_The_Last_Active_Owner_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "status-last-owner");

        var response = await owner.PatchAsJsonAsync(
            $"/api/v1/users/{auth.Id}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Response<UserDto>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.Conflict, payload.Code);
    }

    [Fact]
    public async Task A_Deactivated_User_Can_No_Longer_Sign_In()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "status-deactivate");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "status-deactivate", "Engineer");

        var email = $"engineer-status-deactivate@ops-pilot.test";

        var deactivate = await owner.PatchAsJsonAsync(
            $"/api/v1/users/{userId}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var payload = await deactivate.Content.ReadFromJsonAsync<Response<UserDto>>(ApiAuth.Json);

        Assert.NotNull(payload?.Data);
        Assert.False(payload.Data.IsActive);

        // Sign-in refuses an inactive account. The plan keeps the failure deliberately vague
        // about why, but the account must not receive a token.
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = email, password = ApiAuth.Password });

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        var denied = await login.Content.ReadFromJsonAsync<Response<AuthResponseDto>>(ApiAuth.Json);

        Assert.NotNull(denied);
        Assert.False(denied.Succeeded);
    }

    [Fact]
    public async Task Reactivating_A_User_Restores_Sign_In()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "status-reactivate");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "status-reactivate", "Engineer");

        var email = "engineer-status-reactivate@ops-pilot.test";

        await owner.PatchAsJsonAsync($"/api/v1/users/{userId}/status", new { isActive = false });

        var reactivate = await owner.PatchAsJsonAsync(
            $"/api/v1/users/{userId}/status",
            new { isActive = true });

        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = email, password = ApiAuth.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Changing_The_Roles_Of_An_Unknown_User_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "roles-missing");

        var change = await owner.PutAsJsonAsync(
            $"/api/v1/users/{Guid.NewGuid()}/roles",
            new { roles = new[] { "Viewer" } });

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
    }

    [Fact]
    public async Task Deleting_The_Last_Active_Owner_Is_A_Conflict()
    {
        // Deletion is a soft delete, so it deactivates the account. It therefore has to honour
        // the same last-Owner rule as the status endpoint.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "delete-last-owner");

        var response = await owner.DeleteAsync($"/api/v1/users/{auth.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Response<UserDto>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.Conflict, payload.Code);

        // The account is untouched: the delete was refused, not applied.
        var reloaded = await owner.GetFromJsonAsync<Response<UserDto>>(
            $"/api/v1/users/{auth.Id}",
            ApiAuth.Json);

        Assert.True(reloaded?.Data?.IsActive);
    }

    [Fact]
    public async Task Concurrent_Demotions_Cannot_Leave_The_Company_Without_An_Owner()
    {
        // Two Owners, each demoted by a simultaneous request. "At least one Owner" spans rows,
        // so a check-then-act would let both through and leave the company unadministerable.
        // The per-tenant guard lock serialises them, so the second request sees the first's
        // committed change and refuses: exactly one demotion succeeds.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "owners-race");
        var (_, secondId) = await ApiAuth.MemberClientAsync(factory, owner, "owners-race", "Engineer");

        var promote = await owner.PutAsJsonAsync(
            $"/api/v1/users/{secondId}/roles",
            new { roles = new[] { "Owner" } });

        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var demoteFirst = owner.PutAsJsonAsync(
            $"/api/v1/users/{auth.Id}/roles",
            new { roles = new[] { "Admin" } });

        var demoteSecond = owner.PutAsJsonAsync(
            $"/api/v1/users/{secondId}/roles",
            new { roles = new[] { "Engineer" } });

        var responses = await Task.WhenAll(demoteFirst, demoteSecond);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
