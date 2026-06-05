namespace PhantomPulse.Foundation.Dtos.Responses;

public sealed record UserRoleDto(
    Guid Id,
    string Name,
    string Scope,
    string? SystemRoleType);

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Scope,
    bool IsActive,
    Guid? AgencyId,
    Guid? SubAccountId,
    string? AgencyName,
    string? SubAccountName,
    UserRoleDto? Role,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ─── RBAC ────────────────────────────────────────────────────────────────────

public sealed record RoleDto(
    Guid Id,
    string Name,
    string Description,
    string Scope,
    string? SystemRoleType,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    Guid TenantId);

public sealed record PermissionDto(
    Guid Id,
    string Key,
    string Description,
    string Module,
    string Action);