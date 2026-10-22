using System.Text;

namespace OpsPilot.Domain.Common;

/// <summary>
/// Derives a URL-safe handle from a human-readable name.
/// </summary>
/// <remarks>
/// Company handles and service slugs are both stored in 100-character columns, so truncation has
/// to happen here rather than at the database. Without it an over-long name surfaces as a
/// <c>SqlException</c> (a 500) instead of a clean validation failure.
/// </remarks>
public static class Slug
{
    /// <summary>Length of the longest slug column in the model (companies and services).</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Lower-cases <paramref name="value"/>, turns every run of non-alphanumeric characters into a
    /// single dash, trims leading and trailing dashes, and truncates to <paramref name="maxLength"/>.
    /// Returns an empty string when nothing usable remains.
    /// </summary>
    public static string From(string? value, int maxLength = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var lowered = value.Trim().ToLowerInvariant();

        var builder = new StringBuilder(lowered.Length);
        var lastWasDash = false;

        foreach (var c in lowered)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].Trim('-');
        }

        return slug;
    }

    /// <summary>
    /// Derives a slug, falling back to <paramref name="fallback"/> when the name yields nothing
    /// usable (for example a name made entirely of punctuation).
    /// </summary>
    public static string FromName(string? value, string fallback, int maxLength = MaxLength)
    {
        var slug = From(value, maxLength);

        if (slug.Length > 0)
        {
            return slug;
        }

        var trimmedFallback = From(fallback, maxLength);

        return trimmedFallback.Length > 0 ? trimmedFallback : "company";
    }
}
