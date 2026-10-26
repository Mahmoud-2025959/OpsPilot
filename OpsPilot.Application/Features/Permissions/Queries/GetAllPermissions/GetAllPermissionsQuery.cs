using MediatR;
using OpsPilot.Contracts.Permissions;

namespace OpsPilot.Application.Features.Permissions.Queries.GetAllPermissions;

/// <summary>
/// Lists the permission catalogue (ERD §1). The catalogue is global, so the list is the same
/// for every company.
/// </summary>
public sealed record GetAllPermissionsQuery : IRequest<IReadOnlyList<PermissionDto>>;
