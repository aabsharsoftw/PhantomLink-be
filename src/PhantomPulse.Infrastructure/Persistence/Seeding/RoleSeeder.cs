using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>Seeds the Platform Admin system role with its full permission set.</summary>
internal sealed class RoleSeeder(
    DbContext db,
    IRolePermissionProvider permissionProvider,
    ILogger<RoleSeeder> logger) : IDataSeeder
{
    // Fixed ID — never change once deployed
    internal static readonly Guid PlatformAdminRoleId = new("aaaaaaaa-0001-0000-0000-000000000000");

    public int Order => 20;
    public bool IsDemoOnly => false;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var exists = await db.Set<Role>().IgnoreQueryFilters()
            .AnyAsync(r => r.Id == PlatformAdminRoleId, ct);

        if (exists) return;

        db.Set<Role>().Add(new Role
        {
            Id             = PlatformAdminRoleId,
            TenantId       = Guid.Empty,
            Name           = "Platform Admin",
            Description    = "Full platform access — bypasses all tenant filters",
            IsSystem       = true,
            Scope          = RoleScope.Platform,
            SystemRoleType = SystemRoleType.PlatformAdmin,
        });

        var keys = permissionProvider.GetPermissionKeys(SystemRoleType.PlatformAdmin);
        var permIds = await db.Set<Permission>()
            .Where(p => keys.Contains(p.Key))
            .Select(p => p.Id)
            .ToListAsync(ct);

        db.Set<RolePermission>().AddRange(
            permIds.Select(pid => new RolePermission { RoleId = PlatformAdminRoleId, PermissionId = pid }));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded Platform Admin role ({Count} permissions)", permIds.Count);
    }
}
