namespace PhantomPulse.Foundation.Dtos.Responses;

public sealed record UserRoleDto(
    Guid    Id,
    string  Name,
    string  Scope,
    string? SystemRoleType);

public sealed record UserResponse(
    Guid         Id,
    string       FirstName,
    string       LastName,
    string       Email,
    string?      Phone,
    string       Scope,
    bool         IsActive,
    Guid?        AgencyId,
    Guid?        SubAccountId,
    string?      AgencyName,
    string?      SubAccountName,
    UserRoleDto? Role,
    DateTime     CreatedAt,
    DateTime     UpdatedAt);
