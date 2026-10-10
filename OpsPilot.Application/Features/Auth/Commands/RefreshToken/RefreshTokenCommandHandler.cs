using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public Task<RefreshTokenResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return _tokenService.RefreshTokensAsync(request.RefreshToken, cancellationToken);
    }
}
