using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Automation.Services;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Automation;

public static class AutomationModule
{
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        services.AddScoped<AutomationService>();
        services.AddScoped<WorkflowExecutionJob>();
        services.AddScoped<IAutomationTrigger, AutomationService>();
        return services;
    }
}
