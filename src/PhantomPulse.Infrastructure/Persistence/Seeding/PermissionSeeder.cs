using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Upserts the full permission catalog from <c>roles_and_permissions.json</c> into the database.
/// Must run before any role seeder (Order 10) so role seeders can resolve permission IDs by key.
/// Permissions already present (by key) are skipped — safe to re-run on every startup.
/// </summary>
internal sealed class PermissionSeeder(
    DbContext db,
    IRolePermissionProvider permissionProvider,
    ILogger<PermissionSeeder> logger) : IDataSeeder
{
    public int Order => 10;
    public bool IsDemoOnly => false;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var allKeys = permissionProvider.GetAllPermissionKeys();

        var existingKeys = await db.Set<Permission>()
            .Select(p => p.Key)
            .ToHashSetAsync(ct);

        var missing = allKeys.Where(k => !existingKeys.Contains(k)).ToList();
        if (missing.Count == 0)
        {
            logger.LogInformation("Permission catalog up to date — {Count} permissions", allKeys.Count);
            return;
        }

        foreach (var key in missing)
        {
            var parts = key.Split('.', 2);
            db.Set<Permission>().Add(new Permission
            {
                Id          = DerivePermissionId(key),
                Key         = key,
                Module      = parts[0],
                Action      = parts.Length > 1 ? parts[1] : key,
                Description = "",
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Added} new permissions (catalog total: {Total})",
            missing.Count, allKeys.Count);
    }

    /// <summary>
    /// Deterministic UUID v3 (MD5-based) for a permission key.
    /// Produces the same GUID for the same key on every deployment.
    /// </summary>
    private static Guid DerivePermissionId(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("pp:permission:" + key));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(hash);
    }
}
