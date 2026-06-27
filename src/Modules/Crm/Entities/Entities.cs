using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Entities;

public class Contact : BaseEntity
{
    public string FirstName        { get; set; } = "";
    public string LastName         { get; set; } = "";
    public string Company          { get; set; } = "";
    public string Title            { get; set; } = "";
    public string Source           { get; set; } = "manual";
    public string[] Tags           { get; set; } = [];
    public int    Score            { get; set; } = 50;
    public string Status           { get; set; } = "open";  // open | won | lost | churned
    public string Notes            { get; set; } = "";
    public Guid?  OwnerId          { get; set; }
    public string OwnerName        { get; set; } = "";
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> CustomFields { get; set; } = new();
    public Guid? ImportBatchId { get; set; }
    public ICollection<Deal>         Deals        { get; set; } = [];
    public ICollection<ContactEmail> Emails       { get; set; } = [];
    public ICollection<ContactPhone> Phones       { get; set; } = [];
    public ICollection<ContactNote>  ContactNotes { get; set; } = [];
}

public class ContactEmail : BaseEntity
{
    public Guid   ContactId { get; set; }
    public string Email     { get; set; } = "";
    public string Label     { get; set; } = "work";    // work | home | other
    public bool   IsPrimary { get; set; }
    public Contact Contact  { get; set; } = null!;
}

public class ContactPhone : BaseEntity
{
    public Guid   ContactId { get; set; }
    public string Phone     { get; set; } = "";
    public string Label     { get; set; } = "mobile";  // mobile | work | home | other
    public bool   IsPrimary { get; set; }
    public Contact Contact  { get; set; } = null!;
}

public class ContactNote : BaseEntity
{
    public Guid   ContactId { get; set; }
    public string Body      { get; set; } = "";
    public Contact Contact  { get; set; } = null!;
}

public class Tag : BaseEntity
{
    public string Name        { get; set; } = "";
    public string Color       { get; set; } = "#6366F1";
    public string Description { get; set; } = "";
    public bool   IsSystem    { get; set; }
}

public class ImportBatch : BaseEntity
{
    public string FileName   { get; set; } = "";
    public string Channel    { get; set; } = "all";
    public int    Total      { get; set; }
    public int    Imported   { get; set; }
    public int    Skipped    { get; set; }
    public int    Failed     { get; set; }
    public string ErrorsJson { get; set; } = "[]";
    public string Status     { get; set; } = "completed"; // completed | reverted
}

public class Deal : BaseEntity
{
    public Guid    ContactId      { get; set; }
    public string  Title          { get; set; } = "";
    public decimal Value          { get; set; }
    public string  Currency       { get; set; } = "INR";
    public string  Stage          { get; set; } = "New Lead";
    public string  Priority       { get; set; } = "Medium";
    public Guid?   AssignedUserId { get; set; }
    public Dictionary<string, object?> CustomFields { get; set; } = new();
    public Contact Contact { get; set; } = null!;
}

public class SmartList : BaseEntity
{
    public string Name        { get; set; } = "";
    public string Color       { get; set; } = "#6366F1";
    public string Description { get; set; } = "";
    public bool   IsSystem    { get; set; }
    public string RulesJson   { get; set; } = """{"operator":"and","conditions":[]}""";
    public int    SortOrder   { get; set; }
}
