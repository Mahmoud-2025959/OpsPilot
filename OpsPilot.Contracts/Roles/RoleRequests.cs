namespace OpsPilot.Contracts.Roles;

/// <summary>
/// Creates a custom role. The company comes from the token, never from the body.
/// </summary>
/// <param name="Name">The role name, unique within the company.</param>
/// <param name="Description">An optional explanation of what the role is for.</param>
/// <param name="Permissions">
/// The permissions the role grants. Omitted or empty means the role starts with none.
/// </param>
public sealed record CreateRoleRequest(
    string Name,
    string? Description = null,
    IReadOnlyList<string>? Permissions = null);

/// <summary>Renames a custom role and changes its description.</summary>
/// <param name="Name">The new name, unique within the company.</param>
/// <param name="Description">The new description, or null to clear it.</param>
public sealed record UpdateRoleRequest(
    string Name,
    string? Description = null);

/// <summary>
/// Replaces the whole permission set of a role. Permissions left out are revoked.
/// </summary>
/// <param name="Permissions">The complete set of permissions the role should grant.</param>
public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);
