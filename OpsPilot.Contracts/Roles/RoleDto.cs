using OpsPilot.Contracts.Permissions;

namespace OpsPilot.Contracts.Roles;

/// <summary>
/// A role of the company together with the permissions it grants (ERD §3).
/// </summary>
/// <param name="Id">The role's id.</param>
/// <param name="TenantId">The company the role belongs to.</param>
/// <param name="Name">The role name, e.g. <c>Engineer</c>.</param>
/// <param name="Description">An optional explanation of what the role is for.</param>
/// <param name="IsSystem">
/// True for the five roles provisioned with the company. System roles cannot be renamed or
/// retired.
/// </param>
/// <param name="IsOwner">
/// True for the Owner role, whose permission set is frozen for the lifetime of the company.
/// </param>
/// <param name="Permissions">The permissions this role grants.</param>
public sealed record RoleDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsOwner,
    IReadOnlyList<PermissionDto> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
