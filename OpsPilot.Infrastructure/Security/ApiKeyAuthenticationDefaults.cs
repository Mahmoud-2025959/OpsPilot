namespace OpsPilot.Infrastructure.Security;

/// <summary>
/// Names the moving parts of the ingestion security path so the pipeline and the endpoints agree
/// on them in one place.
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    /// <summary>Scheme that authenticates a request presenting <see cref="HeaderName"/>.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>Header an application uses to present its key (plan, UC-06).</summary>
    public const string HeaderName = "X-API-Key";

    /// <summary>
    /// The default scheme. It is a policy scheme that forwards to <see cref="Scheme"/> when the
    /// key header is present and to the JWT bearer scheme otherwise, so both audiences reach the
    /// same pipeline and tenant resolution sees a populated principal either way.
    /// </summary>
    public const string SmartScheme = "OpsPilot";
}
