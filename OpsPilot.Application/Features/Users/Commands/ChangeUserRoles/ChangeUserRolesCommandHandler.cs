using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Application.Features.Roles;
using OpsPilot.Contracts.Users;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.ChangeUserRoles;

public sealed class ChangeUserRolesCommandHandler : IRequestHandler<ChangeUserRolesCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly RoleGrantGuard _grantGuard;
    private readonly IAuditLogWriter _audit;

    public ChangeUserRolesCommandHandler(
        IUserRepository userRepository,
        RoleGrantGuard grantGuard,
        IAuditLogWriter audit)
    {
        _userRepository = userRepository;
        _grantGuard = grantGuard;
        _audit = audit;
    }

    public Task<UserDto> Handle(ChangeUserRolesCommand request, CancellationToken cancellationToken)
        => _userRepository.ExecuteWithOwnerGuardAsync(
            ct => ChangeRolesAsync(request, ct),
            cancellationToken);

    private async Task<UserDto> ChangeRolesAsync(ChangeUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (user.TenantId != request.TenantId)
        {
            throw new EntityNotFoundException("User", request.UserId);
        }

        var requested = request.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _userRepository.GetRolesByNamesAsync(request.TenantId, requested, cancellationToken);
        var foundNames = roles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = requested.Where(r => !foundNames.Contains(r)).ToList();
        if (missing.Count != 0)
        {
            throw new DomainException(ExceptionMessages.RolesDoNotExist(missing));
        }

        // ERD §3: only an Owner may hand out the Owner role. Checked here, before the demotion
        // rule below, so a caller who is not an Owner is refused for the promotion itself
        // rather than for some side effect of it.
        await _grantGuard.EnsureRolesAssignableAsync(request.TenantId, roles, cancellationToken);

        var keepsOwner = roles.Any(r => r.IsOwner);

        // A company with no Owner can never be administered again, so the last one keeps its role.
        if (!keepsOwner && user.HasRole(SystemRoleNames.Owner))
        {
            if (!await _userRepository.AnotherActiveOwnerExistsAsync(user.Id, cancellationToken))
            {
                throw new ConflictException(ExceptionMessages.LastOwnerCannotBeDemoted);
            }
        }

        // Snapshot the role set before it is touched: the trail records who held which roles
        // going in, not merely the list that came out (FR-18).
        var previousRoles = user.Roles.Select(r => r.Name).ToList();

        // The list is authoritative, so drop what is no longer wanted before adding.
        foreach (var held in user.Roles.ToList())
        {
            if (!roles.Any(r => r.Id == held.Id))
            {
                user.RemoveRole(held);
            }
        }

        foreach (var role in roles)
        {
            user.AssignRole(role);
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        var newRoles = user.Roles.Select(r => r.Name).ToList();

        // Re-saving the same roles is not a change and leaves no trace; the guard above already
        // rejected the move that would strip a company of its last Owner.
        if (!previousRoles.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(newRoles.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase))
        {
            // This runs inside the owner-guard transaction, so the entry is committed (or rolled
            // back) together with the role change it describes -- the trail never claims a
            // demotion that did not happen.
            await _audit.WriteAsync(
                new AuditEntry(
                    AuditActions.UserRoleChanged,
                    nameof(User),
                    user.Id.ToString(),
                    OldValues: new { roles = previousRoles },
                    NewValues: new { roles = newRoles }),
                cancellationToken);
        }

        return user.ToDto();
    }
}
