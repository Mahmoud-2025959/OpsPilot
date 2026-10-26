namespace OpsPilot.Contracts.Auth;

/// <summary>
/// Names of custom claims carried by the access token, shared between the token issuer
/// (<c>TokenService</c>) and the API pipeline so the two can never drift apart.
/// </summary>
public static class AuthClaimTypes
{
    /// <summary>Identifier of the tenant the bearer belongs to. Drives tenant resolution.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>One claim per permission the bearer holds.</summary>
    public const string Permission = "permission";

    /// <summary>
    /// Identifier of the API key that authenticated the request, when one did. It rides along so
    /// an ingested event can name the key that produced it (ERD §3, "حوادث").
    /// </summary>
    public const string ApiKeyId = "apikey_id";
}
