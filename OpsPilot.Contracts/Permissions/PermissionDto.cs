namespace OpsPilot.Contracts.Permissions;

/// <summary>
/// One entry of the permission catalogue (ERD §3).
/// </summary>
/// <param name="Id">The stable id of the catalogue row.</param>
/// <param name="Name">The permission string, e.g. <c>incidents.read</c>.</param>
/// <param name="Category">The group the permission is listed under in the matrix UI.</param>
/// <param name="Description">A human-readable explanation of what the permission allows.</param>
public sealed record PermissionDto(
    Guid Id,
    string Name,
    string Category,
    string? Description);
