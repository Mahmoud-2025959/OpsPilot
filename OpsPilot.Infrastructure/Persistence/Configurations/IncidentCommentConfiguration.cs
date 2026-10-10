using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class IncidentCommentConfiguration : IEntityTypeConfiguration<IncidentComment>
{
    public void Configure(EntityTypeBuilder<IncidentComment> builder)
    {
        builder.ToTable("IncidentComments");
        builder.ConfigureBaseEntity();

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.IncidentId).IsRequired();
        builder.Property(c => c.AuthorId).IsRequired();
        builder.Property(c => c.Body).IsRequired().HasMaxLength(4000);

        // Serves the timeline read, which pages a single incident's comments by age.
        builder.HasIndex(c => new { c.TenantId, c.IncidentId, c.CreatedAt });
        builder.HasIndex(c => c.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comments have no meaning apart from their incident (ERD §4).
        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
