namespace OpsPilot.Contracts.Users;

/// <summary>
/// The permissions a user actually holds, combined from every role they carry.
/// </summary>
/// <remarks>
/// Roles are included as well so a client can explain where a permission comes from without a
/// second request. A retired role grants nothing and is left out of both lists.
/// </remarks>
public sealed record UserPermissionsDto(
    Guid UserId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
