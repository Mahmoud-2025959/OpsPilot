using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Teams;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Teams.Commands.RemoveTeamMember;

public sealed class RemoveTeamMemberCommandHandler
    : IRequestHandler<RemoveTeamMemberCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;

    public RemoveTeamMemberCommandHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<TeamDto> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", request.TeamId);

        if (!team.RemoveMember(request.UserId))
        {
            throw new EntityNotFoundException("Team member", request.UserId);
        }

        await _teamRepository.UpdateAsync(team, cancellationToken);

        return team.ToDto();
    }
}
