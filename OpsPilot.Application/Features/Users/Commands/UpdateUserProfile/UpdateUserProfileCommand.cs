using MediatR;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Commands.UpdateUserProfile;

public sealed record UpdateUserProfileCommand(
    Guid UserId,
    string Email,
    string DisplayName) : IRequest<UserDto?>;
