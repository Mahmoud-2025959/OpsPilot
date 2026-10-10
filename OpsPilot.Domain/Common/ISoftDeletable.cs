namespace OpsPilot.Domain.Common;

/// <summary>
/// Marks an entity that is retired rather than removed, so historical rows keep their
/// references.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>True once the row has been retired. Ordinary queries hide retired rows.</summary>
    bool IsDeleted { get; set; }
}
