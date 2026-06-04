using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.Description).HasMaxLength(500);
        b.Property(r => r.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(r => r.SystemRoleType).HasConversion<string>().HasMaxLength(30);

        // Role names unique within their isolation scope (TenantId = AgencyId or SubAccountId)
        b.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
    }
}
