using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the PhantomCore team agency, sub-accounts, roles, and users.
/// Runs in Development only.
/// </summary>
internal sealed class DemoContactsSeeder(
    DbContext db,
    IRolePermissionProvider permissionProvider,
    ILogger<DemoContactsSeeder> logger) : IDataSeeder
{
    // Fixed IDs — never change once deployed
    private static readonly Guid PhantomCoreAgencyId = new("cccccccc-0001-0000-0000-000000000000");
    private static readonly Guid PcAgencyOwnerRoleId = new("cccccccc-0002-0000-0000-000000000000");
    private static readonly Guid PcAgencyAdminRoleId = new("cccccccc-0003-0000-0000-000000000000");

    private static readonly Guid SubPhantomCoreId = new("cccccccc-0010-0000-0000-000000000000");
    private static readonly Guid SubScentivoId = new("cccccccc-0011-0000-0000-000000000000");
    private static readonly Guid SubFalhoutId = new("cccccccc-0012-0000-0000-000000000000");
    private static readonly Guid SubCharmeId = new("cccccccc-0013-0000-0000-000000000000");
    private static readonly Guid SubSmartGateId = new("cccccccc-0014-0000-0000-000000000000");
    private static readonly Guid SubRkGlobalId = new("cccccccc-0015-0000-0000-000000000000");

    private static readonly Guid UserDaanyaalId = new("cccccccc-0020-0000-0000-000000000000");
    private static readonly Guid UserAbsharId = new("cccccccc-0021-0000-0000-000000000000");
    private static readonly Guid UserReemId = new("cccccccc-0022-0000-0000-000000000000");
    private static readonly Guid UserAliId = new("cccccccc-0023-0000-0000-000000000000");
    private static readonly Guid UserReshmaId = new("cccccccc-0024-0000-0000-000000000000");
    private static readonly Guid UserFalhoutId = new("cccccccc-0025-0000-0000-000000000000");
    private static readonly Guid UserKhouloudId = new("cccccccc-0026-0000-0000-000000000000");
    private static readonly Guid UserGhazalId = new("cccccccc-0027-0000-0000-000000000000");
    private static readonly Guid UserRupeshId = new("cccccccc-0028-0000-0000-000000000000");
    private static readonly Guid UserSajanId = new("cccccccc-0029-0000-0000-000000000000");

    private static readonly (Guid Id, string Name, string Slug)[] SubAccounts =
    [
        (SubPhantomCoreId, "PHANTOM CORE TECHNOLOGIES L.L.C S.O.C", "phantom-core-sa"),
        (SubScentivoId,    "Scentivo General Trading LLC",            "scentivo"),
        (SubFalhoutId,     "Dr. WADAH FALHOUT DENTAL CLINIC",         "dr-falhout"),
        (SubCharmeId,      "Charme Day Surgery Center",               "charme"),
        (SubSmartGateId,   "Smart Gate Real Estate L.L.C",             "smart-gate"),
        (SubRkGlobalId,    "R.K Global Immigration Services",          "rk-global"),
    ];

    public int Order => 110;
    public bool IsDemoOnly => true;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ── Tier 1: Agency ────────────────────────────────────────────────────
        if (!await db.Set<Agency>().AnyAsync(a => a.Id == PhantomCoreAgencyId, ct))
        {
            db.Set<Agency>().Add(new Agency
            {
                Id = PhantomCoreAgencyId,
                Name = "PHANTOM CORE TECHNOLOGIES L.L.C S.O.C",
                Slug = "phantom-core-technologies",
                IsActive = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded PhantomCore agency");
        }

        // ── Tier 2: Sub-accounts + agency roles (single batch) ────────────────

        // Batch-resolve all permission IDs needed for agency roles in one query
        var ownerKeys = permissionProvider.GetPermissionKeys(SystemRoleType.AgencyOwner);
        var adminKeys = permissionProvider.GetPermissionKeys(SystemRoleType.AgencyAdmin);
        var allRoleKeys = ownerKeys.Union(adminKeys).ToList();

        var permissionsByKey = await db.Set<Permission>()
            .Where(p => allRoleKeys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id, ct);

        var tier2Pending = false;

        foreach (var (id, name, slug) in SubAccounts)
        {
            if (!await db.Set<SubAccount>().AnyAsync(sa => sa.Id == id, ct))
            {
                db.Set<SubAccount>().Add(new SubAccount
                {
                    Id = id,
                    AgencyId = PhantomCoreAgencyId,
                    Name = name,
                    Slug = slug,
                    IsActive = true,
                });
                tier2Pending = true;
            }
        }

        tier2Pending |= await TrackAgencyRoleIfMissingAsync(
            PcAgencyOwnerRoleId, SystemRoleType.AgencyOwner,
            "Agency Owner", "Full access to the agency and all sub-accounts",
            ownerKeys, permissionsByKey, ct);

        tier2Pending |= await TrackAgencyRoleIfMissingAsync(
            PcAgencyAdminRoleId, SystemRoleType.AgencyAdmin,
            "Agency Admin", "Agency-level admin; no billing / white-label settings",
            adminKeys, permissionsByKey, ct);

        if (tier2Pending)
            await db.SaveChangesAsync(ct);

        // Sub-account system roles (AccountAdmin / Manager / User) per sub-account
        foreach (var (id, _, _) in SubAccounts)
            await SubAccountProvisioner.EnsureRolesAsync(db, id, permissionProvider, ct);

        // ── Tier 3: Users (batch — single SaveChanges) ────────────────────────
        var defaultPwd = BCrypt.Net.BCrypt.HashPassword("PhantomCore@123!");

        var subAccountIds = SubAccounts.Select(sa => sa.Id).ToArray();
        var adminRoleBySubAccount = await db.Set<Role>().IgnoreQueryFilters()
            .Where(r => subAccountIds.Contains(r.TenantId)
                     && r.SystemRoleType == SystemRoleType.AccountAdmin
                     && !r.IsDeleted)
            .ToDictionaryAsync(r => r.TenantId, r => r.Id, ct);

        // Agency-scope users
        await TrackUserIfMissingAsync(UserDaanyaalId, null, UserScope.Agency,
            "Daanyaal", "Zulfiqar", "phantom.admin@phantomcore.io", "+971 52 892 7863",
            PcAgencyOwnerRoleId, defaultPwd, ct);

        await TrackUserIfMissingAsync(UserAbsharId, null, UserScope.Agency,
            "Abshar", "Khan", "", "+91 72194 85252",
            PcAgencyAdminRoleId, defaultPwd, ct);

        // Sub-account users
        await TrackUserIfMissingAsync(UserReemId, SubPhantomCoreId, UserScope.SubAccount, "Reem", "Zulfiqar", "reemzulfiqar10@gmail.com", "+91 98978 22707", adminRoleBySubAccount[SubPhantomCoreId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserAliId, SubScentivoId, UserScope.SubAccount, "Ali", "Shaikh", "alishaikh2049@gmail.com", "+971 50 518 2983", adminRoleBySubAccount[SubScentivoId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserReshmaId, SubScentivoId, UserScope.SubAccount, "Reshma", "Kadam", "info@scentivoglobal.com", "+971 56 602 0318", adminRoleBySubAccount[SubScentivoId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserFalhoutId, SubFalhoutId, UserScope.SubAccount, "Dr", "Falhout Dental", "drfalhoutcrm@gmail.com", "+971 54 446 2121", adminRoleBySubAccount[SubFalhoutId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserKhouloudId, SubFalhoutId, UserScope.SubAccount, "Khouloud", "Sehli", "Khoulouudsehli@gmail.com", "+971 56 737 1792", adminRoleBySubAccount[SubFalhoutId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserGhazalId, SubCharmeId, UserScope.SubAccount, "Ghazal", "Charme", "charmemedical@gmail.com", "+971 56 408 0348", adminRoleBySubAccount[SubCharmeId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserRupeshId, SubSmartGateId, UserScope.SubAccount, "Rupesh", "Singh", "rupesh@smartgatellc.com", "", adminRoleBySubAccount[SubSmartGateId], defaultPwd, ct);
        await TrackUserIfMissingAsync(UserSajanId, SubRkGlobalId, UserScope.SubAccount, "Sajan", "Bhatia", "info@rkglobalimmigration.com", "+971 56 415 8163", adminRoleBySubAccount[SubRkGlobalId], defaultPwd, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> TrackAgencyRoleIfMissingAsync(
        Guid id, SystemRoleType type, string name, string desc,
        IReadOnlyList<string> keys, Dictionary<string, Guid> permissionsByKey,
        CancellationToken ct)
    {
        if (await db.Set<Role>().IgnoreQueryFilters().AnyAsync(r => r.Id == id, ct))
            return false;

        db.Set<Role>().Add(new Role
        {
            Id = id,
            TenantId = PhantomCoreAgencyId,
            Name = name,
            Description = desc,
            IsSystem = true,
            Scope = RoleScope.Agency,
            SystemRoleType = type,
        });
        db.Set<RolePermission>().AddRange(
            keys.Where(k => permissionsByKey.ContainsKey(k))
                .Select(k => new RolePermission { RoleId = id, PermissionId = permissionsByKey[k] }));
        return true;
    }

    private async Task TrackUserIfMissingAsync(
        Guid id, Guid? subAccountId, UserScope scope,
        string firstName, string lastName, string email, string phone,
        Guid roleId, string passwordHash, CancellationToken ct)
    {
        if (await db.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Id == id, ct)) return;

        db.Set<User>().Add(new User
        {
            Id = id,
            TenantId = scope == UserScope.SubAccount ? subAccountId!.Value : PhantomCoreAgencyId,
            Scope = scope,
            AgencyId = PhantomCoreAgencyId,
            SubAccountId = subAccountId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = string.IsNullOrEmpty(phone) ? null : phone,
            PasswordHash = passwordHash,
            RoleId = roleId,
            IsActive = true,
        });
    }
}
