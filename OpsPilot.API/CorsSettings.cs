namespace OpsPilot.API;

/// <summary>
/// Optional CORS policy for a separate frontend (plan, deployment hardening). Empty origins
/// means the API is same-origin / API-only and no CORS middleware is registered.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public const string PolicyName = "frontend";

    public string[] AllowedOrigins { get; set; } = [];
}
