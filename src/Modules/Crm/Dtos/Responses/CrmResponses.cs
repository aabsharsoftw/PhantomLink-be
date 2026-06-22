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

// ── Tag response ──────────────────────────────────────────────────────────────

public sealed record ContactNoteResponse(
    Guid     Id,
    string   Body,
    DateTime CreatedAt);

public sealed record ImportRowError(int Row, string Column, string Message);

public sealed record ImportLeadsResult(
    Guid                          BatchId,
    int                           Total,
    int                           Imported,
    int                           Skipped,
    int                           Failed,
    IReadOnlyList<ImportRowError> Errors);

public sealed record ImportBatchResponse(
    Guid     Id,
    string   FileName,
    string   Channel,
    int      Total,
    int      Imported,
    int      Skipped,
    int      Failed,
    string   Status,
    DateTime ImportedAt);

public sealed record TagResponse(
    Guid     Id,
    string   Name,
    string   Color,
    string   Description,
    bool     IsSystem,
    int      ContactCount,
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
