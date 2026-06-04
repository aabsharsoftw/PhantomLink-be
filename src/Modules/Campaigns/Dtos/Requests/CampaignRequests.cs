namespace PhantomPulse.Campaigns.Dtos.Requests;

public sealed record CreateCampaignRequest(string Name, string Channel, string Content, string Audience, DateTime? ScheduledAt = null);
