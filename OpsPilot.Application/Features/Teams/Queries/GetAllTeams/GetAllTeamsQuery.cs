using MediatR;
using OpsPilot.Contracts.Teams;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Teams.Queries.GetAllTeams;

public sealed record GetAllTeamsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResponse<TeamDto>>;
