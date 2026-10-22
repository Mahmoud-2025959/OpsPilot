using System.Net;
using System.Net.Http.Json;
using OpsPilot.Contracts.Teams;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers teams and their membership over HTTP: creating teams and changing membership need
/// <c>teams.manage</c>, while reading is open to any signed-in user.
/// </summary>
public sealed class TeamsApiTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Creating_A_Team_And_Managing_Its_Members()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-lifecycle");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "teams-lifecycle", "Engineer");

        var create = await owner.PostAsJsonAsync(
            "/api/v1/teams",
            new { name = "Platform", description = "Owns the platform" });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        Assert.NotNull(created?.Data);
        var teamId = created!.Data;

        var add = await owner.PostAsJsonAsync(
            $"/api/v1/teams/{teamId}/members",
            new { userId });

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var withMember = await add.Content.ReadFromJsonAsync<Response<TeamDto>>(ApiAuth.Json);

        Assert.NotNull(withMember?.Data);
        var member = Assert.Single(withMember.Data.Members);
        Assert.Equal(userId, member.UserId);

        // An omitted role means Member.
        Assert.Equal(TeamRole.Member.ToString(), member.Role);

        var remove = await owner.DeleteAsync($"/api/v1/teams/{teamId}/members/{userId}");

        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var afterRemoval = await remove.Content.ReadFromJsonAsync<Response<TeamDto>>(ApiAuth.Json);

        Assert.NotNull(afterRemoval?.Data);
        Assert.Empty(afterRemoval.Data.Members);
    }

    [Fact]
    public async Task An_Explicit_Lead_Role_Is_Honoured()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-lead");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "teams-lead", "Engineer");

        var create = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });
        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        var add = await owner.PostAsJsonAsync(
            $"/api/v1/teams/{created!.Data}/members",
            new { userId, role = "Lead" });

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var team = await add.Content.ReadFromJsonAsync<Response<TeamDto>>(ApiAuth.Json);

        Assert.NotNull(team?.Data);
        Assert.Equal(TeamRole.Lead.ToString(), Assert.Single(team.Data.Members).Role);
    }

    [Fact]
    public async Task Adding_The_Same_Member_Twice_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-dupe-member");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "teams-dupe-member", "Engineer");

        var create = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });
        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);
        var teamId = created!.Data;

        var first = await owner.PostAsJsonAsync($"/api/v1/teams/{teamId}/members", new { userId });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Membership is really a conflict with the team's current state, so it must not come
        // back as the generic 400 that a DomainException would produce.
        var second = await owner.PostAsJsonAsync($"/api/v1/teams/{teamId}/members", new { userId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var payload = await second.Content.ReadFromJsonAsync<Response<TeamDto>>(ApiAuth.Json);

        Assert.NotNull(payload);
        Assert.Equal(BusinessCode.Conflict, payload.Code);
    }

    [Fact]
    public async Task Removing_Someone_Who_Is_Not_A_Member_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-non-member");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "teams-non-member", "Engineer");

        var create = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });
        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        var remove = await owner.DeleteAsync($"/api/v1/teams/{created!.Data}/members/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task A_Duplicate_Team_Name_Is_A_Conflict()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-dupe-name");

        var first = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Adding_A_User_Who_Does_Not_Exist_Is_Not_Found()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-ghost-user");

        var create = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });
        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);

        var add = await owner.PostAsJsonAsync(
            $"/api/v1/teams/{created!.Data}/members",
            new { userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
    }

    [Fact]
    public async Task A_Viewer_May_Read_Teams_But_Not_Create_One()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-viewer");

        await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });

        var (viewer, _) = await ApiAuth.MemberClientAsync(factory, owner, "teams-viewer", "Viewer");

        var read = await viewer.GetAsync("/api/v1/teams");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await viewer.PostAsJsonAsync("/api/v1/teams", new { name = "Billing" });

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task A_Team_Is_Listed_With_Its_Members()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "teams-list");
        var (_, userId) = await ApiAuth.MemberClientAsync(factory, owner, "teams-list", "Engineer");

        var create = await owner.PostAsJsonAsync("/api/v1/teams", new { name = "Platform" });
        var created = await create.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json);
        var teamId = created!.Data;

        await owner.PostAsJsonAsync($"/api/v1/teams/{teamId}/members", new { userId });

        var list = await owner.GetFromJsonAsync<PagedResponse<TeamDto>>(
            "/api/v1/teams?page=1&pageSize=50",
            ApiAuth.Json);

        Assert.NotNull(list?.Data);

        var listed = Assert.Single(list.Data, t => t.Id == teamId);

        Assert.Equal(userId, Assert.Single(listed.Members).UserId);
    }
}
