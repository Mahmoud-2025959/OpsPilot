using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles;

/// <summary>
/// The two rules from ERD §3 that decide who may hand out what: nobody may grant a permission
/// they do not hold themselves, and only an Owner may grant the Owner role.
/// </summary>
/// <remarks>
/// <para>
/// The checks read the caller's roles from the database rather than from the token, because a
/// token may still carry a permission that has since been revoked, and the whole point of the
/// rules is to compare against what the caller holds <em>now</em>.
/// </para>
/// <para>
/// The Owner is the one exception to the permission rule: it is the company's root
/// administrator and holds <c>tenant.settings</c>, the permission that changes company-wide
/// settings, so it is allowed to define any permission the build knows about — including ones
/// the Owner role itself does not carry.
/// </para>
/// </remarks>
public sealed class RoleGrantGuard
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    public RoleGrantGuard(IUserRepository userRepository, ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Refuses the request unless the caller may grant every one of
    /// <paramref name="permissionNames"/>.
    /// </summary>
    /// <exception cref="ForbiddenException">
    /// The caller is unknown to this company, or asks for a permission they do not hold.
    /// </exception>
    public async Task EnsureGrantableAsync(
        Guid tenantId,
        IReadOnlyCollection<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        if (permissionNames.Count == 0)
        {
            return;
        }

        var caller = await GetCallerAsync(tenantId, cancellationToken);

        if (IsOwner(caller))
        {
            return;
        }

        // A retired role grants nothing, even though the row is still there. The lookup
        // crosses the tenant filter, so the soft-delete rule has to be applied by hand.
        var held = caller.Roles
            .Where(r => !r.IsDeleted)
            .SelectMany(r => r.Permissions)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var escalated = permissionNames.Where(name => !held.Contains(name)).ToList();

        if (escalated.Count != 0)
        {
            throw new ForbiddenException(ExceptionMessages.PermissionsNotHeldByCaller(escalated));
        }
    }

    /// <summary>
    /// Refuses the request when it puts the Owner role on a user and the caller is not an
    /// Owner themselves.
    /// </summary>
    /// <remarks>
    /// The permission rule above would not catch this: the Owner role is not a permission, and
    /// an Admin holding <c>roles.change</c> could otherwise promote itself, then pick up every
    /// permission — including <c>tenant.settings</c> — on its next token.
    /// </remarks>
    /// <exception cref="ForbiddenException">
    /// The caller is unknown to this company, or is not an Owner while granting Owner.
    /// </exception>
    public async Task EnsureRolesAssignableAsync(
        Guid tenantId,
        IReadOnlyCollection<Role> roles,
        CancellationToken cancellationToken = default)
    {
        if (!roles.Any(r => r.IsOwner))
        {
            return;
        }

        var caller = await GetCallerAsync(tenantId, cancellationToken);

        if (IsOwner(caller))
        {
            return;
        }

        throw new ForbiddenException(ExceptionMessages.CannotGrantOwnerRole);
    }

    private async Task<User> GetCallerAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var callerId))
        {
            throw new ForbiddenException(ExceptionMessages.CallerNotInCompany);
        }

        var caller = await _userRepository.FindByIdAcrossTenantsAsync(callerId, cancellationToken);

        if (caller is null || caller.TenantId != tenantId)
        {
            throw new ForbiddenException(ExceptionMessages.CallerNotInCompany);
        }

        return caller;
    }

    private static bool IsOwner(User caller) => caller.Roles.Any(r => r.IsOwner && !r.IsDeleted);
}
