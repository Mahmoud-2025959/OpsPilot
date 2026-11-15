using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Services;

/// <summary>
/// Guards the team a service is assigned to as its owner.
/// </summary>
/// <remarks>
/// <c>Services.OwnerTeamId</c> is a foreign key, so an id that names nothing would surface as a
/// constraint violation — a 500 — instead of a readable answer. The lookup goes through the
/// tenant-filtered repository, so a team belonging to another company is refused as well rather
/// than being accepted by the constraint.
/// </remarks>
internal static class ServiceOwner
{
    internal static async Task EnsureTeamExistsAsync(
        ITeamRepository teamRepository,
        Guid? ownerTeamId,
        CancellationToken cancellationToken)
    {
        if (ownerTeamId is not { } teamId)
        {
            return;
        }

        if (await teamRepository.GetByIdAsync(teamId, cancellationToken) is null)
        {
            throw new EntityNotFoundException("Team", teamId);
        }
    }
}
