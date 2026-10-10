namespace OpsPilot.Domain.Common;

public interface IAuditableEntity
{
    string? CreatedBy { get; set; }

    string? UpdatedBy { get; set; }

    DateTimeOffset CreatedAt { get; }

    DateTimeOffset? UpdatedAt { get; set; }
}
