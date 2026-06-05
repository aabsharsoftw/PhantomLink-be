using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PhantomPulse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Orchestrates all registered <see cref="IDataSeeder"/> implementations in order.
/// Demo seeders (<see cref="IDataSeeder.IsDemoOnly"/> = true) are skipped outside Development.
/// </summary>
public sealed class DatabaseSeeder(
    IEnumerable<IDataSeeder> seeders,
    IHostEnvironment environment,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var isDev = environment.IsDevelopment();

        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            if (seeder.IsDemoOnly && !isDev)
            {
                logger.LogDebug("Skipping demo seeder {Seeder} — not in Development", seeder.GetType().Name);
                continue;
            }

            logger.LogInformation("Running seeder: {Seeder}", seeder.GetType().Name);
            await seeder.SeedAsync(ct);
        }
    }

    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(ct);
    }
}
