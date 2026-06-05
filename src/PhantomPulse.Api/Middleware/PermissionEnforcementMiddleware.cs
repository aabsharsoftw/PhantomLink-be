using PhantomPulse.Foundation.Services;
using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Api.Middleware;

public sealed class PermissionEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, RbacService rbac)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null) { await next(context); return; }

        if (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        var required = endpoint.Metadata.GetOrderedMetadata<RequirePermissionAttribute>();
        if (required.Count == 0) { await next(context); return; }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                "Unauthorized",
                new ApiError("unauthorized", "Authentication required")));
            return;
        }

        // Platform scope and SuperAdmin role have unrestricted access
        if (currentUser.Scope == UserScope.Platform ||
            string.Equals(currentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (currentUser.TenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                "Unauthorized",
                new ApiError("tenant_required", "Tenant context is missing")));
            return;
        }

        var ct = context.RequestAborted;
        var userPerms = await rbac.GetPermissionsAsync(currentUser.UserId, currentUser.TenantId.Value, ct);

        foreach (var attr in required)
        {
            if (!userPerms.Contains(attr.Permission))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                    "Forbidden",
                    new ApiError("permission_denied", $"Missing permission: {attr.Permission}")));
                return;
            }
        }

        await next(context);
    }
}
