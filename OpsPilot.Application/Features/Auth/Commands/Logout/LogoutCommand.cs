using MediatR;

namespace OpsPilot.Application.Features.Auth.Commands.Logout;

public sealed class LogoutCommand : IRequest
{
    public Guid UserId { get; set; }

    public string RefreshToken { get; set; } = string.Empty;
}
