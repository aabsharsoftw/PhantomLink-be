namespace PhantomPulse.Crm.Dtos.Responses;

public sealed record ContactResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Company,
    string Source,
    IReadOnlyList<string> Tags,
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
