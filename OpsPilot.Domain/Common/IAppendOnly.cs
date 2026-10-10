namespace OpsPilot.Domain.Common;

/// <summary>
/// Marks an entity that is written once and then never updated or deleted (ERD §1).
/// </summary>
/// <remarks>
/// The persistence layer rejects any attempt to modify or delete an implementation,
/// so the audit trail and the incident timeline cannot be rewritten after the fact.
/// </remarks>
public interface IAppendOnly;
