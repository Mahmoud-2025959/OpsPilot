using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Teams.Commands.CreateTeam;

public sealed class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, Guid>
{
    private readonly ITeamRepository _teamRepository;

    public CreateTeamCommandHandler(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<Guid> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        // Team names are unique per tenant in the database; checking first yields a readable
        // conflict instead of a constraint violation.
        if (await _teamRepository.NameExistsAsync(request.Name, null, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.TeamAlreadyExists);
        }

        var team = Team.Create(request.TenantId, request.Name, request.Description);

        return await _teamRepository.AddAsync(team, cancellationToken);
    }
}
