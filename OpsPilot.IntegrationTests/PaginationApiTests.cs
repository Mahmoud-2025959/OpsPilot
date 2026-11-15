using System.Net.Http.Json;
using OpsPilot.Contracts.Services;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Pins the page-size contract from the plan: a list request may ask for at most 100 rows, and
/// anything outside the accepted range falls back to the default rather than being honoured.
/// </summary>
public sealed class PaginationApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task A_Page_Size_Above_The_Maximum_Falls_Back_To_The_Default()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (client, _) = await ApiAuth.OwnerClientAsync(factory, "paging-max");

        var over = await client.GetFromJsonAsync<PagedResponse<ServiceDto>>(
            "/api/v1/services?pageSize=500",
            ApiAuth.Json);

        Assert.Equal(20, over?.PageSize);

        // The documented ceiling is still honoured exactly.
        var atMaximum = await client.GetFromJsonAsync<PagedResponse<ServiceDto>>(
            "/api/v1/services?pageSize=100",
            ApiAuth.Json);

        Assert.Equal(100, atMaximum?.PageSize);
    }
}
