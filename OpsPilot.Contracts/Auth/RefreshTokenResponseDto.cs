namespace OpsPilot.Contracts.Auth;

public sealed record RefreshTokenResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
