using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.ConfigureBaseEntity();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.EndpointId).IsRequired();
        builder.Property(d => d.EventType).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Payload).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.Attempts).IsRequired();
        builder.Property(d => d.DeliveredAt);

        // Bounded like the outbox's Error column: the reason an exception carries can be longer
        // than any column should hold, and the dispatcher truncates before it writes so a
        // recording of why delivery failed never becomes a failure of its own.
        builder.Property(d => d.LastError).HasMaxLength(2000);

        // The dispatcher polls for the waiting calls over and over, so the index covers only
        // those and stays small no matter how large the table grows.
        builder.HasIndex(d => new { d.TenantId, d.Status, d.CreatedAt });
        builder.HasIndex(d => d.EndpointId);
        builder.HasIndex(d => d.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // The calls already made still point at the endpoint they were made to, so an endpoint
        // cannot be erased out from under them.
        builder.HasOne<WebhookEndpoint>()
            .WithMany()
            .HasForeignKey(d => d.EndpointId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
