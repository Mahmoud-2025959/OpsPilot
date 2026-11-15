namespace OpsPilot.Contracts.Users;

/// <summary>
/// Replaces the roles a user holds. The list is authoritative: roles left out are removed, and
/// an empty list strips every role.
/// </summary>
public sealed record ChangeUserRolesRequest(IReadOnlyList<string> Roles);

/// <summary>Activates or deactivates a user account.</summary>
public sealed record ChangeUserStatusRequest(bool IsActive);
