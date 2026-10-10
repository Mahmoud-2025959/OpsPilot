using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.ConfigureBaseEntity();

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Slug).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.RepositoryUrl).HasMaxLength(500);
        builder.Property(s => s.OwnerTeamId);
        builder.Property(s => s.IsActive).IsRequired();

        // The same name is legitimate in two environments of one tenant (ERD §3).
        builder.HasIndex(s => new { s.TenantId, s.Name, s.Environment }).IsUnique();
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => new { s.TenantId, s.OwnerTeamId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // A team cannot be deleted out from under the services it owns.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(s => s.OwnerTeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
