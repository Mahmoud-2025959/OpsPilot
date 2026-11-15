using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Teams;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Teams.Queries.GetAllTeams;

public sealed class GetAllTeamsQueryHandler : IRequestHandler<GetAllTeamsQuery, PagedResponse<TeamDto>>
{
    private readonly ITeamRepository _repository;

    public GetAllTeamsQueryHandler(ITeamRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<TeamDto>> Handle(
        GetAllTeamsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, cancellationToken);

        var dtos = items.Select(t => t.ToDto()).ToList();

        return PagedResponse<TeamDto>.Create(dtos, page, pageSize, totalCount);
    }
}
