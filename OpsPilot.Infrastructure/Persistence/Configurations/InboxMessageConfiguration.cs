using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(m => m.MessageId);

        builder.Property(m => m.MessageId).HasMaxLength(200);
        builder.Property(m => m.Consumer).IsRequired().HasMaxLength(200);
        builder.Property(m => m.ProcessedAt);

        // The consumer checks "have I already handled this?" for its own name only.
        builder.HasIndex(m => new { m.Consumer, m.MessageId });
    }
}
