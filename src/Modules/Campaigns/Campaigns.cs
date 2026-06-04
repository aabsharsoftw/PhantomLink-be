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
            var c = new Campaign { TenantId = tenant.TenantId, Name = name, Channel = channel, Content = content, Audience = audience, ScheduledAt = scheduledAt };
            db.Set<Campaign>().Add(c); await db.SaveChangesAsync(ct);
            if (scheduledAt is null) BackgroundJob.Enqueue<CampaignSendJob>(x => x.SendAsync(c.Id));
            else BackgroundJob.Schedule<CampaignSendJob>(x => x.SendAsync(c.Id), scheduledAt.Value - DateTime.UtcNow);
            return c;
        }
    }

    public class CampaignSendJob(DbContext db, IMessagingService messaging)
    {
        public async Task SendAsync(Guid campaignId)
        {
            var c = await db.Set<Campaign>().FindAsync(new object[] { campaignId }) ?? throw new InvalidOperationException();
            c.Status = "Sending"; await db.SaveChangesAsync();
            // TODO: resolve audience to contact list and send per contact
            c.Status = "Sent"; c.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        }
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
