using MediatR;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Queries.GetRoleById;

/// <summary>Reads one role of the company together with the permissions it grants.</summary>
public sealed record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto?>;
