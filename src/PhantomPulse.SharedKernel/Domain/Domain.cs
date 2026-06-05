using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace PhantomPulse.SharedKernel.Domain;

// ─── Enums ───────────────────────────────────────────────────────────────────

/// <summary>
/// Determines the tenant isolation level for a user or role.
/// Platform  → PhantomPulse super-admins; no tenant constraint, bypass all filters.
/// Agency    → Agency owners / admins; TenantId = AgencyId.
/// SubAccount→ Client staff; TenantId = SubAccountId.
/// </summary>
public enum UserScope
{
    Platform,
    Agency,
    SubAccount,
}

// ─── Base ─────────────────────────────────────────────────────────────────────

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// For Agency-scoped rows    → AgencyId.
    /// For SubAccount-scoped rows → SubAccountId.
    /// Platform entities set this to Guid.Empty (they bypass the query filter entirely).
    /// </summary>
    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public Guid? DeletedBy { get; set; }
}

// ─── Tenant Context ───────────────────────────────────────────────────────────

public interface ITenantContext
{
    /// <summary>Current user's scope. Platform users bypass tenant filters.</summary>
    UserScope Scope { get; }

    /// <summary>
    /// Null for Platform users.
    /// AgencyId for Agency-scoped users.
    /// SubAccountId for SubAccount-scoped users.
    /// </summary>
    Guid? TenantId { get; }
}

public class TenantContext : ITenantContext
{
    public UserScope Scope { get; private set; } = UserScope.SubAccount;
    public Guid? TenantId { get; private set; }

    public void Set(UserScope scope, Guid? tenantId)
    {
        Scope = scope;
        TenantId = tenantId;
    }
}

// ─── Tenant Middleware ────────────────────────────────────────────────────────

/// <summary>
/// Reads 'scope' and 'tenant_id' JWT claims and populates TenantContext
/// before the request reaches any controller or service.
/// </summary>
public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, TenantContext tenant)
    {
        var scopeClaim = ctx.User.FindFirst("scope")?.Value;
        var tenantClaim = ctx.User.FindFirst("tenant_id")?.Value;

        var scope = scopeClaim switch
        {
            "platform" => UserScope.Platform,
            "agency" => UserScope.Agency,
            "sub_account" => UserScope.SubAccount,
            _ => UserScope.SubAccount, // safe default
        };

        Guid? tenantId = scope == UserScope.Platform
            ? null
            : Guid.TryParse(tenantClaim, out var id) ? id : null;

        tenant.Set(scope, tenantId);

        await next(ctx);
    }
}

// ─── Current User ─────────────────────────────────────────────────────────────

public interface ICurrentUser
{
    Guid UserId { get; }

    /// <summary>Null for Platform users.</summary>
    Guid? TenantId { get; }

    Guid? AgencyId { get; }
    Guid? SubAccountId { get; }

    UserScope Scope { get; }
    string Email { get; }
    string Role { get; }
}

public class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    private ClaimsPrincipal? Principal => http.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id)
            ? id
            : Guid.Empty;

    public Guid? TenantId =>
        Scope == UserScope.Platform
            ? null
            : Guid.TryParse(Principal?.FindFirst("tenant_id")?.Value, out var id)
                ? id
                : null;

    public Guid? AgencyId =>
        Guid.TryParse(Principal?.FindFirst("agency_id")?.Value, out var id) ? id : null;

    public Guid? SubAccountId =>
        Guid.TryParse(Principal?.FindFirst("sub_account_id")?.Value, out var id) ? id : null;

    public UserScope Scope => Principal?.FindFirst("scope")?.Value switch
    {
        "platform" => UserScope.Platform,
        "agency"   => UserScope.Agency,
        _          => UserScope.SubAccount,
    };

    public string Email => Principal?.FindFirst("email")?.Value ?? "";
    public string Role  => Principal?.FindFirst("role")?.Value  ?? "";
}