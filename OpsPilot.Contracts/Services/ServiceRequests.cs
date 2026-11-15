namespace OpsPilot.Contracts.Services;

/// <summary>
/// Registers a service in the catalogue. The owning tenant comes from the bearer token, never
/// from the body.
/// </summary>
/// <param name="Slug">
/// Optional short handle. When omitted it is derived from <paramref name="Name"/>.
/// </param>
public sealed record CreateServiceRequest(
    string Name,
    string Environment,
    string? Slug = null,
    string? Description = null,
    string? RepositoryUrl = null,
    Guid? OwnerTeamId = null);

/// <summary>
/// Edits the mutable fields of a service. The identity triple (name, environment) stays unique
/// per tenant, so moving a service onto an existing name is a conflict.
/// </summary>
public sealed record UpdateServiceRequest(
    string Name,
    string Environment,
    string? Slug = null,
    string? Description = null,
    string? RepositoryUrl = null,
    Guid? OwnerTeamId = null);
