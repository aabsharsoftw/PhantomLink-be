using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Campaigns.Entities;

public class Campaign : BaseEntity
{
    public string    Name        { get; set; } = "";
    public string    Channel     { get; set; } = "whatsapp";
    public string    Status      { get; set; } = "Draft";
    public string    Audience    { get; set; } = "{}";
    public string    Content     { get; set; } = "";
    public DateTime? ScheduledAt { get; set; }
    public Guid      CreatedBy   { get; set; }
}
