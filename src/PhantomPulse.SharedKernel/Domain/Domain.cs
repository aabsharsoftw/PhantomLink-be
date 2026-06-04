using Microsoft.AspNetCore.Http;

namespace PhantomPulse.SharedKernel.Domain;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public Guid? DeletedBy { get; set; }

}

public interface ITenantContext { Guid TenantId { get; } }

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public void Set(Guid id) => TenantId = id;
}

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, TenantContext tenant)
    {
        var claim = ctx.User.FindFirst("tenant_id")?.Value;
        if (claim is not null && Guid.TryParse(claim, out var id)) tenant.Set(id);
        await next(ctx);
    }
}

public interface ICurrentUser
{
    Guid   UserId   { get; }
    Guid   TenantId { get; }
    string Email    { get; }
    string Role     { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);
}

public class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public Guid   UserId   => Guid.Parse(http.HttpContext?.User.FindFirst("sub")?.Value       ?? Guid.Empty.ToString());
    public Guid   TenantId => Guid.Parse(http.HttpContext?.User.FindFirst("tenant_id")?.Value ?? Guid.Empty.ToString());
    public string Email    => http.HttpContext?.User.FindFirst("email")?.Value ?? "";
    public string Role     => http.HttpContext?.User.FindFirst("role")?.Value  ?? "";

    public IReadOnlyCollection<string> Permissions =>
        (IReadOnlyCollection<string>?)http.HttpContext?.User
            .FindAll("permissions")
            .Select(c => c.Value)
            .ToHashSet()
        ?? Array.Empty<string>();

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
