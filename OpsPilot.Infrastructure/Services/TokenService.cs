using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Settings;
using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptionsMonitor<JwtOptions> _jwtOptions;
    private readonly TimeProvider _timeProvider;

    public TokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IOptionsMonitor<JwtOptions> jwtOptions,
        TimeProvider timeProvider)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _jwtOptions = jwtOptions;
        _timeProvider = timeProvider;
    }

    public Task<string> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var options = _jwtOptions.CurrentValue;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email ?? string.Empty),
            // The tenant travels with the token so the API can resolve it on every request
            // without a database round trip (ERD §3, "Identity و Tenancy").
            new(AuthClaimTypes.TenantId, user.TenantId.ToString())
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name ?? string.Empty));

            foreach (var permission in role.Permissions)
            {
                if (!claims.Any(c => c.Type == AuthClaimTypes.Permission && c.Value == permission.Name))
                {
                    claims.Add(new Claim(AuthClaimTypes.Permission, permission.Name));
                }
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(options.DurationInMinutes);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public async Task<GeneratedRefreshToken> GenerateRefreshTokenAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var options = _jwtOptions.CurrentValue;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashToken(rawToken);

        var token = RefreshToken.Create(
            user.TenantId,
            user.Id,
            tokenHash,
            _timeProvider.GetUtcNow().AddDays(options.RefreshTokenDays));

        await _refreshTokenRepository.AddAsync(token, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new GeneratedRefreshToken(token, rawToken);
    }

    public async Task<RefreshTokenResponseDto> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException(ExceptionMessages.InvalidOrExpiredRefreshToken);

        var utcNow = _timeProvider.GetUtcNow();

        if (storedToken.IsRevoked)
        {
            throw new UnauthorizedException(ExceptionMessages.RefreshTokenRevoked);
        }

        if (storedToken.ExpiresAt < utcNow)
        {
            throw new UnauthorizedException(ExceptionMessages.InvalidOrExpiredRefreshToken);
        }

        if (storedToken.IsUsed)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserId, cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(ExceptionMessages.RefreshTokenReuseDetected);
        }

        var user = await _userRepository.FindByIdAcrossTenantsAsync(storedToken.UserId, cancellationToken)
            ?? throw new UnauthorizedException(ExceptionMessages.InvalidOrExpiredRefreshToken);

        var accessToken = await GenerateAccessTokenAsync(user, cancellationToken);
        var newRefreshToken = await GenerateRefreshTokenAsync(user, cancellationToken);

        // Claim the presented token atomically and link it to its replacement, so replaying it
        // later can be traced along the whole chain rather than just flagged at one row
        // (ERD §3). The claim is a single conditional write, so if two refreshes race with the
        // same token only one wins; the loser sees the token already consumed and is treated as
        // a replay, which revokes the whole chain.
        if (!await _refreshTokenRepository.TryConsumeAsync(
                storedToken.Id,
                newRefreshToken.Entity.Id,
                utcNow,
                cancellationToken))
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserId, cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(ExceptionMessages.RefreshTokenReuseDetected);
        }

        var options = _jwtOptions.CurrentValue;

        return new RefreshTokenResponseDto(
            accessToken,
            newRefreshToken.RawToken,
            utcNow.UtcDateTime.AddMinutes(options.DurationInMinutes));
    }

    public async Task RevokeRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new UnauthorizedException(ExceptionMessages.RefreshTokenNotFound);

        if (storedToken.UserId != userId)
        {
            throw new UnauthorizedException(ExceptionMessages.RefreshTokenNotBelongToUser);
        }

        if (!storedToken.IsRevoked)
        {
            storedToken.Revoke(_timeProvider.GetUtcNow());
            await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
