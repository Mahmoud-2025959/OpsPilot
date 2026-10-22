using MediatR;
using OpsPilot.Contracts.Teams;

namespace OpsPilot.Application.Features.Teams.Commands.RemoveTeamMember;

public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid UserId) : IRequest<TeamDto>;
