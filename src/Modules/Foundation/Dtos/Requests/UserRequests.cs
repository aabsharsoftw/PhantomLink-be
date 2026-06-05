using PhantomPulse.Foundation.Entities;
using PhantomPulse.SharedKernel.Domain;
using System.ComponentModel.DataAnnotations;

namespace PhantomPulse.Foundation.Dtos.Requests;

public sealed record UserListQuery(
    int          Page          = 1,
    int          PageSize      = 25,
    string?      Search        = null,
    UserScope?   Scope         = null,
    Guid?        SubAccountId  = null);

public sealed record CreateUserRequest(
    [Required, StringLength(100)] string  FirstName,
    [Required, StringLength(100)] string  LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [StringLength(30)] string?            Phone,
    [Required, MinLength(8)] string       Password,
    [Required] UserScope                  Scope,
    [Required] Guid                       RoleId,
    Guid?                                 SubAccountId,  // required when Scope = SubAccount for agency/platform callers
    Guid?                                 AgencyId);     // for platform callers only

public sealed record UpdateUserRequest(
    [StringLength(100)] string?  FirstName,
    [StringLength(100)] string?  LastName,
    [StringLength(30)]  string?  Phone,
    Guid?                        RoleId,
    bool?                        IsActive);

// ─── Role management ─────────────────────────────────────────────────────────

public sealed record CreateRoleRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string?          Description,
    [Required] RoleScope                 Scope,
    Guid?                                SubAccountId,
    IReadOnlyList<string>?               PermissionKeys);

public sealed record UpdateRoleRequest(
    [StringLength(100)] string? Name,
    [StringLength(500)] string? Description);

public sealed record SetRolePermissionsRequest(
    [Required] IReadOnlyList<string> PermissionKeys);
