namespace OpsPilot.API.RateLimiting;

/// <summary>Settings for the ingestion throttle (plan, UC-06: 429 over the limit).</summary>
public sealed class IngestionRateLimitOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "RateLimiting:Ingestion";

    /// <summary>How many events one key may post within a window.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>How long one window lasts.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
