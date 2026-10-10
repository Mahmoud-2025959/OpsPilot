using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class PostmortemConfiguration : IEntityTypeConfiguration<Postmortem>
{
    public void Configure(EntityTypeBuilder<Postmortem> builder)
    {
        builder.ToTable("Postmortems");
        builder.ConfigureBaseEntity();

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.IncidentId).IsRequired();
        builder.Property(p => p.Title).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Summary).IsRequired().HasMaxLength(8000);
        builder.Property(p => p.RootCause).HasMaxLength(8000);
        builder.Property(p => p.ActionItems).HasMaxLength(8000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PublishedAt);

        // ERD names the column CreatedBy; see IncidentConfiguration for why the property differs.
        builder.Property(p => p.CreatedByUserId)
            .HasColumnName("CreatedBy")
            .IsRequired();

        // One retrospective per incident (ERD §3).
        builder.HasIndex(p => p.IncidentId).IsUnique();
        builder.HasIndex(p => p.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(p => p.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
