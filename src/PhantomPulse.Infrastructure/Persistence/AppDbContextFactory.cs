using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Infrastructure.Persistence;

// Used by EF Core tools (Add-Migration, Update-Database) at design time.
// Provides a fully configured DbContext without needing the full DI stack.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=phantompulse;Username=postgres;Password=sql@123")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new DesignTimeTenantContext());
    }

    // Platform scope bypasses all query filters — safe for migration scaffolding.
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public UserScope Scope  => UserScope.Platform;
        public Guid?    TenantId => null;
    }
}
