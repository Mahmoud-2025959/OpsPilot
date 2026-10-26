using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Messages;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Infrastructure.Integration;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the inbound half of the broker integration: a message that arrives is applied once,
/// a redelivery of the same message is not applied twice, and a message that fails leaves
/// nothing behind that would stop it from being tried again (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// The broker itself is not part of these tests: the consumer's job is to take a message and
/// apply it, and that is what is exercised here. The transport around it is configuration.
/// </remarks>
public sealed class IntegrationEventConsumerTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task An_Event_That_Arrives_Over_The_Broker_Creates_An_Incident()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth, key) = await ArrangeAsync(factory, "broker-ingest");

        var outcome = await ProcessAsync(
            factory,
            auth.TenantId,
            "message-1",
            Envelope(auth.TenantId, key.Id, "Payments", "Queue depth above 90%"));

        Assert.Equal(IntegrationEventOutcome.Handled, outcome);

        var incidents = await owner.GetFromJsonAsync<PagedResponse<IncidentDto>>(
            "/api/v1/incidents",
            ApiAuth.Json);

        Assert.NotNull(incidents?.Data);
        Assert.Single(incidents!.Data);
        Assert.Contains("Queue depth", incidents.Data[0].Title);
    }

    [Fact]
    public async Task The_Same_Message_Twice_Is_Applied_Once()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth, key) = await ArrangeAsync(factory, "broker-duplicate");

        var body = Envelope(auth.TenantId, key.Id, "Payments", "Queue depth above 90%");

        Assert.Equal(
            IntegrationEventOutcome.Handled,
            await ProcessAsync(factory, auth.TenantId, "message-2", body));

        // The broker promises at-least-once delivery, so the same message can arrive twice.
        Assert.Equal(
            IntegrationEventOutcome.Duplicate,
            await ProcessAsync(factory, auth.TenantId, "message-2", body));

        var incidents = await owner.GetFromJsonAsync<PagedResponse<IncidentDto>>(
            "/api/v1/incidents",
            ApiAuth.Json);

        Assert.NotNull(incidents?.Data);
        Assert.Single(incidents!.Data);
    }

    [Fact]
    public async Task An_Event_Nobody_Answers_Is_Recorded_And_Skipped()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth, key) = await ArrangeAsync(factory, "broker-unknown");

        var unknown = new
        {
            tenantId = auth.TenantId,
            type = "SomethingNobodyHandles",
            payload = new { apiKeyId = key.Id }
        };

        Assert.Equal(
            IntegrationEventOutcome.NoHandler,
            await ProcessAsync(factory, auth.TenantId, "message-3", unknown));

        // Nothing was applied, and the message was not left in the queue either: an event
        // nobody answers is recorded, not retried forever.
        var incidents = await owner.GetFromJsonAsync<PagedResponse<IncidentDto>>(
            "/api/v1/incidents",
            ApiAuth.Json);

        Assert.NotNull(incidents?.Data);
        Assert.Empty(incidents!.Data);
    }

    [Fact]
    public async Task An_Event_Carrying_A_Key_From_Another_Company_Is_Refused()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (_, auth, _) = await ArrangeAsync(factory, "broker-foreign-key");

        // A second company, with a key of its own.
        var (_, otherAuth, otherKey) = await ArrangeAsync(factory, "broker-foreign-key-other");

        Assert.NotEqual(auth.TenantId, otherAuth.TenantId);

        var body = Envelope(auth.TenantId, otherKey.Id, "Payments", "Queue depth above 90%");

        // The key does not belong to the company the envelope names, so it is refused: the
        // company is the envelope's, and the key has to agree with it.
        await Assert.ThrowsAnyAsync<Exception>(
            () => ProcessAsync(factory, auth.TenantId, "message-4", body));

        // And because the claim in the ledger was rolled back with the work, the message is
        // still free to be tried again rather than being mistaken for a duplicate.
        await Assert.ThrowsAnyAsync<Exception>(
            () => ProcessAsync(factory, auth.TenantId, "message-4", body));
    }

    [Fact]
    public async Task A_Message_That_Cannot_Be_Read_Is_Not_Applied()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _, _) = await ArrangeAsync(factory, "broker-malformed");

        Assert.False(IntegrationEventMessage.TryParse("not json at all", out _));
        Assert.False(IntegrationEventMessage.TryParse("{}", out _));
        Assert.False(IntegrationEventMessage.TryParse(
            """{"tenantId":"00000000-0000-0000-0000-000000000000","type":"EventIngested"}""",
            out _));

        var incidents = await owner.GetFromJsonAsync<PagedResponse<IncidentDto>>(
            "/api/v1/incidents",
            ApiAuth.Json);

        Assert.NotNull(incidents?.Data);
        Assert.Empty(incidents!.Data);
    }

    /// <summary>
    /// Registers a company, a service and a key, and returns the key the publisher will act
    /// under.
    /// </summary>
    private static async Task<(HttpClient Owner, AuthResponseDto Auth, CreatedApiKeyDto Key)>
        ArrangeAsync(IntegrationApiFactory factory, string slug)
    {
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, slug);

        var service = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name = "Payments", environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, service.StatusCode);

        var keyResponse = await owner.PostAsJsonAsync("/api/v1/api-keys", new { name = "metrics-agent" });

        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);

        var key = (await keyResponse.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!.Data!;

        return (owner, auth, key);
    }

    /// <summary>
    /// Hands one message to the processor, in a scope of its own with the company entered
    /// first — the way the consumer does it.
    /// </summary>
    private static async Task<IntegrationEventOutcome> ProcessAsync(
        IntegrationApiFactory factory,
        Guid tenantId,
        string messageId,
        object body)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();

        using var entered = tenant.Enter(tenantId);

        var processor = scope.ServiceProvider.GetRequiredService<IntegrationEventProcessor>();

        var json = JsonSerializer.Serialize(body, ApiAuth.Json);

        Assert.True(IntegrationEventMessage.TryParse(json, out var message));
        Assert.NotNull(message);

        return await processor.ProcessAsync(messageId, message!, default);
    }

    /// <summary>The envelope a publisher sends: who it is about, what it is, and the body.</summary>
    private static object Envelope(
        Guid tenantId,
        Guid apiKeyId,
        string service,
        string message) => new
        {
            tenantId,
            type = IntegrationEventTypes.EventIngested,
            payload = new
            {
                apiKeyId,
                service,
                environment = "Production",
                severity = "Critical",
                message,
                occurredAt = (DateTimeOffset?)null
            }
        };
}
