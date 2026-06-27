namespace PhantomPulse.Crm.Dtos.Requests;

// ── Shared sub-inputs ─────────────────────────────────────────────────────────

public sealed record EmailInput(string Email, string Label = "work", bool IsPrimary = false);
public sealed record PhoneInput(string Phone, string Label = "mobile", bool IsPrimary = false);

// ── Contact requests ──────────────────────────────────────────────────────────

public sealed record CreateContactRequest(
    string FirstName,
    string LastName,
    string Source = "manual",
    IReadOnlyList<EmailInput>? Emails = null,
    IReadOnlyList<PhoneInput>? Phones = null);

public sealed record AddTagRequest(string Tag);
public sealed record AddNoteRequest(string Body);

public sealed record AddEmailRequest(string Email, string Label = "work", bool IsPrimary = false);
public sealed record UpdateEmailRequest(string Email, string Label = "work");

public sealed record AddPhoneRequest(string Phone, string Label = "mobile", bool IsPrimary = false);
public sealed record UpdatePhoneRequest(string Phone, string Label = "mobile");

// ── Deal requests ─────────────────────────────────────────────────────────────

public sealed record CreateDealRequest(Guid ContactId, string Title, decimal Value, string Currency = "INR");
public sealed record MoveStageRequest(string Stage);

// ── Lead requests ─────────────────────────────────────────────────────────────

public sealed record CreateLeadRequest(
    string FirstName,
    string LastName,
    string Company   = "",
    string Title     = "",
    string Source    = "manual",
    string Notes     = "",
    Guid?  OwnerId   = null,
    string OwnerName = "",
    IReadOnlyList<EmailInput>? Emails = null,
    IReadOnlyList<PhoneInput>? Phones = null);

public sealed record UpdateScoreRequest(int Delta);
public sealed record UpdateStatusRequest(string Status);

// ── Tag requests ──────────────────────────────────────────────────────────────

public sealed record CreateTagRequest(string Name, string Color = "#6366F1", string Description = "");
public sealed record UpdateTagRequest(string Name, string Color, string Description = "");

// ── Smart List requests ────────────────────────────────────────────────────────

public sealed record CreateSmartListRequest(
    string Name,
    string Color       = "#6366F1",
    string Description = "",
    string RulesJson   = """{"operator":"and","conditions":[]}""");

public sealed record UpdateSmartListRequest(
    string Name,
    string Color,
    string Description,
    string RulesJson);

public sealed record RenameSmartListRequest(string Name);

public sealed record PreviewSmartListRequest(string RulesJson);
