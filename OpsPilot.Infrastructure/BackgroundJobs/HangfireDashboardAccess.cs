using System.Security.Cryptography;
using System.Text;

namespace OpsPilot.Infrastructure.BackgroundJobs;

/// <summary>
/// Pure decision for whether a Hangfire dashboard request is allowed. Kept out of the ASP.NET
/// filter so the rules can be tested without spinning up Hangfire's dashboard types.
/// </summary>
public static class HangfireDashboardAccess
{
    /// <summary>
    /// Production always requires Basic credentials. Outside Production, an empty username means
    /// the dashboard is open (the local developer case). Once a username is configured, Basic
    /// auth is required in every environment.
    /// </summary>
    public static bool Allow(
        HangfireOptions options,
        bool isProduction,
        string? authorizationHeader)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.DashboardUsername))
        {
            return MatchesBasic(
                authorizationHeader,
                options.DashboardUsername,
                options.DashboardPassword ?? string.Empty);
        }

        return !isProduction;
    }

    public static bool MatchesBasic(string? authorizationHeader, string username, string password)
    {
        if (string.IsNullOrEmpty(authorizationHeader) ||
            !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encoded = authorizationHeader["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separator = decoded.IndexOf(':');

            if (separator < 0)
            {
                return false;
            }

            var presentedUser = decoded[..separator];
            var presentedPassword = decoded[(separator + 1)..];

            return FixedEquals(presentedUser, username) && FixedEquals(presentedPassword, password);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            CryptographicOperations.FixedTimeEquals(leftBytes, leftBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
