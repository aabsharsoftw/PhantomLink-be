using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence;

/// <summary>
/// Startup seeder for data that cannot go into EF migrations (tenant-specific bootstrapping).
/// The static permission catalog is seeded via HasData() in PermissionConfiguration.
/// All seeds are idempotent — safe to run on every startup.
/// </summary>
public class DataSeeder(DbContext db, ILogger<DataSeeder> logger)
{
    // ── Fixed IDs — never change once deployed ────────────────────────────────
    private static readonly Guid PlatformAdminRoleId = new("aaaaaaaa-0001-0000-0000-000000000000");
    private static readonly Guid PlatformAdminUserId = new("aaaaaaaa-0002-0000-0000-000000000000");

    private static readonly Guid DemoAgencyId      = new("bbbbbbbb-0001-0000-0000-000000000000");
    private static readonly Guid DemoSubAccountId  = new("bbbbbbbb-0002-0000-0000-000000000000");
    private static readonly Guid AgencyOwnerRoleId = new("bbbbbbbb-0003-0000-0000-000000000000");
    private static readonly Guid AgencyOwnerUserId = new("bbbbbbbb-0004-0000-0000-000000000000");

    // ── PhantomCore team seed IDs ─────────────────────────────────────────────
    private static readonly Guid PhantomCoreAgencyId   = new("cccccccc-0001-0000-0000-000000000000");
    private static readonly Guid PcAgencyOwnerRoleId   = new("cccccccc-0002-0000-0000-000000000000");
    private static readonly Guid PcAgencyAdminRoleId   = new("cccccccc-0003-0000-0000-000000000000");

    // Sub-accounts
    private static readonly Guid SubPhantomCoreId    = new("cccccccc-0010-0000-0000-000000000000");
    private static readonly Guid SubScentivoId        = new("cccccccc-0011-0000-0000-000000000000");
    private static readonly Guid SubFalhoutId         = new("cccccccc-0012-0000-0000-000000000000");
    private static readonly Guid SubCharmeId          = new("cccccccc-0013-0000-0000-000000000000");
    private static readonly Guid SubSmartGateId       = new("cccccccc-0014-0000-0000-000000000000");
    private static readonly Guid SubRkGlobalId        = new("cccccccc-0015-0000-0000-000000000000");

    // Users
    private static readonly Guid UserDaanyaalId  = new("cccccccc-0020-0000-0000-000000000000");
    private static readonly Guid UserAbsharId    = new("cccccccc-0021-0000-0000-000000000000");
    private static readonly Guid UserReemId      = new("cccccccc-0022-0000-0000-000000000000");
    private static readonly Guid UserAliId       = new("cccccccc-0023-0000-0000-000000000000");
    private static readonly Guid UserReshmaId    = new("cccccccc-0024-0000-0000-000000000000");
    private static readonly Guid UserFalhoutId   = new("cccccccc-0025-0000-0000-000000000000");
    private static readonly Guid UserKhouloudId  = new("cccccccc-0026-0000-0000-000000000000");
    private static readonly Guid UserGhazalId    = new("cccccccc-0027-0000-0000-000000000000");
    private static readonly Guid UserRupeshId    = new("cccccccc-0028-0000-0000-000000000000");
    private static readonly Guid UserSajanId     = new("cccccccc-0029-0000-0000-000000000000");

    // ─────────────────────────────────────────────────────────────────────────

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPlatformAdminAsync(ct);
        await SeedDemoAgencyAsync(ct);

