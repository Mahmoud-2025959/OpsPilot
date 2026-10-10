using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("WebhookEndpoints");
        builder.ConfigureBaseEntity();

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Url).IsRequired().HasMaxLength(500);
        builder.Property(e => e.SecretHash).IsRequired().HasMaxLength(256);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.TenantId);

        // A tenant must not be able to register the same destination twice, which would
        // deliver every notification to it more than once.
        builder.HasIndex(e => new { e.TenantId, e.Url }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
