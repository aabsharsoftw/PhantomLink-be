using PhantomPulse.SharedKernel.Contracts;
using PhantomPulse.SharedKernel.Domain;

namespace PhantomPulse.Api.Middleware;

public sealed class PermissionEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
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

        if (string.Equals(currentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (currentUser.TenantId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                "Unauthorized",
                new ApiError("tenant_required", "Tenant context is missing")));
            return;
        }

        foreach (var attr in required)
        {
            if (!currentUser.HasPermission(attr.Permission))
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