        // ── DEV SEED: Team Roles page demo data ───────────────────────────────
        // Set to false (or comment out the call below) before deploying to production.
        // All entities use fixed GUIDs and are fully idempotent — safe to re-run.
        const bool seedTeamRolesData = true;
        if (seedTeamRolesData)
            await SeedPhantomCoreTeamAsync(ct);
        // ── END DEV SEED ──────────────────────────────────────────────────────
    }

    // ── Platform admin ────────────────────────────────────────────────────────

    private async Task SeedPlatformAdminAsync(CancellationToken ct)
    {
        var roleExists = await db.Set<Role>().IgnoreQueryFilters()
            .AnyAsync(r => r.Id == PlatformAdminRoleId, ct);

        if (!roleExists)
        {
            var role = new Role
            {
                Id             = PlatformAdminRoleId,
                TenantId       = Guid.Empty,
                Name           = "Platform Admin",
                Description    = "Full platform access — bypasses all tenant filters",
                IsSystem       = true,
                Scope          = RoleScope.Platform,
                SystemRoleType = SystemRoleType.PlatformAdmin,
            };
            db.Set<Role>().Add(role);
            db.Set<RolePermission>().AddRange(
                RolePermissionMatrix.GetPermissionIds(SystemRoleType.PlatformAdmin)
                    .Select(pid => new RolePermission { RoleId = PlatformAdminRoleId, PermissionId = pid }));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded Platform Admin role");
        }

        var userExists = await db.Set<User>().IgnoreQueryFilters()
            .AnyAsync(u => u.Id == PlatformAdminUserId, ct);

        if (!userExists)
        {
            db.Set<User>().Add(new User
            {
                Id           = PlatformAdminUserId,
                TenantId     = Guid.Empty,
                Scope        = UserScope.Platform,
                AgencyId     = null,
                SubAccountId = null,
                FirstName    = "Platform",
                LastName     = "Admin",
                Email        = "admin@phantompulse.io",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
                RoleId       = PlatformAdminRoleId,
                IsActive     = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded Platform Admin user — admin@phantompulse.io / Admin@123!");
        }
    }

    // ── Demo agency ───────────────────────────────────────────────────────────

    private async Task SeedDemoAgencyAsync(CancellationToken ct)
    {
        var agencyExists = await db.Set<Agency>()
            .AnyAsync(a => a.Id == DemoAgencyId, ct);

        if (!agencyExists)
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

        var subExists = await db.Set<SubAccount>()
            .AnyAsync(sa => sa.Id == DemoSubAccountId, ct);

        if (!subExists)
        {
            db.Set<SubAccount>().Add(new SubAccount
            {
                Id       = DemoSubAccountId,
                AgencyId = DemoAgencyId,
                Name     = "Demo Client",
                Slug     = "demo-client",
                IsActive = true,
            });
            await db.SaveChangesAsync(ct);
        }

        var ownerRoleExists = await db.Set<Role>().IgnoreQueryFilters()
            .AnyAsync(r => r.Id == AgencyOwnerRoleId, ct);

        if (!ownerRoleExists)
        {
            var role = new Role
            {
                Id             = AgencyOwnerRoleId,
                TenantId       = DemoAgencyId,
                Name           = "Agency Owner",
                Description    = "Full access to the agency and all sub-accounts",
                IsSystem       = true,
                Scope          = RoleScope.Agency,
                SystemRoleType = SystemRoleType.AgencyOwner,
            };
            db.Set<Role>().Add(role);
            db.Set<RolePermission>().AddRange(
                RolePermissionMatrix.GetPermissionIds(SystemRoleType.AgencyOwner)
                    .Select(pid => new RolePermission { RoleId = AgencyOwnerRoleId, PermissionId = pid }));
            await db.SaveChangesAsync(ct);
        }

        var ownerUserExists = await db.Set<User>().IgnoreQueryFilters()
            .AnyAsync(u => u.Id == AgencyOwnerUserId, ct);

        if (!ownerUserExists)
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
            logger.LogInformation("Seeded Agency Owner user — owner@demo.com / Owner@123!");
        }

        await EnsureSubAccountRolesAsync(DemoSubAccountId, ct);
    }

    // ── PhantomCore team (Team Roles page demo data) ──────────────────────────

    private async Task SeedPhantomCoreTeamAsync(CancellationToken ct)
    {
        // Agency
        if (!await db.Set<Agency>().AnyAsync(a => a.Id == PhantomCoreAgencyId, ct))
        {
            db.Set<Agency>().Add(new Agency
            {
                Id       = PhantomCoreAgencyId,
                Name     = "PHANTOM CORE TECHNOLOGIES L.L.C S.O.C",
                Slug     = "phantom-core-technologies",
                IsActive = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded PhantomCore agency");
        }

        // Sub-accounts
        await EnsureSubAccount(SubPhantomCoreId, PhantomCoreAgencyId, "PHANTOM CORE TECHNOLOGIES L.L.C S.O.C", "phantom-core-sa", ct);
        await EnsureSubAccount(SubScentivoId,    PhantomCoreAgencyId, "Scentivo General Trading LLC",          "scentivo",        ct);
        await EnsureSubAccount(SubFalhoutId,     PhantomCoreAgencyId, "Dr. WADAH FALHOUT DENTAL CLINIC",       "dr-falhout",      ct);
        await EnsureSubAccount(SubCharmeId,      PhantomCoreAgencyId, "Charme Day Surgery Center",             "charme",          ct);
        await EnsureSubAccount(SubSmartGateId,   PhantomCoreAgencyId, "Smart Gate Real Estate L.L.C",          "smart-gate",      ct);
        await EnsureSubAccount(SubRkGlobalId,    PhantomCoreAgencyId, "R.K Global Immigration Services",       "rk-global",       ct);

        // Agency-level roles (Owner + Admin)
        await EnsureAgencyRole(PcAgencyOwnerRoleId, PhantomCoreAgencyId, SystemRoleType.AgencyOwner, "Agency Owner", "Full access to the agency and all sub-accounts", ct);
        await EnsureAgencyRole(PcAgencyAdminRoleId, PhantomCoreAgencyId, SystemRoleType.AgencyAdmin, "Agency Admin",  "Agency-level admin; no billing / white-label settings", ct);

        // Sub-account system roles
        foreach (var saId in new[] { SubPhantomCoreId, SubScentivoId, SubFalhoutId, SubCharmeId, SubSmartGateId, SubRkGlobalId })
            await EnsureSubAccountRolesAsync(saId, ct);

        // Users
        var defaultPwd = BCrypt.Net.BCrypt.HashPassword("PhantomCore@123!");

        // Agency-scope users
        await EnsureUser(UserDaanyaalId, PhantomCoreAgencyId, null, UserScope.Agency,
            "Daanyaal", "Zulfiqar", "phantom.admin@phantomcore.io", "+971 52 892 7863", PcAgencyOwnerRoleId, defaultPwd, ct);

        await EnsureUser(UserAbsharId, PhantomCoreAgencyId, null, UserScope.Agency,
            "Abshar", "Khan", "", "+91 72194 85252", PcAgencyAdminRoleId, defaultPwd, ct);

        // Sub-account users — resolve AccountAdmin role for each sub-account
        await EnsureSubAccountUser(UserReemId,    PhantomCoreAgencyId, SubPhantomCoreId, "Reem",    "Zulfiqar",       "reemzulfiqar10@gmail.com",      "+91 98978 22707",  defaultPwd, ct);
        await EnsureSubAccountUser(UserAliId,     PhantomCoreAgencyId, SubScentivoId,    "Ali",     "Shaikh",         "alishaikh2049@gmail.com",        "+971 50 518 2983", defaultPwd, ct);
        await EnsureSubAccountUser(UserReshmaId,  PhantomCoreAgencyId, SubScentivoId,    "Reshma",  "Kadam",          "info@scentivoglobal.com",        "+971 56 602 0318", defaultPwd, ct);
        await EnsureSubAccountUser(UserFalhoutId, PhantomCoreAgencyId, SubFalhoutId,     "Dr",      "Falhout Dental", "drfalhoutcrm@gmail.com",         "+971 54 446 2121", defaultPwd, ct);
        await EnsureSubAccountUser(UserKhouloudId,PhantomCoreAgencyId, SubFalhoutId,     "Khouloud","Sehli",          "Khoulouudsehli@gmail.com",       "+971 56 737 1792", defaultPwd, ct);
        await EnsureSubAccountUser(UserGhazalId,  PhantomCoreAgencyId, SubCharmeId,      "Ghazal",  "Charme",         "charmemedical@gmail.com",        "+971 56 408 0348", defaultPwd, ct);
        await EnsureSubAccountUser(UserRupeshId,  PhantomCoreAgencyId, SubSmartGateId,   "Rupesh",  "Singh",          "rupesh@smartgatellc.com",        "",                 defaultPwd, ct);
        await EnsureSubAccountUser(UserSajanId,   PhantomCoreAgencyId, SubRkGlobalId,    "Sajan",   "Bhatia",         "info@rkglobalimmigration.com",   "+971 56 415 8163", defaultPwd, ct);

        logger.LogInformation("Seeded PhantomCore team ({Count} users)", 10);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task EnsureSubAccount(Guid id, Guid agencyId, string name, string slug, CancellationToken ct)
    {
        if (await db.Set<SubAccount>().AnyAsync(sa => sa.Id == id, ct)) return;
        db.Set<SubAccount>().Add(new SubAccount { Id = id, AgencyId = agencyId, Name = name, Slug = slug, IsActive = true });
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAgencyRole(Guid id, Guid agencyId, SystemRoleType type, string name, string desc, CancellationToken ct)
    {
        if (await db.Set<Role>().IgnoreQueryFilters().AnyAsync(r => r.Id == id, ct)) return;
        var role = new Role
        {
            Id             = id,
            TenantId       = agencyId,
            Name           = name,
            Description    = desc,
            IsSystem       = true,
            Scope          = RoleScope.Agency,
            SystemRoleType = type,
        };
        db.Set<Role>().Add(role);
        db.Set<RolePermission>().AddRange(
            RolePermissionMatrix.GetPermissionIds(type)
                .Select(pid => new RolePermission { RoleId = id, PermissionId = pid }));
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUser(
        Guid id, Guid agencyId, Guid? subAccountId, UserScope scope,
        string firstName, string lastName, string email, string phone,
        Guid roleId, string passwordHash, CancellationToken ct)
    {
        if (await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Id == id, ct)) return;
        var tenantId = scope == UserScope.SubAccount ? subAccountId!.Value : agencyId;
        db.Set<User>().Add(new User
        {
            Id           = id,
            TenantId     = tenantId,
            Scope        = scope,
            AgencyId     = agencyId,
            SubAccountId = subAccountId,
            FirstName    = firstName,
            LastName     = lastName,
            Email        = email,
            Phone        = string.IsNullOrEmpty(phone) ? null : phone,
            PasswordHash = passwordHash,
            RoleId       = roleId,
            IsActive     = true,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureSubAccountUser(
        Guid id, Guid agencyId, Guid subAccountId,
        string firstName, string lastName, string email, string phone,
        string passwordHash, CancellationToken ct)
    {
        if (await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Id == id, ct)) return;

        // Find the AccountAdmin role for this sub-account
        var accountAdminRole = await db.Set<Role>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == subAccountId
                                   && r.SystemRoleType == SystemRoleType.AccountAdmin
                                   && !r.IsDeleted, ct)
            ?? throw new InvalidOperationException(
                $"AccountAdmin role missing for sub-account {subAccountId}. Ensure EnsureSubAccountRolesAsync ran first.");

        await EnsureUser(id, agencyId, subAccountId, UserScope.SubAccount,
            firstName, lastName, email, phone, accountAdminRole.Id, passwordHash, ct);
    }

    // Mirrors SubAccountProvisioner.EnsureRolesAsync (that class is internal to Foundation).
    private async Task EnsureSubAccountRolesAsync(Guid subAccountId, CancellationToken ct)
    {
        var hasRoles = await db.Set<Role>().IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == subAccountId && r.IsSystem && !r.IsDeleted, ct);
        if (hasRoles) return;

        var definitions = new[]
        {
            (SystemRoleType.AccountAdmin, "Account Admin", "Full access within this account"),
            (SystemRoleType.Manager,      "Manager",       "Operational access within this account"),
            (SystemRoleType.User,         "User",          "Standard day-to-day access"),
        };

        foreach (var (type, name, desc) in definitions)
        {
            var role = new Role
            {
                TenantId       = subAccountId,
                Name           = name,
                Description    = desc,
                IsSystem       = true,
                Scope          = RoleScope.SubAccount,
                SystemRoleType = type,
            };
            db.Set<Role>().Add(role);
            db.Set<RolePermission>().AddRange(
                RolePermissionMatrix.GetPermissionIds(type)
                    .Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded system roles for sub-account {SubAccountId}", subAccountId);
    }

    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(ct);
    }
}
