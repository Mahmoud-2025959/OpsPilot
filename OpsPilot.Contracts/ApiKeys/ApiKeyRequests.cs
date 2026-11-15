namespace OpsPilot.Contracts.ApiKeys;

/// <summary>
/// Issues a new ingestion key. The raw secret is returned once, in the response, and is never
/// stored or shown again (plan, FR-13).
/// </summary>
/// <param name="ExpiresAt">
/// When the key stops working. Optional — a sensible default lifetime is applied when omitted.
/// </param>
public sealed record CreateApiKeyRequest(string Name, DateTimeOffset? ExpiresAt = null);
