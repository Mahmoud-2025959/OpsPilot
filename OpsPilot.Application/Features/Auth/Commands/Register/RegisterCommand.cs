using MediatR;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(
    Guid TenantId,
    string Email,
    string DisplayName,
    string Password,
    string ConfirmPassword,
    IReadOnlyList<string>? Roles = null) : IRequest<AuthResponseDto>;
