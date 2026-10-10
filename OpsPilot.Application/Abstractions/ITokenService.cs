using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public sealed record GeneratedRefreshToken(RefreshToken Entity, string RawToken);

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default);

    Task<GeneratedRefreshToken> GenerateRefreshTokenAsync(User user, CancellationToken cancellationToken = default);

    Task<RefreshTokenResponseDto> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);
}
