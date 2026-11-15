using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Realtime;
using OpsPilot.Contracts.Wrappers;
using Xunit;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Covers the live incident channel end to end (plan, UC-15 and FR-20): a socket is
/// authenticated by the same token as the REST API, joined to its own user and company groups,
/// and a change in one company never reaches a watcher in another.
/// </summary>
public sealed class IncidentHubTests(LocalDbFixture db) : IClassFixture<LocalDbFixture>
{
    [Fact]
    public async Task Negotiating_Without_A_Token_Is_Refused()
    {
        await using var factory = new IntegrationApiFactory(db);
        var anonymous = factory.CreateClient();

        var negotiate = await anonymous.PostAsync("/hubs/incidents/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);
    }

    [Fact]
    public async Task The_Query_String_Token_Is_Read_For_Hubs_Only()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (_, auth) = await ApiAuth.OwnerClientAsync(factory, "hub-negotiate");

        var anonymous = factory.CreateClient();
        var query = $"?access_token={Uri.EscapeDataString(auth.AccessToken)}";

        var negotiate = await anonymous.PostAsync(
            $"/hubs/incidents/negotiate{query}&negotiateVersion=1",
            null);

        // A browser cannot set headers on a WebSocket handshake, so the token has to be
        // readable from the query string for hub traffic.
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);

        // ...and only for hub traffic: were ordinary endpoints to accept it too, every access
        // log and proxy trace along the way would be holding a live credential.
        var ordinary = await anonymous.GetAsync($"/api/v1/notifications{query}");

