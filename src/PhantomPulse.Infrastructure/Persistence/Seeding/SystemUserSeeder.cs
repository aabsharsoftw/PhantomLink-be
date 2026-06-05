using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhantomPulse.Foundation.Entities;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>Seeds the Platform Admin system user. Depends on <see cref="RoleSeeder"/> (Order 20).</summary>
internal sealed class SystemUserSeeder(DbContext db, ILogger<SystemUserSeeder> logger) : IDataSeeder
{
    // Fixed IDs — never change once deployed
    private static readonly Guid PlatformAdminUserId = new("aaaaaaaa-0002-0000-0000-000000000000");

    public int Order => 30;
    public bool IsDemoOnly => false;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var exists = await db.Set<User>().IgnoreQueryFilters()
            .AnyAsync(u => u.Id == PlatformAdminUserId, ct);

        if (exists) return;

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
            RoleId       = RoleSeeder.PlatformAdminRoleId,
            IsActive     = true,
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded Platform Admin user — admin@phantompulse.io / Admin@123!");
    }
}
