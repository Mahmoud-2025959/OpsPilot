using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

public sealed class Permission : BaseEntity
{
    private Permission(string name, string category, string? description)
    {
        Name = name;
        Category = category;
        Description = description;
    }

    public string Name { get; private set; }

    public string Category { get; private set; }

    public string? Description { get; private set; }

    public static Permission Create(string name, string category, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return new Permission(name.Trim(), category.Trim(), description?.Trim());
    }
}