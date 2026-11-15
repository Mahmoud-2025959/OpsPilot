using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Notifications.Queries.GetNotifications;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The signed-in user's inbox (plan, UC-13 and FR-16).
/// </summary>
/// <remarks>
/// Open to any authenticated user without a permission gate — the route can only ever return
/// the caller's own notifications, because the user is taken from the token rather than from
/// the query string. Asking for somebody else's inbox is therefore not a thing this endpoint
/// can be talked into, and the tenant filter keeps companies apart underneath.
/// </remarks>
[Route("api/v1/notifications")]
public sealed class NotificationsController : BaseController
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationDto>>> GetMine(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<PagedResponse<NotificationDto>>(
                "Unauthorized.",
                BusinessCode.Unauthorized));
        }

        var result = await Mediator.Send(new GetNotificationsQuery(userId, isRead, page, pageSize));

        return Ok(result);
    }
}
