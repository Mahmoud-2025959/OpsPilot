namespace OpsPilot.Contracts.Tenants;

/// <summary>
/// The company the request was made in (ERD §1).
/// </summary>
/// <param name="Id">The company's id.</param>
/// <param name="Name">The company's display name.</param>
/// <param name="Slug">The stable, URL-safe handle the company was signed up with.</param>
/// <param name="IsActive">
/// False while the company is deactivated. A deactivated company cannot sign in; its data is
/// kept exactly where it is.
/// </param>
/// <param name="CreatedAt">When the company was created.</param>
/// <param name="UpdatedAt">When it was last written to.</param>
public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
