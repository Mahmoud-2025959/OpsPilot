using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

public sealed class Service : TenantOwnedEntity
{
    private Service(
        string name,
        string slug,
        EnvironmentType environment,
        string? description,
        string? repositoryUrl,
        Guid? ownerTeamId)
    {
        Name = name;
        Slug = slug;
        Environment = environment;
        Description = description;
        RepositoryUrl = repositoryUrl;
        OwnerTeamId = ownerTeamId;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public EnvironmentType Environment { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Source repository backing this service, when one is known.</summary>
    public string? RepositoryUrl { get; private set; }

    /// <summary>Team that owns this service and is paged for its incidents.</summary>
    public Guid? OwnerTeamId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Service Create(
        Guid tenantId,
        string name,
        string slug,
        EnvironmentType environment,
        string? description = null,
        string? repositoryUrl = null,
        Guid? ownerTeamId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Service(
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            environment,
            description?.Trim(),
            repositoryUrl?.Trim(),
            ownerTeamId)
        {
            TenantId = tenantId
        };
    }

    /// <summary>Reassigns ownership. Passing null leaves the service unowned.</summary>
    public void AssignOwnerTeam(Guid? teamId)
    {
        OwnerTeamId = teamId;
    }

    public void ChangeRepository(string? repositoryUrl)
    {
        RepositoryUrl = repositoryUrl?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