        Assert.Equal(HttpStatusCode.Unauthorized, ordinary.StatusCode);
    }

    [Fact]
    public async Task A_Transition_Reaches_Everyone_Watching_The_Company()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "hub-tenant");
        var serviceId = await CreateServiceAsync(owner, "Hub Tenant");
        var incidentId = await CreateIncidentAsync(owner, serviceId);
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "hub-tenant", "Engineer");

        var ownerUpdates = new List<Guid>();
        var engineerUpdates = new List<Guid>();

        await using var ownerHub = BuildHub(factory, auth.AccessToken);
        await using var engineerHub = BuildHub(factory, TokenOf(engineer));

        ownerHub.On<JsonElement>(
            "incidentUpdated",
            payload => ownerUpdates.Add(payload.GetProperty("incidentId").GetGuid()));

        engineerHub.On<JsonElement>(
            "incidentUpdated",
            payload => engineerUpdates.Add(payload.GetProperty("incidentId").GetGuid()));

        await ownerHub.StartAsync();
        await engineerHub.StartAsync();

        var transition = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/transitions",
            new { toStatus = "acknowledged" });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        await WaitUntilAsync(
            () => ownerUpdates.Count == 1 && engineerUpdates.Count == 1,
            "both watchers were told about the transition");

        Assert.Equal(incidentId, Assert.Single(ownerUpdates));
        Assert.Equal(incidentId, Assert.Single(engineerUpdates));
    }

    [Fact]
    public async Task A_Notification_Reaches_Only_Its_Recipient()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, auth) = await ApiAuth.OwnerClientAsync(factory, "hub-notify");
        var serviceId = await CreateServiceAsync(owner, "Hub Notify");
        var incidentId = await CreateIncidentAsync(owner, serviceId);
        var (engineer, engineerId) = await ApiAuth.MemberClientAsync(factory, owner, "hub-notify", "Engineer");

        var assigneeCopy = new List<NotificationDto>();
        var assignerCopy = new List<NotificationDto>();

        await using var engineerHub = BuildHub(factory, TokenOf(engineer));
        await using var ownerHub = BuildHub(factory, auth.AccessToken);

        engineerHub.On<NotificationDto>("notificationCreated", assigneeCopy.Add);
        ownerHub.On<NotificationDto>("notificationCreated", assignerCopy.Add);

        await engineerHub.StartAsync();
        await ownerHub.StartAsync();

        var assign = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/assign",
            new { assigneeId = engineerId });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        await WaitUntilAsync(
            () => assigneeCopy.Count == 1,
            "the assignee was told about the assignment");

        // The delivered copy is the in-app one; the email row is still waiting for a mailer
        // and is deliberately not announced as if it had arrived.
        var notification = Assert.Single(assigneeCopy);
        Assert.Equal("IncidentAssigned", notification.Type);
        Assert.Equal("InApp", notification.Channel);
        Assert.Equal(incidentId, notification.IncidentId);

        // The owner did the assigning and is the reporter, so nothing was addressed to them.
        Assert.Empty(assignerCopy);
    }

    [Fact]
    public async Task Two_Watchers_Can_Hold_A_Conversation()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, ownerAuth) = await ApiAuth.OwnerClientAsync(factory, "hub-chat");
        var (engineer, engineerId) = await ApiAuth.MemberClientAsync(factory, owner, "hub-chat", "Engineer");

        var ownerHeard = new List<ConversationMessageDto>();
        var engineerHeard = new List<ConversationMessageDto>();

        await using var ownerHub = BuildHub(factory, ownerAuth.AccessToken);
        await using var engineerHub = BuildHub(factory, TokenOf(engineer));

        ownerHub.On<ConversationMessageDto>("messageReceived", ownerHeard.Add);
        engineerHub.On<ConversationMessageDto>("messageReceived", engineerHeard.Add);

        await ownerHub.StartAsync();
        await engineerHub.StartAsync();

        await ownerHub.InvokeAsync("SendMessage", "Investigating the payment outage now.");

        await WaitUntilAsync(() => engineerHeard.Count == 1, "the engineer heard the owner");

        var heard = Assert.Single(engineerHeard);
        Assert.Equal("Investigating the payment outage now.", heard.Text);
        Assert.Equal(ownerAuth.Id, heard.SenderId);
        Assert.Equal(ownerAuth.DisplayName, heard.SenderName);

        // The sender's own client already shows what it wrote, so echoing it back would make
        // every conversation render each line twice.
        Assert.Empty(ownerHeard);

        await engineerHub.InvokeAsync("SendMessage", "Copy that, looking at the logs.");

        await WaitUntilAsync(() => ownerHeard.Count == 1, "the owner heard the engineer");

        var reply = Assert.Single(ownerHeard);
        Assert.Equal("Copy that, looking at the logs.", reply.Text);
        Assert.Equal(engineerId, reply.SenderId);
        Assert.Equal("Engineer hub-chat", reply.SenderName);

        // ...and the reply is not echoed either, which is what makes this a conversation
        // between two watchers rather than two copies of the same stream.
        Assert.Single(engineerHeard);
    }

    [Fact]
    public async Task A_Blank_Message_Is_Refused()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, ownerAuth) = await ApiAuth.OwnerClientAsync(factory, "hub-blank");
        var (engineer, _) = await ApiAuth.MemberClientAsync(factory, owner, "hub-blank", "Engineer");

        var engineerHeard = new List<ConversationMessageDto>();

        await using var ownerHub = BuildHub(factory, ownerAuth.AccessToken);
        await using var engineerHub = BuildHub(factory, TokenOf(engineer));

        engineerHub.On<ConversationMessageDto>("messageReceived", engineerHeard.Add);

        await ownerHub.StartAsync();
        await engineerHub.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(
            () => ownerHub.InvokeAsync("SendMessage", "   "));

        Assert.Contains("cannot be empty", error.Message, StringComparison.Ordinal);

        // A refused message is refused outright: nothing was broadcast on the way to the error.
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Empty(engineerHeard);
    }

    [Fact]
    public async Task A_Message_Does_Not_Leave_The_Company()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, ownerAuth) = await ApiAuth.OwnerClientAsync(factory, "hub-chat-acme");
        var (_, strangerAuth) = await ApiAuth.OwnerClientAsync(factory, "hub-chat-globex");

        var strangersInbox = new List<ConversationMessageDto>();

        await using var ownerHub = BuildHub(factory, ownerAuth.AccessToken);
        await using var strangerHub = BuildHub(factory, strangerAuth.AccessToken);

        strangerHub.On<ConversationMessageDto>("messageReceived", strangersInbox.Add);

        await ownerHub.StartAsync();
        await strangerHub.StartAsync();

        await ownerHub.InvokeAsync("SendMessage", "Anyone else seeing the spike?");

        // The message is addressed to the sender's company group, so a watcher in another
        // company is not merely filtered out on their side -- they were never addressed.
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Empty(strangersInbox);
    }

    [Fact]
    public async Task A_Watcher_In_Another_Company_Sees_Nothing()
    {
        await using var factory = new IntegrationApiFactory(db);
        var (owner, _) = await ApiAuth.OwnerClientAsync(factory, "hub-acme");
        var (_, strangerAuth) = await ApiAuth.OwnerClientAsync(factory, "hub-globex");

        var serviceId = await CreateServiceAsync(owner, "Hub Acme");
        var incidentId = await CreateIncidentAsync(owner, serviceId);

        var strangersUpdates = new List<Guid>();

        await using var strangerHub = BuildHub(factory, strangerAuth.AccessToken);
        strangerHub.On<JsonElement>(
            "incidentUpdated",
            payload => strangersUpdates.Add(payload.GetProperty("incidentId").GetGuid()));

        await strangerHub.StartAsync();

        var transition = await owner.PostAsJsonAsync(
            $"/api/v1/incidents/{incidentId}/transitions",
            new { toStatus = "acknowledged" });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        // Granted the same grace the positive test waits out, so that "nothing arrived" means
        // the group was never addressed rather than the message being merely slow.
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Empty(strangersUpdates);
    }

    /// <summary>
    /// Opens a hub connection that runs in-process against the test server, carrying the
    /// access token in the query string exactly as a browser client would.
    /// </summary>
    private static HubConnection BuildHub(IntegrationApiFactory factory, string accessToken)
    {
        var url = new Uri(
            factory.Server.BaseAddress,
            $"hubs/incidents?access_token={Uri.EscapeDataString(accessToken)}");

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                // Long polling is the transport that needs no socket, which is the only one
                // the in-memory test server offers; the query-string token rides along on
                // every poll just as it does on a handshake.
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static string TokenOf(HttpClient client)
        => client.DefaultRequestHeaders.Authorization?.Parameter
           ?? throw new InvalidOperationException("The client is not authenticated.");

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), $"Timed out waiting until {because}.");
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient owner, string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/services",
            new { name, environment = "Production" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }

    private static async Task<Guid> CreateIncidentAsync(HttpClient owner, Guid serviceId)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/incidents", new
        {
            serviceId,
            title = "Checkout is failing",
            description = "5xx on the payment callback",
            severity = "High",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Response<Guid>>(ApiAuth.Json))!.Data;
    }
}
