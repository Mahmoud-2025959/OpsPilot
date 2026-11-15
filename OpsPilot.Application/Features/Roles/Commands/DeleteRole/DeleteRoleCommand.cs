using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Commands.DeleteRole;

/// <summary>
/// Retires a custom role. The row is kept so the accounts that held it stay resolvable.
/// </summary>
/// <param name="TenantId">The company the role belongs to.</param>
/// <param name="Id">The role to retire.</param>
public sealed record DeleteRoleCommand(
    Guid TenantId,
    Guid Id) : IRequest<RoleDto>;
