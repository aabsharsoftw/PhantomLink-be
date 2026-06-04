using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Crm.Entities;

public class Contact : BaseEntity
{
    public string FirstName { get; set; } = "";
    public string LastName  { get; set; } = "";
    public string Email     { get; set; } = "";
    public string Phone     { get; set; } = "";
    public string Company   { get; set; } = "";
    public string Source    { get; set; } = "manual";
    public string[] Tags    { get; set; } = [];
    public Dictionary<string, object?> CustomFields { get; set; } = new();
    public ICollection<Deal> Deals { get; set; } = [];
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
