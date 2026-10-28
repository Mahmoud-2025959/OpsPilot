using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Events.Commands.IngestEvent;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Infrastructure.BackgroundJobs;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Security;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the webhook channel's delivery side: an announced event leaves a call waiting, a
/// pass turns that call into a signed request, and an endpoint that refuses earns its retries
/// until the row is parked.
/// </summary>
/// <remarks>
/// Assertions are row-scoped — matched on this test's own endpoint — because every test in the
/// class shares one database and the dispatcher necessarily walks every company in it.
/// </remarks>
public sealed class WebhookDispatchTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    private const string Url = "https://hooks.example.test/ops";

    [Fact]
    public async Task An_Announced_Event_Leaves_A_Call_Waiting_For_Every_Endpoint()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        await IngestAsync(company);

        var deliveries = await company.Context.WebhookDeliveries
            .Where(d => d.EndpointId == endpoint.Id)
            .ToListAsync();

        var delivery = Assert.Single(deliveries);

        Assert.Equal(IntegrationEventTypes.IncidentCreated, delivery.EventType);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.Attempts);
        Assert.Null(delivery.DeliveredAt);
    }

    [Fact]
    public async Task A_Queued_Call_Is_Delivered_And_Marked_Delivered()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        await IngestAsync(company);

        var sender = new RecordingWebhookSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider);
        var ourDelivery = await company.Context.WebhookDeliveries
            .Where(d => d.EndpointId == endpoint.Id)
            .OrderByDescending(d => d.CreatedAt)
            .FirstAsync();

        var delivered = await worker.DispatchPendingAsync();

        Assert.True(delivered >= 1);

        var call = Assert.Single(sender.Sent, c => c.DeliveryId == ourDelivery.Id.ToString());

        Assert.Equal(Url, call.Url);
        Assert.Equal(IntegrationEventTypes.IncidentCreated, call.EventType);

        var reloaded = await ReloadAsync(company, endpoint.Id);

        Assert.Equal(WebhookDeliveryStatus.Delivered, reloaded.Status);
        Assert.NotNull(reloaded.DeliveredAt);
        Assert.Null(reloaded.LastError);
    }

    [Fact]
    public async Task The_Call_Carries_The_Endpoint_Signing_Secret()
    {
        // The secret is stored encrypted and read back at delivery time, because a hash cannot
        // sign anything.
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        await IngestAsync(company);

        var sender = new RecordingWebhookSender();
        await using var provider = BuildContainer(sender);

        await BuildWorker(provider).DispatchPendingAsync();

        var call = Assert.Single(sender.Sent, c => c.Url == Url);

        Assert.NotEmpty(call.Secret);
        Assert.StartsWith("whsec_", call.Secret);
    }

    [Fact]
    public async Task The_Call_Body_Is_The_Envelope()
    {
        using var company = await db.CompanyAsync();
        await RegisterAsync(company, Url);

        await IngestAsync(company);

        var sender = new RecordingWebhookSender();
        await using var provider = BuildContainer(sender);

        await BuildWorker(provider).DispatchPendingAsync();

        var call = Assert.Single(sender.Sent, c => c.Url == Url);

        using var document = System.Text.Json.JsonDocument.Parse(call.Body);

        Assert.Equal(
            IntegrationEventTypes.IncidentCreated,
            document.RootElement.GetProperty("event").GetString());
        Assert.Equal(
            company.TenantId.ToString(),
            document.RootElement.GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task A_Refusing_Endpoint_Retries_Until_The_Call_Is_Parked()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        await IngestAsync(company);

        var sender = new FailingWebhookSender();
        await using var provider = BuildContainer(sender);

        var worker = BuildWorker(provider, maxAttempts: 3);

        await worker.DispatchPendingAsync();
        await worker.DispatchPendingAsync();

        // Two attempts used up and still queued: a single failure is not the end of a row.
        var halfway = await ReloadAsync(company, endpoint.Id);

        Assert.Equal(WebhookDeliveryStatus.Pending, halfway.Status);
        Assert.Equal(2, halfway.Attempts);
        Assert.Equal("The endpoint refused the call.", halfway.LastError);

        await worker.DispatchPendingAsync();

        var parked = await ReloadAsync(company, endpoint.Id);

        Assert.Equal(WebhookDeliveryStatus.Failed, parked.Status);
        Assert.Equal(3, parked.Attempts);

        // A parked row stays parked: further passes leave it and its attempt count alone.
        await worker.DispatchPendingAsync();

        var untouched = await ReloadAsync(company, endpoint.Id);

        Assert.Equal(WebhookDeliveryStatus.Failed, untouched.Status);
        Assert.Equal(3, untouched.Attempts);
    }

    [Fact]
    public async Task A_Refusal_That_Will_Keep_Happening_Earns_No_More_Attempts()
    {
        // A 4xx will be answered the same way next time, so retrying it is only a way of
        // filling the queue with work that cannot succeed.
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        await IngestAsync(company);

        var sender = new FailingWebhookSender(permanent: true);
        await using var provider = BuildContainer(sender);

        await BuildWorker(provider, maxAttempts: 5).DispatchPendingAsync();

        var parked = await ReloadAsync(company, endpoint.Id);

        Assert.Equal(WebhookDeliveryStatus.Failed, parked.Status);
        Assert.Equal(1, parked.Attempts);
    }

    [Fact]
    public async Task An_Endpoint_That_Is_Out_Of_Service_Is_Not_Called()
    {
        using var company = await db.CompanyAsync();
        var endpoint = await RegisterAsync(company, Url);

        endpoint.Deactivate();
        company.Context.WebhookEndpoints.Update(endpoint);
        await company.Context.SaveChangesAsync();

        await IngestAsync(company);

        var sender = new RecordingWebhookSender();
        await using var provider = BuildContainer(sender);

        await BuildWorker(provider).DispatchPendingAsync();

        Assert.DoesNotContain(sender.Sent, c => c.Url == Url);
    }

    private static async Task<WebhookEndpoint> RegisterAsync(CompanyScope company, string url)
    {
        var endpoint = WebhookEndpoint.Create(
            company.TenantId,
            url,
            new string('a', 64),
            Protector().Protect("whsec_test_secret"));

        await company.Context.WebhookEndpoints.AddAsync(endpoint);
        await company.Context.SaveChangesAsync();

        return endpoint;
    }

    private static async Task IngestAsync(CompanyScope company)
    {
        var apiKey = ApiKey.Create(
            company.TenantId,
            "Test ingester",
            $"op_{Guid.NewGuid():N}"[..16],
            new string('a', 64),
            DateTimeOffset.UtcNow.AddDays(90));

        await company.Context.ApiKeys.AddAsync(apiKey);
        await company.Context.SaveChangesAsync();

        var handler = new IngestEventCommandHandler(
            new ServiceRepository(company.Context),
            new IncidentRepository(company.Context, new RecordingIncidentNotifier()),
            new SlaPolicyRepository(company.Context),
            TimeProvider.System);

        await handler.Handle(
            new IngestEventCommand(
                company.TenantId,
                apiKey.Id,
                company.Company.User.Id,
                "Payments",
                EnvironmentType.Production,
                Severity.High,
                "p99 latency above threshold"),
            default);
    }

    /// <summary>Reads the row back through a context of its own, so nothing is served from the cache.</summary>
    private async Task<WebhookDelivery> ReloadAsync(CompanyScope company, Guid endpointId)
    {
        await using var context = db.CreateContext(company.Tenants);

        return await context.WebhookDeliveries
            .Where(d => d.EndpointId == endpointId)
            .OrderBy(d => d.CreatedAt)
            .FirstAsync();
    }

    private ServiceProvider BuildContainer(IWebhookSender sender) =>
        new ServiceCollection()
            .AddScoped<TenantContext>()
            .AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<TenantContext>())
            .AddScoped(provider => db.CreateContext(provider.GetRequiredService<TenantContext>()))
            .AddScoped<IWebhookRepository, WebhookRepository>()
            .AddScoped<ITenantDirectory, TenantDirectory>()
            .AddSingleton<ISecretProtector>(_ => Protector())
            .AddSingleton(sender)
            .BuildServiceProvider();

    private static AesGcmApiKeySecretProtector Protector() =>
        new(Options.Create(new ApiKeyEncryptionOptions
        {
            Key = "KioqKioqKioqKioqKioqKioqKioqKioqKioqKioqKio="
        }));

    private static WebhookDispatchWorker BuildWorker(
        IServiceProvider provider,
        int maxAttempts = 5) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            Options.Create(new WebhookDispatchOptions
            {
                BatchSize = 50,
                MaxAttempts = maxAttempts
            }),
            NullLogger<WebhookDispatchWorker>.Instance);
}
