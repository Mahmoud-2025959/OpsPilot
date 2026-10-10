namespace OpsPilot.Domain.Common;

public abstract class BaseEntity : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public bool IsDeleted { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public void SoftDelete() => IsDeleted = true;

    public void Restore() => IsDeleted = false;
}