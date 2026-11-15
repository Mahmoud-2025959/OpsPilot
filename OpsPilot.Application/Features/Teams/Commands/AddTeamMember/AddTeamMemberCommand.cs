using MediatR;
using OpsPilot.Contracts.Teams;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Teams.Commands.AddTeamMember;

/// <summary>
/// Adds a user to a team. <paramref name="Role"/> defaults to <see cref="TeamRole.Member"/> when
/// null.
/// </summary>
public sealed record AddTeamMemberCommand(
    Guid TeamId,
    Guid UserId,
    TeamRole? Role = null) : IRequest<TeamDto>;
