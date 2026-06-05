using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the demo agency, sub-account, and owner user.
/// Runs in Development only.
/// </summary>
internal sealed class DemoDataSeeder(
    DbContext db,
    IRolePermissionProvider permissionProvider,
    ILogger<DemoDataSeeder> logger) : IDataSeeder
{
    // Fixed IDs — never change once deployed
    private static readonly Guid DemoAgencyId      = new("bbbbbbbb-0001-0000-0000-000000000000");
    private static readonly Guid DemoSubAccountId  = new("bbbbbbbb-0002-0000-0000-000000000000");
    private static readonly Guid AgencyOwnerRoleId = new("bbbbbbbb-0003-0000-0000-000000000000");
    private static readonly Guid AgencyOwnerUserId = new("bbbbbbbb-0004-0000-0000-000000000000");

    public int Order => 100;
    public bool IsDemoOnly => true;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ── Tier 1: Agency ────────────────────────────────────────────────────
        if (!await db.Set<Agency>().AnyAsync(a => a.Id == DemoAgencyId, ct))
        {
            db.Set<Agency>().Add(new Agency
            {
                Id       = DemoAgencyId,
                Name     = "Demo Agency",
                Slug     = "demo-agency",
                IsActive = true,
            });
            await db.SaveChangesAsync(ct);
        }

        // ── Tier 2: Sub-account + agency owner role ────────────────────────────
        var tier2Pending = false;

        if (!await db.Set<SubAccount>().AnyAsync(sa => sa.Id == DemoSubAccountId, ct))
        {
            db.Set<SubAccount>().Add(new SubAccount
            {
                Id       = DemoSubAccountId,
                AgencyId = DemoAgencyId,
                Name     = "Demo Client",
                Slug     = "demo-client",
                IsActive = true,
            });
            tier2Pending = true;
        }

        if (!await db.Set<Role>().IgnoreQueryFilters().AnyAsync(r => r.Id == AgencyOwnerRoleId, ct))
        {
            db.Set<Role>().Add(new Role
            {
                Id             = AgencyOwnerRoleId,
                TenantId       = DemoAgencyId,
                Name           = "Agency Owner",
                Description    = "Full access to the agency and all sub-accounts",
                IsSystem       = true,
                Scope          = RoleScope.Agency,
                SystemRoleType = SystemRoleType.AgencyOwner,
            });

            var keys = permissionProvider.GetPermissionKeys(SystemRoleType.AgencyOwner);
            var permIds = await db.Set<Permission>()
                .Where(p => keys.Contains(p.Key))
                .Select(p => p.Id)
                .ToListAsync(ct);

            db.Set<RolePermission>().AddRange(
                permIds.Select(pid => new RolePermission { RoleId = AgencyOwnerRoleId, PermissionId = pid }));
            tier2Pending = true;
        }

        if (tier2Pending)
            await db.SaveChangesAsync(ct);

        // Sub-account system roles (AccountAdmin / Manager / User)
        await SubAccountProvisioner.EnsureRolesAsync(db, DemoSubAccountId, permissionProvider, ct);

        // ── Tier 3: Owner user ────────────────────────────────────────────────
        if (!await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Id == AgencyOwnerUserId, ct))
        {
            db.Set<User>().Add(new User
            {
                Id           = AgencyOwnerUserId,
                TenantId     = DemoAgencyId,
                Scope        = UserScope.Agency,
                AgencyId     = DemoAgencyId,
                SubAccountId = null,
                FirstName    = "Demo",
                LastName     = "Owner",
                Email        = "owner@demo.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner@123!"),
                RoleId       = AgencyOwnerRoleId,
                IsActive     = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded Demo Agency Owner — owner@demo.com / Owner@123!");
        }
    }
}
