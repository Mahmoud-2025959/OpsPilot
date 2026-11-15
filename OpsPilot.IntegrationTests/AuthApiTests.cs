using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the token-lifetime rules that only hold over the real pipeline: rotation is
/// single-use, a bearer stops working the moment its account is disabled, and an unknown refresh
/// token is refused as a credential rather than reported as a missing entity.
/// </summary>
public sealed class AuthApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Reusing_A_Refresh_Token_Revokes_The_Whole_Chain()
    {
        await using var factory = new IntegrationApiFactory(db);
        var client = factory.CreateClient();
        var owner = await ApiAuth.RegisterOwnerAsync(client, "auth-reuse");

        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = owner.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var rotated = await first.Content.ReadFromJsonAsync<Response<RefreshTokenResponseDto>>(ApiAuth.Json);

        Assert.NotNull(rotated?.Data);

        // The token that was just exchanged is spent, so presenting it again is a replay.
        var replay = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = owner.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var payload = await replay.Content.ReadFromJsonAsync<Response<object>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Contains(payload.Errors, e => e.Contains("reuse", StringComparison.OrdinalIgnoreCase));

        // Detecting the replay revokes the whole chain, so the token minted moments ago is dead
        // as well -- a stolen token is not allowed to keep a session alive.
        var afterRevocation = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = rotated!.Data!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task Concurrent_Refreshes_Issue_Exactly_One_Replacement()
    {
        // Two requests present the same refresh token at the same time. Single use has to be
        // decided by the database: a read-then-write would let both through and quietly create
        // two live sessions from one token, which also blinds the replay detection below.
        await using var factory = new IntegrationApiFactory(db);
        var client = factory.CreateClient();
        var owner = await ApiAuth.RegisterOwnerAsync(client, "auth-race");

        var first = client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = owner.RefreshToken });

        var second = client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = owner.RefreshToken });

        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Logging_Out_With_An_Unknown_Token_Is_Unauthorized()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "auth-logout-unknown");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Response<object>>(ApiAuth.Json);

        Assert.NotNull(payload);

        // The message must name the problem, not report an entity with the empty-guid sentinel.
        Assert.DoesNotContain(payload.Errors, e => e.Contains(Guid.Empty.ToString()));
    }

    [Fact]
    public async Task A_Bearer_Stops_Working_As_Soon_As_The_Account_Is_Deactivated()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "auth-deactivate");
        var (member, userId) = await ApiAuth.MemberClientAsync(factory, owner, "auth-deactivate", "Engineer");

        // The token is accepted while the account is active...
        var whileActive = await member.GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.OK, whileActive.StatusCode);

        var deactivate = await owner.PatchAsJsonAsync(
            $"/api/v1/users/{userId}/status",
            new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // ...and refused on the very next request, rather than remaining usable until it lapses.
        var whileInactive = await member.GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, whileInactive.StatusCode);
    }
}
