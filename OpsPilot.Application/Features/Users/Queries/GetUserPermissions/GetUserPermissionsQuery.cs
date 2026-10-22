using MediatR;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Queries.GetUserPermissions;

/// <summary>Asks what a user in the ambient company can actually do.</summary>
public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<UserPermissionsDto?>;
