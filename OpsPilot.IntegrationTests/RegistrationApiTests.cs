using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.Persistence;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Drives sign-up over HTTP against the real pipeline, running against the throwaway database.
/// This is the only test that proves the original blockers in one pass: registration has to
/// create the company and its Owner, and the token it hands back has to resolve a tenant for
/// the next request so any permission-gated endpoint answers 200 instead of 401.
/// </summary>
public sealed class RegistrationApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Email = "e2e-owner@ops-pilot.test";
    private const string Password = "Sup3rSecret!";

    [Fact]
    public async Task RegisterTenant_CreatesCompanyAndOwner_AndTokenResolvesTenant()
    {
        await using var factory = new IntegrationApiFactory(db);
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register-tenant",
            new
            {
                companyName = "Globex E2E",
                slug = "globex-e2e",
                email = Email,
                displayName = "E2E Owner",
                password = Password,
                confirmPassword = Password,
            });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<Response<AuthResponseDto>>(Json);

        Assert.NotNull(auth?.Data);
        Assert.True(auth!.Succeeded, string.Join(", ", auth.Errors));

        var tenantId = auth.Data.TenantId;

        Assert.NotEqual(Guid.Empty, tenantId);
        Assert.Contains(SystemRoleNames.Owner, auth.Data.Roles);
        Assert.Contains(PermissionNames.IncidentsCreate, auth.Data.Permissions);
        Assert.DoesNotContain(PermissionNames.EventsIngest, auth.Data.Permissions);
        Assert.False(string.IsNullOrWhiteSpace(auth.Data.AccessToken));

        // The account must have landed in the throwaway database, not the developer's own.
        await using var write = db.CreateContext(new TenantContext());
        Assert.True(await write.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == Email));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Data.AccessToken);

        // Every claim-backed lookup below depends on TenantResolutionMiddleware: without it
        // the query filter sees an empty tenant id and returns nothing but 404/401.
        var me = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var mePayload = await me.Content.ReadFromJsonAsync<Response<CurrentUserDto>>(Json);

        Assert.NotNull(mePayload?.Data);
        Assert.Equal(auth.Data.Id, mePayload.Data.Id);
        Assert.Equal(tenantId, mePayload.Data.TenantId);

        var incidents = await client.GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.OK, incidents.StatusCode);

        // The same policies must reject an anonymous caller.
        using var anonymous = factory.CreateClient();
        var denied = await anonymous.GetAsync("/api/v1/incidents");

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
}
