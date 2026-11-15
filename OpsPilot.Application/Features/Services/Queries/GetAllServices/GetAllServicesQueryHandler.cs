using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Services;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Services.Queries.GetAllServices;

public sealed class GetAllServicesQueryHandler
    : IRequestHandler<GetAllServicesQuery, PagedResponse<ServiceDto>>
{
    private readonly IServiceRepository _repository;

    public GetAllServicesQueryHandler(IServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<ServiceDto>> Handle(
        GetAllServicesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, cancellationToken);

        var dtos = items.Select(s => s.ToDto()).ToList();

        return PagedResponse<ServiceDto>.Create(dtos, page, pageSize, totalCount);
    }
}
