using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Commands.UpdateRole;

/// <summary>
/// Renames a custom role and changes its description. The company comes from the caller.
/// </summary>
/// <param name="TenantId">The company the role belongs to.</param>
/// <param name="Id">The role to rename.</param>
/// <param name="Name">The new name, unique within the company.</param>
/// <param name="Description">The new description, or null to clear it.</param>
public sealed record UpdateRoleCommand(
    Guid TenantId,
    Guid Id,
    string Name,
    string? Description) : IRequest<RoleDto>;
