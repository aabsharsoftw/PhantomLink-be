using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(p => p.Id);
        b.Property(p => p.Key).HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.Key).IsUnique();
        b.Property(p => p.Description).HasMaxLength(500);
        b.Property(p => p.Module).HasMaxLength(50).IsRequired();
        b.Property(p => p.Action).HasMaxLength(50).IsRequired();
    }
}
