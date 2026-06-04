using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Foundation.Entities;

namespace PhantomPulse.Infrastructure.Persistence;

public class DataSeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Set<Permission>().Select(p => p.Key).ToHashSetAsync(ct);

        var toAdd = PermissionKeys.All
            .Where(p => !existing.Contains(p.Key))
            .Select(p => new Permission
            {
                Key         = p.Key,
                Description = p.Description,
                Module      = p.Module,
                Action      = p.Action,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Set<Permission>().AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(ct);
    }
}
