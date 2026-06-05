using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.Property(p => p.Key).HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.Key).IsUnique();
        b.Property(p => p.Description).HasMaxLength(500);
        b.Property(p => p.Module).HasMaxLength(50).IsRequired();
        b.Property(p => p.Action).HasMaxLength(50).IsRequired();

        // Seed all permissions with fixed GUIDs so they exist after `dotnet ef database update`.
        // To add a new permission: add the key to PermissionKeys, add its GUID to PermissionSeedData,
        // then run `dotnet ef migrations add AddXxxPermission`.
        b.HasData(PermissionSeedData.All);
    }
}
