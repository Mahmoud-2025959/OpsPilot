using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class SlaViolationConfiguration : IEntityTypeConfiguration<SlaViolation>
{
    public void Configure(EntityTypeBuilder<SlaViolation> builder)
    {
        builder.ToTable("SlaViolations");
        builder.ConfigureBaseEntity();

        builder.Property(v => v.TenantId).IsRequired();
        builder.Property(v => v.IncidentId).IsRequired();

        // ERD names the column Type; the property stays SlaType so the call site reads well.
        builder.Property(v => v.SlaType)
            .HasColumnName("Type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.DueAt).IsRequired();
        builder.Property(v => v.DetectedAt).IsRequired();
        builder.Property(v => v.ResolvedAt);

        // This is what makes the detection job idempotent: re-running it cannot produce a
        // second breach row, and therefore cannot re-notify (ERD §3).
        builder.HasIndex(v => new { v.IncidentId, v.SlaType }).IsUnique();
        builder.HasIndex(v => v.TenantId);

        builder.Ignore(v => v.IsOpen);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(v => v.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
