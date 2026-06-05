using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Foundation.Entities;

// ─── Enums ───────────────────────────────────────────────────────────────────

// UserScope lives in PhantomPulse.SharedKernel.Domain — do not redefine here.

public enum RoleScope
{
    Platform,
    Agency,
    SubAccount,
}

public enum SystemRoleType
{
    PlatformAdmin, // PhantomPulse super-admin — bypasses all tenant filters
    AgencyOwner,   // Full access across the agency and all its sub-accounts
    AgencyAdmin,   // Agency-level admin; no billing / white-label settings
    AccountAdmin,  // Full access within a single sub-account
    Manager,       // Elevated access within a sub-account; no settings
    User,          // Standard day-to-day access within a sub-account
}

// ─── Hierarchy ───────────────────────────────────────────────────────────────

/// <summary>
/// Top-level entity. Represents a reseller / white-label customer.
/// One Agency owns N SubAccounts.
/// Does not extend BaseEntity — it IS the top-level tenant boundary.
/// </summary>
public class Agency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? CustomDomain { get; set; } // white-label domain for login routing
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SubAccount> SubAccounts { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}

/// <summary>
/// A client account owned by an Agency.
/// All CRM data (Contacts, Deals, Conversations…) is isolated per SubAccount via TenantId.
/// Does not extend BaseEntity — it is itself a tenant boundary.
/// </summary>
public class SubAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Agency Agency { get; set; } = null!;
    public ICollection<User> Users { get; set; } = [];
}

// ─── Identity ────────────────────────────────────────────────────────────────

/// <summary>
/// A user in the system.
///
/// Scope = Platform   → AgencyId = null, SubAccountId = null, TenantId = Guid.Empty (filter bypassed)
/// Scope = Agency     → AgencyId = set,  SubAccountId = null, TenantId = AgencyId
/// Scope = SubAccount → AgencyId = set,  SubAccountId = set,  TenantId = SubAccountId
/// </summary>
public class User : BaseEntity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; } = true;

    // Hierarchy placement
    public UserScope Scope { get; set; } = UserScope.SubAccount;
    public Guid? AgencyId { get; set; } // null for Platform users
    public Guid? SubAccountId { get; set; } // null for Agency and Platform users

    // Navigation
    public Agency? Agency { get; set; }
    public SubAccount? SubAccount { get; set; }
    public Role? Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// A named set of permissions.
/// TenantId = Guid.Empty for Platform-scoped roles (filter bypassed).
/// TenantId = AgencyId   for Agency-scoped roles.
/// TenantId = SubAccountId for SubAccount-scoped roles.
/// </summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystem { get; set; } = false; // system roles cannot be deleted
    public RoleScope Scope { get; set; } = RoleScope.SubAccount;
    public SystemRoleType? SystemRoleType { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// ─── RBAC ────────────────────────────────────────────────────────────────────

/// <summary>
/// Global permission definitions — not tenant-scoped.
/// Key format: "Module.Action" e.g. "Contacts.Read", "Billing.Manage"
/// </summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = ""; // e.g. "Contacts.Read"
    public string Description { get; set; } = "";
    public string Module { get; set; } = ""; // e.g. "Contacts"
    public string Action { get; set; } = ""; // e.g. "Read"

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

// ─── Auth ────────────────────────────────────────────────────────────────────

/// <summary>
/// Refresh token tied to a user.
/// Does NOT extend BaseEntity — tokens have no tenant boundary and
/// need no soft-delete or audit trail. Looked up by token string only.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }

    public User User { get; set; } = null!;
}