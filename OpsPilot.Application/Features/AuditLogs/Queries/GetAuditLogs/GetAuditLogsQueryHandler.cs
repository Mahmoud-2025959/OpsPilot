using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.AuditLogs;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.AuditLogs.Queries.GetAuditLogs;

public sealed class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResponse<AuditLogDto>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsQueryHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<AuditLogDto>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(
            Normalize(request.Action),
            Normalize(request.EntityType),
            Normalize(request.EntityId),
            request.ActorUserId,
            request.From,
            request.To,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(log => log.ToDto()).ToList();

        return PagedResponse<AuditLogDto>.Create(dtos, page, pageSize, totalCount);
    }

    /// <summary>Treating a blank filter as "no filter" keeps <c>?action=</c> from meaning
    /// "entries whose action is the empty string", which is never what a caller intends.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
