namespace PhantomPulse.Crm.Dtos.Responses;

public sealed record ContactEmailResponse(Guid Id, string Email, string Label, bool IsPrimary);
public sealed record ContactPhoneResponse(Guid Id, string Phone, string Label, bool IsPrimary);

public sealed record ContactResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Company,
    string Source,
    IReadOnlyList<string>             Tags,
    IReadOnlyList<ContactEmailResponse> Emails,
    IReadOnlyList<ContactPhoneResponse> Phones,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record DealResponse(
    Guid Id,
    Guid ContactId,
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    string Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── Lead response ─────────────────────────────────────────────────────────────

public sealed record LeadResponse(
    Guid     Id,
    string   FirstName,
    string   LastName,
    string   Name,
    string   PrimaryEmail,
    string   PrimaryPhone,
    IReadOnlyList<ContactEmailResponse> Emails,
    IReadOnlyList<ContactPhoneResponse> Phones,
    string   Company,
    string   Title,
    string[] Tags,
    string   Owner,
    string   OwnerInitials,
    string   Source,
    int      Score,
    string   Status,
    string   Notes,
    DateTime CreatedAt,
    DateTime LastActivityAt);
