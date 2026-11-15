using MediatR;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Notifications.Queries.GetNotifications;

/// <summary>
/// Reads the signed-in user's own notifications (plan, UC-13 and FR-16). The user is never
/// supplied by the caller: the endpoint derives it from the token, so a request can only ever
/// read its own inbox.
/// </summary>
public sealed record GetNotificationsQuery(
    Guid UserId,
    bool? IsRead = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<NotificationDto>>;
