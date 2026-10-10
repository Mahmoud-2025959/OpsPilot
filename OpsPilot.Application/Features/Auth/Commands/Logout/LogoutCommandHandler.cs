using MediatR;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Application.Features.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly ITokenService _tokenService;

    public LogoutCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        return _tokenService.RevokeRefreshTokenAsync(request.UserId, request.RefreshToken, cancellationToken);
    }
}
