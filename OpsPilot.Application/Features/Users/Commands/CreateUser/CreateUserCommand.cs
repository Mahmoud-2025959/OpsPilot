using MediatR;

namespace OpsPilot.Application.Features.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    Guid TenantId,
    string Email,
    string DisplayName,
    string Password,
    string ConfirmPassword,
    IReadOnlyList<string>? Roles = null) : IRequest<Guid>;
