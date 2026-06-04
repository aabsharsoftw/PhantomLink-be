using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Foundation.Services;

namespace PhantomPulse.Foundation;

public static class FoundationModule
{
    public static IServiceCollection AddFoundationModule(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<RbacService>();
        return services;
    }
}
