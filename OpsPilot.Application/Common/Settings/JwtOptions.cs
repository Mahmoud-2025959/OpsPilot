namespace OpsPilot.Application.Common.Settings;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int DurationInMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 7;
}
