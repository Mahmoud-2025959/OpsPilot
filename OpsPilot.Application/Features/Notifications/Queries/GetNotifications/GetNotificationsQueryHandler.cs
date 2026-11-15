using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, PagedResponse<NotificationDto>>
{
    private readonly INotificationRepository _repository;

    public GetNotificationsQueryHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<NotificationDto>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _repository.GetPagedForUserAsync(
            request.UserId,
            request.IsRead,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(notification => notification.ToDto()).ToList();

        return PagedResponse<NotificationDto>.Create(dtos, page, pageSize, totalCount);
    }
}
