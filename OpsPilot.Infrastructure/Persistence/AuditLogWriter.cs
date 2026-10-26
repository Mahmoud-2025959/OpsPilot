using System.Text.Json;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Appends entries to the tenant's audit trail (plan, FR-18).
/// </summary>
/// <remarks>
/// The tenant and the acting user are read from the ambient scope rather than taken as
/// parameters: every caller of this writer is acting inside a request that has already been
/// authenticated and resolved to one company, and an audit record that could name a different
/// one would be worse than no record at all. A request with no signed-in user (a machine
/// credential, a background sweep) is recorded with a null actor, which the entity documents
/// as the marker for a system action.
/// </remarks>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public AuditLogWriter(
        AppDbContext context,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        // Fail loudly rather than filing the entry under an unresolved tenant: the query filter
        // would hide the row from everyone, turning a mistake into silently lost history.
        if (!_currentTenant.IsResolved)
        {
            throw new InvalidOperationException(
                $"Cannot record audit entry '{entry.Action}' before a tenant has been resolved.");
        }

        var log = AuditLog.Create(
            _currentTenant.TenantId,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            actorUserId: ParseActor(),
            oldValues: Serialize(entry.OldValues),
            newValues: Serialize(entry.NewValues));

        await _context.AuditLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Guid? ParseActor() =>
        Guid.TryParse(_currentUser.UserId, out var id) ? id : null;

    /// <summary>
    /// Snapshots are small by construction — only the fields the action changed — so plain
    /// default serialisation is enough. Enums go through as their names, matching how they are
    /// stored everywhere else in this database.
    /// </summary>
    private static string? Serialize(object? values) =>
        values is null
            ? null
            : JsonSerializer.Serialize(values, new JsonSerializerOptions
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
}
