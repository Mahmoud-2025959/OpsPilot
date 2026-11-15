using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Common;

/// <summary>
/// Derives the key that groups repeated reports of the same underlying fault (ERD §3,
/// "مفتاح الضم").
/// </summary>
/// <remarks>
/// <para>
/// One outage usually announces itself over and over, and only the variable parts of the message
/// change. Hashing <c>service + environment + normalized message</c> gives every one of those
/// reports the same key, so a later one joins the incident the first one opened instead of
/// opening a new incident each time. The database's filtered unique index is what makes that
/// rule hold under concurrency; this helper only decides what "the same fault" means.
/// </para>
/// <para>
/// Normalization is deliberately simple — the plan asks for a first cut that can be improved:
/// the message is lower-cased, every run of digits is dropped (so <c>timed out after 30s</c> and
/// <c>timed out after 31s</c> collapse together) and whitespace is collapsed.
/// </para>
/// </remarks>
public static class CorrelationKey
{
    private static readonly Regex DigitRuns = new("[0-9]+", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRuns = new("\\s+", RegexOptions.Compiled);

    /// <summary>
    /// Computes the correlation key for an event. The result is a lowercase hex digest, well
    /// inside the 128 characters the incident column allows.
    /// </summary>
    public static string Compute(Guid serviceId, EnvironmentType environment, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var material = $"{serviceId:N}|{environment}|{Normalize(message)}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    /// <summary>Strips the variable parts of a message so that near-identical reports match.</summary>
    public static string Normalize(string message)
    {
        var withoutDigits = DigitRuns.Replace(message.ToLowerInvariant(), " ");
        return WhitespaceRuns.Replace(withoutDigits, " ").Trim();
    }
}
