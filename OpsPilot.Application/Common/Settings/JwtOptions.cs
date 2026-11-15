using System.Text;

namespace OpsPilot.Application.Common.Settings;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The smallest signing key HMAC-SHA256 accepts, in bytes. Anything shorter is refused by
    /// the crypto stack with a message that says nothing about where the key came from.
    /// </summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// The key that signs and verifies access tokens. It is a secret: a deployment supplies it
    /// through the environment, never through a committed file.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int DurationInMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// Fails fast on a misconfigured installation. Tokens signed with a key nobody knows are
    /// useless, and tokens signed with a key that is too short are refused by the crypto stack
    /// with a message that says nothing about where the key came from — so the problem is
    /// reported here, at startup, in the same place as every other missing secret.
    /// </summary>
    /// <exception cref="InvalidOperationException">A value is missing or the key is too short.</exception>
    public static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException(
                $"'{SectionName}:Issuer' is not configured. Set it through appsettings or the " +
                $"'{SectionName}__Issuer' environment variable.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException(
                $"'{SectionName}:Audience' is not configured. Set it through appsettings or the " +
                $"'{SectionName}__Audience' environment variable.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException(
                $"'{SectionName}:SigningKey' is not configured. Set it to a base64-encoded key of " +
                $"at least {MinimumSigningKeyLength} bytes (generate one with 'openssl rand -base64 32') " +
                $"through appsettings, user-secrets, or the '{SectionName}__SigningKey' environment variable.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:SigningKey' must be at least {MinimumSigningKeyLength} bytes long; " +
                $"it is {Encoding.UTF8.GetByteCount(options.SigningKey)}.");
        }
    }
}
