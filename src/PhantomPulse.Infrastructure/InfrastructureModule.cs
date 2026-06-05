using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Foundation.Authorization;
using PhantomPulse.Infrastructure.Persistence;
using PhantomPulse.Infrastructure.Persistence.Seeding;
using PhantomPulse.Infrastructure.Realtime;

namespace PhantomPulse.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn  = config.GetConnectionString("Default")!;
        var redis = config.GetConnectionString("Redis")!;

        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(conn).UseSnakeCaseNamingConvention());
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddStackExchangeRedisCache(opt => opt.Configuration = redis);
        services.AddSignalR().AddStackExchangeRedis(redis);
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(conn));
        services.AddHangfireServer(opt => opt.Queues = new[] { "campaigns", "automation", "imports", "default" });
        services.AddSingleton<IRolePermissionProvider, JsonRolePermissionProvider>();
        // System seeders (run in all environments)
        services.AddScoped<IDataSeeder, PermissionSeeder>();
        services.AddScoped<IDataSeeder, RoleSeeder>();
        services.AddScoped<IDataSeeder, SystemUserSeeder>();
        // Demo seeders (Development only — gated by IsDemoOnly)
        services.AddScoped<IDataSeeder, DemoDataSeeder>();
        services.AddScoped<IDataSeeder, DemoContactsSeeder>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
