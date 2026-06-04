namespace PhantomPulse.Campaigns.Dtos.Responses;

public sealed record CampaignResponse(Guid Id, string Name, string Channel, string Status, string Audience, string Content, DateTime? ScheduledAt, DateTime CreatedAt, DateTime UpdatedAt);
