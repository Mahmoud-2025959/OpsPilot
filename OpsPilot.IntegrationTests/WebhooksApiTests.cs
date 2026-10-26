using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Contracts.Webhooks;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the webhook channel's registration side: registering an endpoint, minting and
/// rotating its secret, and the rules about who may do it.
/// </summary>
/// <remarks>
/// The delivery side is covered by <see cref="WebhookDispatchTests"/>, which drives the
/// dispatcher rather than the HTTP surface.
/// </remarks>
public sealed class WebhooksApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Url = "https://hooks.example.test/ops";

    [Fact]
    public async Task An_Endpoint_Can_Be_Registered_And_Mints_A_Secret()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-register");

        var response = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.True(body!.Succeeded, string.Join(", ", body.Errors));
        Assert.StartsWith("whsec_", body.Data.Secret);
        Assert.Equal(Url, body.Data.Url);
    }

    [Fact]
    public async Task The_Secret_Can_Be_Read_Back()
    {
        // The operator who configured the other end needs the secret again, and a hash cannot
        // be turned back into one — so the stored copy is what makes it recoverable.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-readback");

        var created = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });
        var createdBody = await created.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        var list = await owner.GetFromJsonAsync<PagedResponse<WebhookEndpointDto>>(
            "/api/v1/webhooks",
            Json);

        Assert.NotNull(list?.Data);

        var endpoint = Assert.Single(list.Data);

        Assert.Equal(createdBody!.Data!.Secret, endpoint.Secret);
        Assert.Equal(Url, endpoint.Url);
        Assert.True(endpoint.IsActive);
    }

    [Fact]
    public async Task The_Same_Url_Cannot_Be_Registered_Twice()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-duplicate");

        await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });

        var again = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_Plain_Http_Url_Is_Refused_Unless_It_Is_The_Local_Machine()
    {
        // The signing secret travels with every call, so it must not travel in the clear.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-insecure");

        var refused = await owner.PostAsJsonAsync(
            "/api/v1/webhooks",
            new { url = "http://hooks.example.test/ops" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // A local integration has no use for a certificate, so it is allowed.
        var allowed = await owner.PostAsJsonAsync(
            "/api/v1/webhooks",
            new { url = "http://localhost:5099/ops" });

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task An_Endpoint_Can_Be_Pointed_Somewhere_Else_And_Put_Out_Of_Service()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-update");

        var created = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });
        var createdBody = await created.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        var updated = await owner.PutAsJsonAsync(
            $"/api/v1/webhooks/{createdBody!.Data!.Id}",
            new { url = "https://hooks.example.test/elsewhere", isActive = false });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var body = await updated.Content.ReadFromJsonAsync<Response<WebhookEndpointDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.Equal("https://hooks.example.test/elsewhere", body.Data!.Url);
        Assert.False(body.Data.IsActive);
    }

    [Fact]
    public async Task The_Secret_Can_Be_Rotated()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-rotate");

        var created = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });
        var createdBody = await created.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        var rotated = await owner.PostAsync(
            $"/api/v1/webhooks/{createdBody!.Data!.Id}/rotate-secret",
            null);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);

        var body = await rotated.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        Assert.NotNull(body?.Data);
        Assert.NotEqual(createdBody.Data.Secret, body.Data.Secret);

        // And the new secret is the one that is stored, so the old one is dead.
        var list = await owner.GetFromJsonAsync<PagedResponse<WebhookEndpointDto>>(
            "/api/v1/webhooks",
            Json);

        Assert.NotNull(list?.Data);
        Assert.Equal(body.Data.Secret, Assert.Single(list.Data).Secret);
    }

    [Fact]
    public async Task An_Endpoint_Can_Be_Retired()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-delete");

        var created = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });
        var createdBody = await created.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        var deleted = await owner.DeleteAsync($"/api/v1/webhooks/{createdBody!.Data!.Id}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var read = await owner.GetAsync($"/api/v1/webhooks/{createdBody.Data.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task An_Endpoint_From_Another_Company_Cannot_Be_Read()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-isolation");
        var (stranger, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-isolation-other");

        var created = await owner.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });
        var createdBody = await created.Content.ReadFromJsonAsync<Response<CreatedWebhookDto>>(Json);

        var read = await stranger.GetAsync($"/api/v1/webhooks/{createdBody!.Data!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Registering_An_Endpoint_Is_Not_For_Everybody()
    {
        // Reading the registrations and changing them are two different permissions, and the
        // second one is not given to the people who work incidents.
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "webhook-permissions");
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "webhook-permissions", "Engineer");

        var registered = await engineer.PostAsJsonAsync("/api/v1/webhooks", new { url = Url });

        Assert.Equal(HttpStatusCode.Forbidden, registered.StatusCode);

        // Reading them, on the other hand, is allowed.
        var read = await engineer.GetAsync("/api/v1/webhooks");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }
}
