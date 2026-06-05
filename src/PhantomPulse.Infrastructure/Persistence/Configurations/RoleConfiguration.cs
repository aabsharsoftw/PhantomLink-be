using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.Description).HasMaxLength(500);
        b.Property(r => r.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(r => r.SystemRoleType).HasConversion<string>().HasMaxLength(30);

        // Custom roles: name must be unique within a tenant.
        b.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();

        // System roles: only one instance of each SystemRoleType per tenant.
        // Prevents a bug from creating e.g. two "AgencyOwner" system roles for the same agency.
        b.HasIndex(r => new { r.TenantId, r.SystemRoleType })
            .IsUnique()
            .HasFilter("is_system = true AND system_role_type IS NOT NULL");
    }
}
