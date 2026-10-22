namespace OpsPilot.Application.Abstractions;

/// <summary>
/// One sensitive action to record on the tenant's audit trail (plan, FR-18).
/// </summary>
/// <remarks>
/// The values are passed as objects and serialised by the writer, so every call site records
/// snapshots in the same shape without repeating the serialisation. They are kept deliberately
/// small — the fields the action changed, not whole entities — which is what makes a trail
/// entry readable at a glance.
/// </remarks>
public sealed record AuditEntry(
    string Action,
    string EntityType,
    string EntityId,
    object? OldValues = null,
    object? NewValues = null);

/// <summary>
/// Appends entries to the audit trail. The trail is append-only (ERD §1), so this interface
/// offers no way to amend or remove what has been recorded.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Records one action. The tenant and the acting user come from the ambient scope — the
    /// same sources the rest of the request pipeline trusts — so a handler cannot attribute an
    /// action to a company or a person it is not acting on behalf of.
    /// </summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
