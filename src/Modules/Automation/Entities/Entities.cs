using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Automation.Entities;

public class Workflow : BaseEntity
{
    public string Name     { get; set; } = "";
    public string Trigger  { get; set; } = "";
    public string Action   { get; set; } = "";
    public string Payload  { get; set; } = "{}";
    public bool   IsActive { get; set; } = true;
}

public class ChatbotSession : BaseEntity
{
    public string   WaPhoneNumber  { get; set; } = "";
    public Guid     WorkflowId     { get; set; }
    public int      CurrentNode    { get; set; } = 0;
    public string   Status         { get; set; } = "active";
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
