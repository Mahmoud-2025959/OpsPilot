using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers issuing, listing, reading and revoking ingestion keys over HTTP, including the thing
/// that makes them usable again after they were lost: the secret is stored encrypted and read
/// back through the list.
/// </summary>
public sealed class ApiKeysApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Created_Key_Can_Be_Read_Back_From_The_List()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "key-readback");

        var create = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "ci-pipeline" });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        Assert.True(created!.Succeeded, string.Join(", ", created.Errors));

        var key = created.Data;

        Assert.StartsWith("op_live_", key.Key);
        Assert.Equal(key.Key[..16], key.KeyPrefix);

        // The list is where an operator reads a key back after losing it, so it carries the
        // secret rather than only the prefix.
        var list = await client.GetAsync("/api/v1/api-keys");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var listed = await list.Content.ReadFromJsonAsync<PagedResponse<ApiKeyDto>>(ApiAuth.Json);

        Assert.NotNull(listed?.Data);

        var dto = Assert.Single(listed!.Data);
        Assert.Equal(key.Id, dto.Id);
        Assert.Equal(key.KeyPrefix, dto.KeyPrefix);
        Assert.Equal(key.Key, dto.Key);
        Assert.Equal("Active", dto.Status);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task A_Single_Key_Can_Be_Read_Back()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "key-read-one");

        var create = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "one-key" });
        var key = (await create.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        var read = await client.GetAsync($"/api/v1/api-keys/{key.Id}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var dto = (await read.Content.ReadFromJsonAsync<Response<ApiKeyDto>>(ApiAuth.Json))!.Data!;

        Assert.Equal(key.Id, dto.Id);
        Assert.Equal(key.Key, dto.Key);
    }

    [Fact]
    public async Task Rotating_A_Key_Issues_A_New_Secret_And_Retires_The_Old_One()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "key-rotate");

        // A registered service, so the only thing that can reject the call is the key itself.
        var service = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });
        Assert.Equal(HttpStatusCode.OK, service.StatusCode);

        var create = await owner.PostAsJsonAsync("/api/v1/api-keys", new { name = "to-rotate" });
        var key = (await create.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        var ingest = factory.CreateClient();
        ingest.DefaultRequestHeaders.Add("X-API-Key", key.Key);

        var before = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "heartbeat" });
        Assert.Equal(HttpStatusCode.Accepted, before.StatusCode);

        var rotate = await owner.PostAsync($"/api/v1/api-keys/{key.Id}/rotate", null);

        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);

        var rotated = (await rotate.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        Assert.NotEqual(key.Key, rotated.Key);
        Assert.Equal(key.Id, rotated.Id);

        // The old secret is refused from the moment the rotation is saved...
        var after = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "heartbeat" });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);

        // ...and the new one is what the list now shows.
        var list = await owner.GetAsync("/api/v1/api-keys");
        var listed = await list.Content.ReadFromJsonAsync<PagedResponse<ApiKeyDto>>(ApiAuth.Json);

        Assert.NotNull(listed?.Data);

        var dto = Assert.Single(listed!.Data);
        Assert.Equal(rotated.Key, dto.Key);

        var fresh = factory.CreateClient();
        fresh.DefaultRequestHeaders.Add("X-API-Key", rotated.Key);

        var accepted = await fresh.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "heartbeat" });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task A_Key_With_No_Stored_Secret_Reads_Back_As_Null_Until_It_Is_Rotated()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "key-legacy");

        var create = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "legacy" });
        var key = (await create.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        // A key issued before the secret began being stored: the hash is there, the secret is
        // not, and no hash can be reversed into the secret it was made from.
        await ClearStoredSecretAsync(key.Id);

        var list = await client.GetAsync("/api/v1/api-keys");
        var listed = await list.Content.ReadFromJsonAsync<PagedResponse<ApiKeyDto>>(ApiAuth.Json);

        Assert.NotNull(listed?.Data);

        var dto = Assert.Single(listed!.Data);
        Assert.Null(dto.Key);

        // Rotation is the way out: it puts a readable secret on the same row.
        var rotate = await client.PostAsync($"/api/v1/api-keys/{key.Id}/rotate", null);

        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);

        var rotated = (await rotate.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;
        Assert.NotNull(rotated.Key);
        Assert.StartsWith("op_live_", rotated.Key);
    }

    [Fact]
    public async Task Two_Keys_With_The_Same_Name_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "key-dup");

        var first = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "monitoring" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/api-keys", new { name = "monitoring" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_Expiry_In_The_Past_Is_A_Validation_Error()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "key-past");

        var create = await client.PostAsJsonAsync(
            "/api/v1/api-keys",
            new { name = "dud", expiresAt = DateTimeOffset.UtcNow.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task A_Revoked_Key_Is_Rejected_At_The_Door()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "key-revoke");

        // A registered service, so the only thing that can reject the call is the key itself.
        var service = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });
        Assert.Equal(HttpStatusCode.OK, service.StatusCode);

        var create = await owner.PostAsJsonAsync("/api/v1/api-keys", new { name = "to-revoke" });
        var key = (await create.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        var ingest = factory.CreateClient();
        ingest.DefaultRequestHeaders.Add("X-API-Key", key.Key);

        // It works while the key is active...
        var before = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "heartbeat" });
        Assert.Equal(HttpStatusCode.Accepted, before.StatusCode);

        var revoke = await owner.DeleteAsync($"/api/v1/api-keys/{key.Id}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        // ...and is refused as soon as it is revoked.
        var after = await ingest.PostAsJsonAsync(
            "/api/v1/events",
            new { service = "Payments", environment = "Production", severity = "High", message = "heartbeat" });

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Listing_Keys_Requires_The_Manage_Permission()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "key-perm");

        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "key-perm", "Engineer");

        var list = await engineer.GetAsync("/api/v1/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    /// <summary>
    /// Drops the stored secret of a key, which is what a row created before the column existed
    /// looks like.
    /// </summary>
    private async Task ClearStoredSecretAsync(Guid keyId)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "UPDATE ApiKeys SET KeyCiphertext = NULL WHERE Id = @id", connection);

        command.Parameters.AddWithValue("@id", keyId);

        await command.ExecuteNonQueryAsync();
    }
}
