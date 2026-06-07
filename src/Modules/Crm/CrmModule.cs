using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Crm.Services;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Crm;

public static class CrmModule
{
    public static IServiceCollection AddCrmModule(this IServiceCollection services)
    {
        services.AddScoped<ContactService>();
        services.AddScoped<PipelineService>();
        services.AddScoped<LeadService>();
        services.AddScoped<IContactService, ContactService>();
        return services;
    }
}
