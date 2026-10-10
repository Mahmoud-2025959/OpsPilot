using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Stores AI output as suggestions on an incident. Optional phase 6 (ERD §3).
/// </summary>
public sealed class AiSuggestionConfiguration : IEntityTypeConfiguration<AiSuggestion>
{
    public void Configure(EntityTypeBuilder<AiSuggestion> builder)
    {
        builder.ToTable("AiSuggestions");
        builder.ConfigureBaseEntity();

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.IncidentId).IsRequired();
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(s => s.Content).IsRequired().HasMaxLength(8000);
        builder.Property(s => s.Model).IsRequired().HasMaxLength(100);
        builder.Property(s => s.AcceptedAt);

        builder.HasIndex(s => new { s.TenantId, s.IncidentId, s.Kind });
        builder.HasIndex(s => s.TenantId);

        builder.Ignore(s => s.IsAccepted);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // A suggestion without its incident is meaningless, so it goes when the incident goes.
        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(s => s.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
