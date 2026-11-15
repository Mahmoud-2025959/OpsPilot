using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Teams;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Teams.Commands.AddTeamMember;

public sealed class AddTeamMemberCommandHandler : IRequestHandler<AddTeamMemberCommand, TeamDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly IUserRepository _userRepository;

    public AddTeamMemberCommandHandler(ITeamRepository teamRepository, IUserRepository userRepository)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
    }

    public async Task<TeamDto> Handle(AddTeamMemberCommand request, CancellationToken cancellationToken)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, cancellationToken)
            ?? throw new EntityNotFoundException("Team", request.TeamId);

        // The user lookup is tenant-filtered, so a user from another company is a 404 rather
        // than a cross-tenant membership.
        _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        // The domain rejects a duplicate membership with a plain DomainException, which surfaces
        // as a 400. Membership is really a conflict with the team's current state, so check here
        // and answer 409 instead.
        if (team.Members.Any(m => m.UserId == request.UserId))
        {
            throw new ConflictException(ExceptionMessages.TeamMemberAlreadyExists);
        }

        team.AddMember(request.UserId, request.Role ?? TeamRole.Member);

        await _teamRepository.UpdateAsync(team, cancellationToken);

        return team.ToDto();
    }
}
