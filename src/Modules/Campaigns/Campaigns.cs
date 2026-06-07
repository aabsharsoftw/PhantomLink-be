using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhantomPulse.Campaigns.Entities;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Campaigns.Services
{
    public class CampaignService(DbContext db, ITenantContext tenant)
    {
        public Task<List<Campaign>> GetAllAsync(CancellationToken ct = default) => db.Set<Campaign>().ToListAsync(ct);

        public async Task<Campaign> CreateAsync(string name, string channel, string content, string audience, DateTime? scheduledAt, CancellationToken ct = default)
        {
            var c = new Campaign { TenantId = tenant.TenantId!.Value, Name = name, Channel = channel, Content = content, Audience = audience, ScheduledAt = scheduledAt };
            db.Set<Campaign>().Add(c); await db.SaveChangesAsync(ct);
            if (scheduledAt is null) BackgroundJob.Enqueue<CampaignSendJob>(x => x.SendAsync(c.Id));
            else BackgroundJob.Schedule<CampaignSendJob>(x => x.SendAsync(c.Id), scheduledAt.Value - DateTime.UtcNow);
            return c;
        }
    }

    public class CampaignSendJob
    {
        public Task SendAsync(Guid campaignId) =>
            throw new NotImplementedException("Campaign sending not yet implemented.");
    }
}

namespace PhantomPulse.Campaigns
{
    public static class CampaignsModule
    {
        public static IServiceCollection AddCampaignsModule(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<PhantomPulse.Campaigns.Services.CampaignService>();
            services.AddScoped<PhantomPulse.Campaigns.Services.CampaignSendJob>();
            return services;
        }
    }
}
