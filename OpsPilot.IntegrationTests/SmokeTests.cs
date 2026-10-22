using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OpsPilot.IntegrationTests;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_Returns_NotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_Is_Served_In_Development()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("openapi", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ingestion authenticates with <c>X-API-Key</c> rather than a token, so its buttons in
    /// Swagger UI are dead until the document publishes a scheme for that header — this guards
    /// the difference between "the endpoint is documented" and "the endpoint can be called".
    /// </summary>
    [Fact]
    public async Task Swagger_Publishes_The_Api_Key_Scheme_For_Ingestion()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var apiKey = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("ApiKey");

        Assert.Equal("apiKey", apiKey.GetProperty("type").GetString());
        Assert.Equal("X-API-Key", apiKey.GetProperty("name").GetString());
        Assert.Equal("header", apiKey.GetProperty("in").GetString());

        // The requirement is what makes Swagger UI attach the value it collected: a scheme
        // nobody references is published but never sent.
        var requirements = document.RootElement
            .GetProperty("security")
            .EnumerateArray();

        Assert.Contains(
            requirements,
            requirement => requirement.TryGetProperty("ApiKey", out _));
    }

    /// <summary>
    /// Swagger UI can only attach a token when the document publishes a security scheme, so this
    /// guards the difference between "the API is documented" and "the API can be tried".
    /// </summary>
    [Fact]
    public async Task Swagger_Publishes_The_Bearer_Scheme_And_Only_V1_Routes()
    {
        var client = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // GetProperty throws when the member is absent, which fails this test loudly rather
        // than letting Swagger UI quietly render without an Authorize button.
        var bearer = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        // The document carries the versioned surface only, so every operation appears once.
        var paths = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .ToList();

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.StartsWith("/api/v1/", path, StringComparison.OrdinalIgnoreCase));
    }
}