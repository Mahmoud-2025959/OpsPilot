namespace OpsPilot.Infrastructure.Security;

/// <summary>
/// The key that protects the stored copy of an ingestion key (ERD §3, "التدقيق والمفاتيح").
/// </summary>
/// <remarks>
/// <para>
/// This is the one secret that is not a credential: it is the padlock on the credentials. A
/// deployment supplies it through the environment — never through a committed file, because a
/// database dump plus this key is every ingestion key in the clear. Development uses the
/// throwaway value in appsettings.Development.json.
/// </para>
/// <para>
/// The value is a base64-encoded 32-byte key. <see cref="PreviousKeys"/> exists so the padlock
/// can be changed without a data migration: the first key encrypts, and every key in the list
/// is tried when decrypting, which lets rows written under the old key keep working until
/// they are rotated.
/// </para>
/// </remarks>
public sealed class ApiKeyEncryptionOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "ApiKeyEncryption";

    /// <summary>The length of the padlock, in bytes.</summary>
    public const int KeySize = 32;

    /// <summary>Base64-encoded 32-byte key. Required.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated base64 keys that are still accepted for decryption but no longer used
    /// to encrypt.
    /// </summary>
    public string PreviousKeys { get; set; } = string.Empty;

    /// <summary>
    /// Fails fast on a misconfigured installation. The application cannot store a key it can
    /// read back, and finding that out on the first create — rather than at startup — is how
    /// a key ends up stored in a form nobody can retrieve.
    /// </summary>
    /// <exception cref="InvalidOperationException">A key is missing, malformed, or the wrong length.</exception>
    public static void Validate(ApiKeyEncryptionOptions options)
    {
        Decode(options.Key, $"{SectionName}:Key");

        foreach (var entry in Split(options.PreviousKeys))
        {
            Decode(entry, $"{SectionName}:PreviousKeys");
        }
    }

    /// <summary>
    /// The padlocks in the order they are tried: the current one first, then the retired ones.
    /// </summary>
    public static IReadOnlyList<byte[]> Resolve(ApiKeyEncryptionOptions options)
    {
        var keys = new List<byte[]> { Decode(options.Key, $"{SectionName}:Key") };

        foreach (var entry in Split(options.PreviousKeys))
        {
            keys.Add(Decode(entry, $"{SectionName}:PreviousKeys"));
        }

        return keys;
    }

    /// <summary>The entries of a comma-separated list, with the blanks dropped.</summary>
    private static IEnumerable<string> Split(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Turns a configured key into the bytes the cipher takes. The failure mode is deliberately
    /// loud: a key that is present but wrong would otherwise silently make every stored secret
    /// unreadable.
    /// </summary>
    internal static byte[] Decode(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"'{name}' is not configured. Set it to a base64-encoded {KeySize}-byte key " +
                $"(generate one with 'openssl rand -base64 32') through appsettings, user-secrets, " +
                $"or the '{SectionName}__Key' environment variable.");
        }

        byte[] decoded;

        try
        {
            decoded = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"'{name}' is not valid base64.", exception);
        }

        if (decoded.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"'{name}' must decode to exactly {KeySize} bytes; it decoded to {decoded.Length}.");
        }

        return decoded;
    }
}
