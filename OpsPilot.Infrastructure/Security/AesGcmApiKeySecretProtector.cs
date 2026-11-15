using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Security;

/// <summary>
/// Encrypts stored secrets with AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// GCM is authenticated, not just scrambled: the tag travels with the ciphertext, so a
/// tampered row fails to decrypt instead of yielding a corrupted value. A fresh nonce per
/// write is what makes that safe — reusing one under the same key would leak the XOR of two
/// plaintexts, so the nonce is drawn from the CSPRNG every time and never stored.
/// </para>
/// <para>
/// The layout of the stored value is nonce, then tag, then ciphertext, all base64-encoded
/// together. Decryption tries the current key first and then the retired ones, so changing
/// the configured key does not strand rows encrypted under the old one. Used for ingestion
/// keys and webhook secrets.
/// </para>
/// </remarks>
public sealed class AesGcmApiKeySecretProtector : ISecretProtector
{
    /// <summary>The key length AES-256 expects.</summary>
    public const int KeySize = ApiKeyEncryptionOptions.KeySize;

    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IReadOnlyList<byte[]> _keys;

    public AesGcmApiKeySecretProtector(IOptions<ApiKeyEncryptionOptions> options)
    {
        _keys = ApiKeyEncryptionOptions.Resolve(options.Value);
    }

    public string Protect(string rawSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);

        var plaintext = Encoding.UTF8.GetBytes(rawSecret);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);

        using var aes = new AesGcm(_keys[0], TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];

        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return null;
        }

        byte[] payload;

        try
        {
            payload = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException)
        {
            // Not something this protector wrote. Nothing to recover from it.
            return null;
        }

        if (payload.Length < NonceSize + TagSize)
        {
            return null;
        }

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var encrypted = payload[(NonceSize + TagSize)..];

        foreach (var key in _keys)
        {
            try
            {
                var plaintext = new byte[encrypted.Length];

                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, encrypted, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
            catch (CryptographicException)
            {
                // Wrong key for this ciphertext — try the next one.
            }
        }

        return null;
    }
}
