using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Commands.CreateRole;

/// <summary>
/// Creates a custom role. The company comes from the caller, never from the body.
/// </summary>
/// <param name="TenantId">The company the role belongs to.</param>
/// <param name="Name">The role name, unique within the company.</param>
/// <param name="Description">An optional explanation of what the role is for.</param>
/// <param name="Permissions">The permissions the role grants.</param>
public sealed record CreateRoleCommand(
    Guid TenantId,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : IRequest<RoleDto>;
