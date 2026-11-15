using Microsoft.Extensions.Options;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Infrastructure.Security;
using Xunit;

namespace OpsPilot.UnitTests.Application;

/// <summary>
/// Covers the cipher that stands between a database dump and a usable ingestion key, and the
/// round trip that lets an operator read a key back after it was issued.
/// </summary>
public class ApiKeySecretProtectorTests
{
    private const string Key = "KioqKioqKioqKioqKioqKioqKioqKioqKioqKioqKio=";
    private const string OtherKey = "KioqKioqKioqKioqKioqKioqKioqKioqKioqKioqKis=";

    [Fact]
    public void A_Protected_Key_Reads_Back_As_Itself()
    {
        var protector = Protector();
        var raw = ApiKeySecrets.GenerateRawKey();

        var stored = protector.Protect(raw);

        Assert.Equal(raw, protector.Unprotect(stored));
    }

    [Fact]
    public void The_Stored_Value_Does_Not_Contain_The_Secret()
    {
        var raw = ApiKeySecrets.GenerateRawKey();

        var stored = Protector().Protect(raw);

        Assert.DoesNotContain(raw, stored);
        Assert.NotEqual(raw, stored);
    }

    [Fact]
    public void The_Same_Secret_Encrypts_Differently_Every_Time()
    {
        // A fresh nonce per write: encrypting the same secret twice must not produce the same
        // ciphertext, or equal plaintexts would be visible as equal ciphertexts.
        var protector = Protector();
        var raw = ApiKeySecrets.GenerateRawKey();

        Assert.NotEqual(protector.Protect(raw), protector.Protect(raw));
    }

    [Fact]
    public void A_Tampered_Ciphertext_Does_Not_Open()
    {
        var protector = Protector();
        var stored = protector.Protect(ApiKeySecrets.GenerateRawKey());

        var payload = Convert.FromBase64String(stored);
        payload[^1] ^= 0xFF;

        Assert.Null(protector.Unprotect(Convert.ToBase64String(payload)));
    }

    [Fact]
    public void A_Key_Encrypted_With_Another_Padlock_Does_Not_Open()
    {
        var stored = Protector(OtherKey).Protect(ApiKeySecrets.GenerateRawKey());

        Assert.Null(Protector().Unprotect(stored));
    }

    [Fact]
    public void A_Key_Encrypted_With_A_Retired_Padlock_Still_Opens()
    {
        // This is what makes the padlock changeable without a data migration: the retired key is
        // still tried when decrypting.
        var retired = Protector(OtherKey);
        var stored = retired.Protect(ApiKeySecrets.GenerateRawKey());

        var current = Protector(previousKeys: OtherKey);

        Assert.Equal(retired.Unprotect(stored), current.Unprotect(stored));
    }

    [Fact]
    public void Nothing_To_Decrypt_Is_Not_An_Error()
    {
        var protector = Protector();

        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(string.Empty));
        Assert.Null(protector.Unprotect("   "));
        Assert.Null(protector.Unprotect("not base64 at all!"));
        Assert.Null(protector.Unprotect(Convert.ToBase64String(new byte[4])));
    }

    [Fact]
    public void A_Missing_Key_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions()));

        Assert.Contains("ApiKeyEncryption:Key", exception.Message);
    }

    [Fact]
    public void A_Key_Of_The_Wrong_Length_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions { Key = "aGVsbG8=" }));

        Assert.Contains("32 bytes", exception.Message);
    }

    [Fact]
    public void A_Key_That_Is_Not_Base64_Is_Rejected_At_Startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions { Key = "not base64!" }));

        Assert.Contains("base64", exception.Message);
    }

    [Fact]
    public void A_Valid_Key_Passes_Validation()
    {
        ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions { Key = Key });
    }

    [Fact]
    public void A_Malformed_Retired_Key_Is_Rejected_At_Startup()
    {
        // A retired key that cannot be read would strand every row encrypted under it, so it
        // has to be caught at startup rather than at the first decryption.
        var exception = Assert.Throws<InvalidOperationException>(
            () => ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions
            {
                Key = Key,
                PreviousKeys = "not base64!"
            }));

        Assert.Contains("ApiKeyEncryption:PreviousKeys", exception.Message);
    }

    [Fact]
    public void Retired_Keys_Are_Validated_Together_With_The_Current_One()
    {
        ApiKeyEncryptionOptions.Validate(new ApiKeyEncryptionOptions
        {
            Key = Key,
            PreviousKeys = OtherKey
        });
    }

    private static AesGcmApiKeySecretProtector Protector(string? key = null, string? previousKeys = null) =>
        new(Options.Create(new ApiKeyEncryptionOptions
        {
            Key = key ?? Key,
            PreviousKeys = previousKeys ?? string.Empty
        }));
}
