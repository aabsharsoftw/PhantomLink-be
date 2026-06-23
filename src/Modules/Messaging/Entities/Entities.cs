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

public class MessageTemplate : BaseEntity
{
    public string   Name      { get; set; } = "";
    public string   Channel   { get; set; } = "WhatsApp"; // WhatsApp | Email | SMS
    public string   Category  { get; set; } = "Marketing"; // Marketing | Utility | Authentication
    public string   Status    { get; set; } = "Pending";   // Approved | Pending | Rejected
    public string   Body      { get; set; } = "";
    public string[] Variables { get; set; } = [];
    public int      Usage     { get; set; } = 0;
}

public class EmailTemplate : BaseEntity
{
    public string   Name       { get; set; } = "";
    public string   Subject    { get; set; } = "";
    public string   HtmlBody   { get; set; } = "";
    public string   TextBody   { get; set; } = "";
    public string   Category   { get; set; } = "Marketing"; // Marketing | Utility | Authentication
    public string   Status     { get; set; } = "Pending";   // Approved | Pending | Rejected
    public string[] Variables  { get; set; } = [];
    public int      Usage      { get; set; } = 0;
}
