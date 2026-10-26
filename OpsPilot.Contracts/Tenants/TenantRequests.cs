namespace OpsPilot.Contracts.Tenants;

/// <summary>
/// Renames the company. The handle the company signed up with is not changeable: it appears in
/// the tokens and in the URLs people have already shared.
/// </summary>
/// <param name="Name">The new display name.</param>
public sealed record RenameTenantRequest(string Name);
