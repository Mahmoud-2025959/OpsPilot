using MediatR;

namespace OpsPilot.Application.Features.Teams.Commands.CreateTeam;

/// <summary>Creates a team. The tenant comes from the caller, never from the body.</summary>
public sealed record CreateTeamCommand(
    Guid TenantId,
    string Name,
    string? Description = null) : IRequest<Guid>;
