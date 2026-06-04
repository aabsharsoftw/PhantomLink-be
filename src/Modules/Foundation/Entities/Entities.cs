using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Foundation.Entities;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum UserScope
{
    Agency,     // AgencyOwner / AgencyAdmin — access spans all sub-accounts of the agency
    SubAccount, // AccountAdmin / Manager / User — scoped to a single sub-account
}

public enum RoleScope
{
    Platform, // PhantomPulse super admins
    Agency,
    SubAccount,
}

public enum SystemRoleType
{
    AgencyOwner,   // Full platform access for the owning user
    AgencyAdmin,   // Agency-level admin, may have restricted access
    AccountAdmin,  // Full access within a sub-account
    Manager,       // Elevated access within a sub-account
    User,          // Standard access within a sub-account
}

// ─── Hierarchy ───────────────────────────────────────────────────────────────

/// <summary>
/// Top-level entity. Represents a reseller or white-label customer.
/// One Agency owns N SubAccounts.
/// </summary>
public class Agency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? CustomDomain { get; set; } // white-label domain
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SubAccount> SubAccounts { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}

/// <summary>
/// A location / client account owned by an Agency.
/// Replaces the former "Tenant" concept.
/// All CRM data (Contacts, Deals, Conversations…) is isolated by SubAccount via TenantId.
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
/// - Agency users (Scope = Agency): TenantId = AgencyId, SubAccountId is null.
/// - SubAccount users (Scope = SubAccount): TenantId = SubAccountId, AgencyId refers to parent agency.
/// The BaseEntity.TenantId drives the EF query filter and is always set.
/// </summary>
public class User : BaseEntity
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Guid? RoleId { get; set; }
    public bool IsActive { get; set; } = true;

    // Hierarchy placement
    public UserScope Scope { get; set; } = UserScope.SubAccount;
    public Guid AgencyId { get; set; }
    public Guid? SubAccountId { get; set; }

    // Navigation
    public Agency Agency { get; set; } = null!;
    public SubAccount? SubAccount { get; set; }
    public Role? Role { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// A named set of permissions scoped to either an Agency or a SubAccount.
/// TenantId = AgencyId for Agency-scoped roles; TenantId = SubAccountId for SubAccount-scoped roles.
/// </summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystem { get; set; } = false;
    public RoleScope Scope { get; set; } = RoleScope.SubAccount;
    public SystemRoleType? SystemRoleType { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// ─── RBAC ────────────────────────────────────────────────────────────────────

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
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

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public User User { get; set; } = null!;
}
