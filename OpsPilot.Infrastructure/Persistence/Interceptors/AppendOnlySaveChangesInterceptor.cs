using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces the append-only rule for <see cref="IAppendOnly"/> entities (ERD §1).
/// </summary>
/// <remarks>
/// This is a data-integrity invariant, not a business rule, so it is enforced at the
/// persistence boundary where it cannot be bypassed — including by raw SQL executed through
/// this context. It fails the whole unit of work rather than silently skipping the change,
/// so a caller attempting to rewrite history gets an immediate, unambiguous error.
/// </remarks>
public sealed class AppendOnlySaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            Guard(context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            Guard(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext context)
    {
        // Deleting an incident removes its timeline with it (the schema declares
        // DeleteBehavior.Cascade on the incident relationship), so those deletes are the
        // intended end of an incident, never a rewrite of history. Every other append-only
        // change keeps failing the whole unit of work.
        var deletedIncidentIds = context.ChangeTracker.Entries<Incident>()
            .Where(e => e.State == EntityState.Deleted)
            .Select(e => e.Entity.Id)
            .ToHashSet();

        var violations = context.ChangeTracker.Entries<IAppendOnly>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Where(e => !IsCascadeFromDeletedIncident(e, deletedIncidentIds))
            .ToList();

        if (violations.Count == 0)
        {
            return;
        }

        var detail = string.Join(
            Environment.NewLine,
            violations.Select(Describe));

        throw new AppendOnlyEntityModifiedException(
            $"Append-only records cannot be updated or deleted:{Environment.NewLine}{detail}");
    }

    /// <summary>
    /// True when the entry is the dependent side of an incident that is itself being deleted
    /// in the same unit of work. Only deletes qualify; updates are never exempted, and
    /// entities without an incident, such as <see cref="AuditLog"/>, are never matched.
    /// </summary>
    private static bool IsCascadeFromDeletedIncident(
        EntityEntry entry,
        IReadOnlySet<Guid> deletedIncidentIds)
    {
        if (entry.State != EntityState.Deleted)
        {
            return false;
        }

        var incidentIdProperty = entry.Metadata.FindProperty(nameof(IncidentEvent.IncidentId));
        if (incidentIdProperty is null)
        {
            return false;
        }

        return entry.Property(incidentIdProperty).CurrentValue is Guid incidentId
            && deletedIncidentIds.Contains(incidentId);
    }

    private static string Describe(EntityEntry entry) => entry.State switch
    {
        EntityState.Modified => $"  - UPDATE {entry.Metadata.ClrType.Name} (key {ReadKey(entry)})",
        EntityState.Deleted => $"  - DELETE {entry.Metadata.ClrType.Name} (key {ReadKey(entry)})",
        _ => $"  - {entry.State} {entry.Metadata.ClrType.Name}"
    };

    private static string ReadKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return "<unknown>";
        }

        return string.Join(
            "/",
            key.Properties.Select(p => $"{p.Name}={entry.Property(p.Name).CurrentValue}"));
    }
}
