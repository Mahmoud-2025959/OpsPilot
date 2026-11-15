using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Commands.SetRolePermissions;

/// <summary>
/// Replaces the whole permission set of a role. The list is authoritative: permissions left
/// out are revoked.
/// </summary>
/// <param name="TenantId">The company the role belongs to.</param>
/// <param name="Id">The role whose permissions are replaced.</param>
/// <param name="Permissions">The complete set of permissions the role should grant.</param>
public sealed record SetRolePermissionsCommand(
    Guid TenantId,
    Guid Id,
    IReadOnlyList<string> Permissions) : IRequest<RoleDto>;
