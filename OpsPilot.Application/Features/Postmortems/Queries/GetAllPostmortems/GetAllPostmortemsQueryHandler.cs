using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetAllPostmortems;

public sealed class GetAllPostmortemsQueryHandler
    : IRequestHandler<GetAllPostmortemsQuery, PagedResponse<PostmortemDto>>
{
    private readonly IPostmortemRepository _repository;

    public GetAllPostmortemsQueryHandler(IPostmortemRepository repository) =>
        _repository = repository;

    public async Task<PagedResponse<PostmortemDto>> Handle(
        GetAllPostmortemsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        PostmortemStatus? status = Enum.TryParse<PostmortemStatus>(request.Status, ignoreCase: true, out var s)
            && Enum.IsDefined(s)
                ? s
                : null;

        var (items, totalCount) = await _repository.GetPagedAsync(
            status,
            request.IncidentId,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(p => p.ToDto()).ToList();

        return PagedResponse<PostmortemDto>.Create(dtos, page, pageSize, totalCount);
    }
}
