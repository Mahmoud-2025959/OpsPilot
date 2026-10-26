using System.Security.Cryptography;
using System.Text;

namespace OpsPilot.Infrastructure.Webhooks;

/// <summary>
/// Signs a webhook call so the receiver can tell that it came from OpsPilot and was not
/// altered on the way (plan, notifications group: the webhook channel).
/// </summary>
/// <remarks>
/// <para>
/// The signature covers the timestamp and the body together, joined by a dot. Signing the
/// body alone would let an old, correctly signed call be replayed; signing the timestamp
/// alone would leave the body open to alteration. Together they make a captured call worth
/// nothing once it is old — the receiver compares the timestamp against its own clock and
/// refuses anything that is too old.
/// </para>
/// <para>
/// The shape is the one every webhook provider converged on: an HMAC-SHA256 in hexadecimal,
/// prefixed with the algorithm it was made with, so a receiver that has never heard of this
/// system can still verify it.
/// </para>
/// </remarks>
public static class WebhookSigner
{
    /// <summary>The header naming the kind of event being announced.</summary>
    public const string EventHeader = "X-OpsPilot-Event";

    /// <summary>The header carrying the id of the delivery.</summary>
    public const string DeliveryHeader = "X-OpsPilot-Delivery";

    /// <summary>The header carrying the moment the call was made, in Unix seconds.</summary>
    public const string TimestampHeader = "X-OpsPilot-Timestamp";

    /// <summary>The header carrying the signature.</summary>
    public const string SignatureHeader = "X-OpsPilot-Signature";

    /// <summary>
    /// Signs a body as it was made at a moment in time.
    /// </summary>
    /// <param name="secret">The shared signing secret.</param>
    /// <param name="timestamp">When the call was made.</param>
    /// <param name="body">The exact body that goes on the wire.</param>
    public static string Sign(string secret, DateTimeOffset timestamp, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(body);

        var payload = $"{timestamp.ToUnixTimeSeconds()}.{body}";

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload));

        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
