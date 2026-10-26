using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Queries.GetAllRoles;

/// <summary>Lists the company's roles, each with the permissions it grants.</summary>
public sealed record GetAllRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
