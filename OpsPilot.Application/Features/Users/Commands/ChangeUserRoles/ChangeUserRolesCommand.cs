using MediatR;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Commands.ChangeUserRoles;

/// <summary>
/// Replaces the roles a user holds. The list is authoritative: roles left out are removed.
/// </summary>
public sealed record ChangeUserRolesCommand(
    Guid TenantId,
    Guid UserId,
    IReadOnlyList<string> Roles) : IRequest<UserDto>;
