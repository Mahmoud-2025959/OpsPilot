using OpsPilot.Contracts.Notifications;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification notification) =>
        new(notification.Id,
            notification.UserId,
            notification.IncidentId,
            notification.Channel.ToString(),
            notification.Type.ToString(),
            notification.Title,
            notification.Body,
            notification.Status.ToString(),
            notification.SentAt,
            notification.ReadAt,
            notification.IsRead,
            notification.CreatedAt);
}
