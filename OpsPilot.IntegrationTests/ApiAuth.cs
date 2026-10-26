using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Signs a company up over HTTP and returns a client already carrying the Owner's token.
/// </summary>
/// <remarks>
/// The HTTP-backed classes share this so that "who am I and what may I do" is established the
/// same way a person would establish it, through <c>/auth/register-tenant</c> and the tenant
/// resolution that the resulting bearer token drives.
/// </remarks>
internal static class ApiAuth
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal const string Password = "Sup3rSecret!";

    internal static async Task<AuthResponseDto> RegisterOwnerAsync(
        HttpClient client,
        string slug,
        string? displayName = null)
    {
        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register-tenant",
            new
            {
                companyName = slug,
                slug,
                email = $"owner-{slug}@ops-pilot.test",
                displayName = displayName ?? "Test Owner",
                password = Password,
                confirmPassword = Password,
            });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<Response<AuthResponseDto>>(Json);

        Assert.NotNull(auth?.Data);
        Assert.True(auth!.Succeeded, string.Join(", ", auth.Errors));

        return auth.Data;
    }

    /// <summary>Registers a company and returns a client authenticated as its Owner.</summary>
    internal static async Task<(HttpClient Client, AuthResponseDto Owner)> OwnerClientAsync(
        IntegrationApiFactory factory,
        string slug)
    {
        var client = factory.CreateClient();
        var owner = await RegisterOwnerAsync(client, slug);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        return (client, owner);
    }

    /// <summary>
    /// Creates a second user in the company through the admin endpoint and signs them in, so a
    /// test can act as somebody whose permissions differ from the Owner's.
    /// </summary>
    internal static async Task<(HttpClient Client, Guid UserId)> MemberClientAsync(
        IntegrationApiFactory factory,
        HttpClient ownerClient,
        string slug,
        string role)
    {
        var email = $"{role.ToLowerInvariant()}-{slug}@ops-pilot.test";

        var create = await ownerClient.PostAsJsonAsync(
            "/api/v1/users",
            new
            {
                email,
                displayName = $"{role} {slug}",
                password = Password,
                confirmPassword = Password,
                roles = new[] { role },
            });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(Json);

        Assert.NotNull(created?.Data);

        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = await login.Content.ReadFromJsonAsync<Response<AuthResponseDto>>(Json);

        Assert.NotNull(auth?.Data);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Data.AccessToken);

        return (client, created.Data);
    }
}
