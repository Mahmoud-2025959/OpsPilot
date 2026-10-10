using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    /// <summary>
    /// Restricts uniqueness to incidents that are still open. Without the filter, once an
    /// incident is resolved its correlation key could never be reused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two deliberate departures from the ERD's literal <c>NOT IN ('Resolved','Closed')</c>:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// SQL Server does not accept a leading NOT in a filtered-index predicate — both
    /// <c>NOT IN</c> and <c>NOT LIKE</c> fail to parse, so the equivalent pair of
    /// positive <c>&lt;&gt;</c> comparisons is used instead. <c>Status</c> is non-nullable,
    /// so the two forms are exactly equivalent.
    /// </item>
    /// <item>
    /// The correlation-key IS NOT NULL clause is load-bearing: SQL Server treats NULLs as
    /// equal to each other in a unique index, so an uncorrelated incident would otherwise
    /// collide with every other uncorrelated incident in the same tenant.
    /// </item>
    /// </list>
    /// </remarks>
    private const string OpenIncidentFilter =
        "[CorrelationKey] IS NOT NULL AND [Status] <> 'Resolved' AND [Status] <> 'Closed'";

    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.ConfigureBaseEntity();

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.ServiceId).IsRequired();
        builder.Property(i => i.Title).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(2000);
        builder.Property(i => i.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.OccurrenceCount).IsRequired();
        builder.Property(i => i.CorrelationKey).HasMaxLength(128);
        builder.Property(i => i.AssignedTo);
        builder.Property(i => i.SlaPolicyId);
        builder.Property(i => i.LastEventAt);
        builder.Property(i => i.AcknowledgedAt);
        builder.Property(i => i.InvestigatingAt);
        builder.Property(i => i.MitigatedAt);
        builder.Property(i => i.ResolvedAt);
        builder.Property(i => i.ClosedAt);
        builder.Property(i => i.ResponseDueAt);
        builder.Property(i => i.ResolutionDueAt);

        // The ERD names this column CreatedBy, but BaseEntity already owns a string audit
        // member of that name, so the property is disambiguated and the column is not.
        builder.Property(i => i.CreatedByUserId)
            .HasColumnName("CreatedBy")
            .IsRequired();

        // Optimistic concurrency (ERD §1). Modelled as a shadow property because row
        // versioning is a storage concern; the domain has no use for the token, and EF
        // checks and refreshes it automatically.
        builder.Property<byte[]>(ShadowProperties.RowVersion)
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.ServiceId, i.CreatedAt });
        builder.HasIndex(i => new { i.TenantId, i.CorrelationKey });
        builder.HasIndex(i => new { i.TenantId, i.AssignedTo });

        // The database, not the application, is what guarantees two concurrent events cannot
        // open two incidents for the same fault (ERD §3, "فهرس ضم الأحداث").
        builder.HasIndex(i => new { i.TenantId, i.CorrelationKey })
            .IsUnique()
            .HasFilter(OpenIncidentFilter);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(i => i.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.AssignedTo)
            .OnDelete(DeleteBehavior.Restrict);

        // Incidents outlive policy edits, so a policy cannot be removed while referenced.
        builder.HasOne<SlaPolicy>()
            .WithMany()
            .HasForeignKey(i => i.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
