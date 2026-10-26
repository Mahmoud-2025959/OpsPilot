using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the plan's ingestion limit (UC-06): a fixed window per API key, answered past it with
/// a 429 problem, while the bearer-token surface is left alone.
/// </summary>
/// <remarks>
/// The window is shrunk to a permit or two through the factory — the rule under test is who gets
/// their own bucket and what the refusal looks like, not the plan's figure of 100.
/// </remarks>
public sealed class RateLimitingTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_Key_Over_The_Window_Is_Refused_With_A_429_Problem()
    {
        await using var factory = new IntegrationApiFactory(db, ingestionPermitLimit: 2);
        var owner = await ArrangeAsync(factory, "rl-window");
        var ingest = await KeyClientAsync(factory, owner, "metrics-agent");

        Assert.Equal(HttpStatusCode.Accepted, (await IngestAsync(ingest)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await IngestAsync(ingest)).StatusCode);

        var refused = await IngestAsync(ingest);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var problem = await refused.Content.ReadFromJsonAsync<ProblemDetails>(ApiAuth.Json);

        Assert.Equal(StatusCodes.Status429TooManyRequests, problem!.Status);
        Assert.Equal("Too many requests", problem.Title);
        Assert.NotNull(problem.Detail);
    }

    [Fact]
    public async Task A_Second_Key_Keeps_Its_Own_Window()
    {
        await using var factory = new IntegrationApiFactory(db, ingestionPermitLimit: 1);
        var owner = await ArrangeAsync(factory, "rl-perkey");
        var first = await KeyClientAsync(factory, owner, "first-agent");
        var second = await KeyClientAsync(factory, owner, "second-agent");

        Assert.Equal(HttpStatusCode.Accepted, (await IngestAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await IngestAsync(first)).StatusCode);

        // The window that closed belonged to the first key alone: the company's other tooling
        // still has its own permit to spend.
        Assert.Equal(HttpStatusCode.Accepted, (await IngestAsync(second)).StatusCode);
    }

    [Fact]
    public async Task A_Request_That_Names_No_Key_Shares_The_Unverified_Bucket()
    {
        await using var factory = new IntegrationApiFactory(db, ingestionPermitLimit: 1);
        await ArrangeAsync(factory, "rl-guess");

        var guess = factory.CreateClient();
        guess.DefaultRequestHeaders.Add("X-API-Key", "op_live_00000000000000000000000000000000");

        // The first guess is refused for being unknown. The second never even reaches that test:
        // callers that proved no key all draw from one bucket, so each guess cannot claim a
        // fresh one.
        Assert.Equal(HttpStatusCode.Unauthorized, (await IngestAsync(guess)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await IngestAsync(guess)).StatusCode);
    }

    [Fact]
    public async Task Endpoints_Beyond_Ingestion_Are_Not_Counted()
    {
        await using var factory = new IntegrationApiFactory(db, ingestionPermitLimit: 1);
        var owner = await ArrangeAsync(factory, "rl-jwt");

        // Five reads where the ingestion window would have allowed one.
        for (var i = 0; i < 5; i++)
        {
            var response = await owner.GetAsync("/api/v1/services");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>Registers an owner and its services — the ground a key's requests ingest on.</summary>
    private static async Task<HttpClient> ArrangeAsync(IntegrationApiFactory factory, string slug)
    {
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, slug);

        await CreateServiceAsync(owner, "Payments");
        await CreateServiceAsync(owner, "Checkout");

        return owner;
    }

    /// <summary>A client that authenticates with a key of its own, the way tooling would.</summary>
    private static async Task<HttpClient> KeyClientAsync(
        IntegrationApiFactory factory,
        HttpClient owner,
        string keyName)
    {
        var created = await owner.PostAsJsonAsync("/api/v1/api-keys", new { name = keyName });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var key = (await created.Content.ReadFromJsonAsync<Response<CreatedApiKeyDto>>(ApiAuth.Json))!
            .Data!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", key.Key);

        return client;
    }

    private static Task<HttpResponseMessage> IngestAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/v1/events",
            new
            {
                service = "Payments",
                environment = "Production",
                severity = "High",
                message = "p99 latency above threshold",
            });

    private static async Task CreateServiceAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
