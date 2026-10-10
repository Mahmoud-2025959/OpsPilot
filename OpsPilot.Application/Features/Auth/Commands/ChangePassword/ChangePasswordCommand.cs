using MediatR;

namespace OpsPilot.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommand : IRequest
{
    public Guid UserId { get; set; }

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
