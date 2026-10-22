using MediatR;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Commands.ChangeUserStatus;

/// <summary>Activates or deactivates a user account.</summary>
public sealed record ChangeUserStatusCommand(
    Guid TenantId,
    Guid UserId,
    bool IsActive) : IRequest<UserDto>;
