using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Messaging.Entities;

public class Conversation : BaseEntity
{
    public string   WaPhoneNumber  { get; set; } = "";
    public string   Status         { get; set; } = "Open";
    public Guid?    AssignedUserId { get; set; }
    public DateTime LastMessageAt  { get; set; } = DateTime.UtcNow;
    public ICollection<Message> Messages { get; set; } = [];
}

public class Message : BaseEntity
{
    public Guid   ConversationId { get; set; }
    public string Body           { get; set; } = "";
    public string Channel        { get; set; } = "whatsapp";
    public string Direction      { get; set; } = "inbound";
    public string Status         { get; set; } = "sent";
    public bool   IsInternal     { get; set; } = false;
    public Conversation Conversation { get; set; } = null!;
}
