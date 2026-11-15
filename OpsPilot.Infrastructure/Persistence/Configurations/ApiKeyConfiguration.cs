using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");
        builder.ConfigureBaseEntity();

        builder.Property(k => k.TenantId).IsRequired();
        builder.Property(k => k.Name).IsRequired().HasMaxLength(200);
        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(16);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(256);

        // The encrypted copy of the secret, so a key can be read back after it was issued.
        // Null for rows that predate the column, and for a secret that could not be encrypted.
        builder.Property(k => k.KeyCiphertext).HasMaxLength(512);

        builder.Property(k => k.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(k => k.ExpiresAt).IsRequired();
        builder.Property(k => k.LastUsedAt);

        // The prefix is the only non-secret part of a key and is what the UI lists, so it has
        // to identify a single key (ERD §3).
        builder.HasIndex(k => k.KeyPrefix).IsUnique();
        builder.HasIndex(k => new { k.TenantId, k.Name }).IsUnique();
        builder.HasIndex(k => k.TenantId);

        builder.Ignore(k => k.IsActive);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(k => k.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
