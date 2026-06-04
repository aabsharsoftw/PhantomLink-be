namespace PhantomPulse.Crm.Dtos.Requests;

public sealed record CreateContactRequest(string FirstName, string LastName, string Email, string Phone, string Source = "manual");

public sealed record AddTagRequest(string Tag);

public sealed record CreateDealRequest(Guid ContactId, string Title, decimal Value, string Currency = "INR");

public sealed record MoveStageRequest(string Stage);
