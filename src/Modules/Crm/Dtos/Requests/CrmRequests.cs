namespace PhantomPulse.Crm.Dtos.Requests;

public sealed record CreateContactRequest(string FirstName, string LastName, string Email, string Phone, string Source = "manual");

public sealed record AddTagRequest(string Tag);

public sealed record CreateDealRequest(Guid ContactId, string Title, decimal Value, string Currency = "INR");

public sealed record MoveStageRequest(string Stage);

// ── Lead-specific requests ────────────────────────────────────────────────────

public sealed record CreateLeadRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Company  = "",
    string Title    = "",
    string Source   = "manual",
    string Notes    = "",
    Guid?  OwnerId  = null,
    string OwnerName = "");

public sealed record UpdateScoreRequest(int Delta);

public sealed record UpdateStatusRequest(string Status);
