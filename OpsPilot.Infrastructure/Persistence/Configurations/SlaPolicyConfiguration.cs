using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies");
        builder.ConfigureBaseEntity();

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.ResponseMinutes).IsRequired();
        builder.Property(p => p.ResolutionMinutes).IsRequired();

        // Exactly one policy row per severity, per tenant (ERD §3).
        builder.HasIndex(p => new { p.TenantId, p.Severity }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
