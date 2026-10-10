using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.ConfigureBaseEntity();

        builder.Property(m => m.Type).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(2000);

        // The dispatcher polls for unprocessed rows over and over, so the index deliberately
        // covers only those rows and stays small no matter how large the table grows (ERD §3).
        builder.HasIndex(m => m.ProcessedAt)
            .HasFilter("[ProcessedAt] IS NULL");

        builder.HasIndex(m => m.TenantId);

        // No foreign key on TenantId: this column exists for traceability, and a stuck
        // tenant must never be able to block delivery of its own outbox row.
    }
}
