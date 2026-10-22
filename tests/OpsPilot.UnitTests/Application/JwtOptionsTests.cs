using OpsPilot.Application.Common.Settings;
using Xunit;

namespace OpsPilot.UnitTests.Application;

/// <summary>
/// The signing key is a secret now: it lives in the development file or the environment, and
/// the committed file carries an empty placeholder. That only works if the application says so
/// loudly when the key is missing, rather than failing on the first login.
/// </summary>
public class JwtOptionsTests
{
    private const string SigningKey = "OpsPilot-test-signing-key-not-a-real-secret-32+";

    [Fact]
    public void A_Fully_Configured_Set_Passes_Validation()
    {
        JwtOptions.Validate(Options());
    }

    [Fact]
    public void A_Missing_Signing_Key_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.Validate(Options(signingKey: string.Empty)));

        Assert.Contains("Jwt:SigningKey", exception.Message);
    }

    [Fact]
    public void A_Signing_Key_That_Is_Too_Short_Is_Rejected_At_Startup()
    {
        // HMAC-SHA256 refuses anything under 256 bits. The crypto stack would say so, but not
        // in a way that points at the configuration.
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.Validate(Options(signingKey: "too-short")));

        Assert.Contains("32 bytes", exception.Message);
    }

    [Fact]
    public void A_Missing_Issuer_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.Validate(Options(issuer: string.Empty)));

        Assert.Contains("Jwt:Issuer", exception.Message);
    }

    [Fact]
    public void A_Missing_Audience_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtOptions.Validate(Options(audience: string.Empty)));

        Assert.Contains("Jwt:Audience", exception.Message);
    }

    private static JwtOptions Options(
        string issuer = "OpsPilot",
        string audience = "OpsPilot.Client",
        string signingKey = SigningKey) =>
        new()
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = signingKey
        };
}
