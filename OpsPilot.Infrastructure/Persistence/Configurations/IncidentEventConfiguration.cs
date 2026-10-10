using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentEventConfiguration : IEntityTypeConfiguration<IncidentEvent>
{
    public void Configure(EntityTypeBuilder<IncidentEvent> builder)
    {
        builder.ToTable("IncidentEvents");
        builder.ConfigureBaseEntity();

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.IncidentId).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(e => e.Payload).HasMaxLength(4000);
        builder.Property(e => e.ApiKeyId);
        builder.Property(e => e.OccurredAt).IsRequired();

        // The timeline is read as "everything on this incident, oldest first".
        builder.HasIndex(e => new { e.TenantId, e.IncidentId, e.OccurredAt });
        builder.HasIndex(e => e.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // The timeline is meaningless without its incident (ERD §4).
        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Null when a user produced the event, hence Restrict and an optional FK.
        builder.HasOne<ApiKey>()
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
