using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OpsPilot.Contracts.Auth;
using OpsPilot.Contracts.Realtime;

namespace OpsPilot.API.Hubs;

/// <summary>
/// Live incident, notification and conversation traffic (plan, UC-15 and FR-20).
/// </summary>
/// <remarks>
/// <para>
/// Authentication rides on the same JWT as the REST API. The browser cannot set headers on a
/// WebSocket handshake, so the token arrives in the query string as <c>access_token</c> — read
/// for hub traffic only, in the bearer handler, so ordinary endpoints keep refusing tokens
/// that would otherwise end up in access logs.
/// </para>
/// <para>
/// On connect the caller joins two groups: one personal (<c>user:{id}</c>) for their own
/// notifications, and one per company (<c>tenant:{id}</c>) for incident changes and messages.
/// Grouping is what makes a fan-out a single send rather than a loop over every connection — the
/// hub never needs to know who is watching.
/// </para>
/// </remarks>
[Authorize]
public sealed class IncidentsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;

        // The token has already been validated (the [Authorize] above and the bearer handler's
        // active-account check both ran), so these claims are known good; a missing one would
        // mean the pipeline itself drifted, and the connection is left without the group rather
        // than being refused here.
        if (TryReadUserId(user, out var userId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                HubProtocol.UserGroup(userId),
                Context.ConnectionAborted);
        }

        if (TryReadTenantId(user, out var tenantId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                HubProtocol.TenantGroup(tenantId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Passes a message on to everyone else watching the same company.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sender is read from the validated token and never from the payload: a client that
    /// could name itself could speak as anybody.
    /// </para>
    /// <para>
    /// The message is addressed to the company group rather than to a connection, so it cannot
    /// leave the company even if a client asks it to. It goes to <em>others</em> in that group,
    /// because the sender's own client already knows what it just wrote.
    /// </para>
    /// </remarks>
    public async Task SendMessage(string text)
    {
        var message = text?.Trim();

        if (string.IsNullOrEmpty(message))
        {
            throw new HubException("A message cannot be empty.");
        }

        if (message.Length > HubProtocol.MaxMessageLength)
        {
            throw new HubException(
                $"A message cannot be longer than {HubProtocol.MaxMessageLength} characters.");
        }

        if (!TryReadUserId(Context.User, out var senderId)
            || !TryReadTenantId(Context.User, out var tenantId))
        {
            // The pipeline guarantees both claims, so reaching here means this connection was
            // never attached to the groups OnConnectedAsync builds. Saying so beats dropping
            // the message into a group nobody is in.
            throw new HubException("This connection is not attached to a company.");
        }

        await Clients
            .OthersInGroup(HubProtocol.TenantGroup(tenantId))
            .SendAsync(
                HubProtocol.MessageReceived,
                new ConversationMessageDto(senderId, message, DateTimeOffset.UtcNow),
                Context.ConnectionAborted);
    }

    private static bool TryReadUserId(ClaimsPrincipal? user, out Guid userId)
        => Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
           && userId != Guid.Empty;

    private static bool TryReadTenantId(ClaimsPrincipal? user, out Guid tenantId)
        => Guid.TryParse(user?.FindFirstValue(AuthClaimTypes.TenantId), out tenantId)
           && tenantId != Guid.Empty;
}
