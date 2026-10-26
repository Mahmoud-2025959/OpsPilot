using MediatR;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Queries.GetCurrentUser;

/// <summary>Returns the signed-in user's own profile and the permissions they hold.</summary>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<CurrentUserDto?>;
