using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Crm.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.Property(t => t.Color).HasMaxLength(20).IsRequired();
        b.Property(t => t.Description).HasMaxLength(500);

        // Unique tag name per tenant — soft-deleted rows don't block reuse
        b.HasIndex(t => new { t.TenantId, t.Name })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tags_unique_name_per_tenant");
    }
}
