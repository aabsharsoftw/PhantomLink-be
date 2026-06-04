using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Messaging.Services;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddScoped<MessagingService>();
        services.AddScoped<IMessagingService, MessagingService>();
        return services;
    }
}
