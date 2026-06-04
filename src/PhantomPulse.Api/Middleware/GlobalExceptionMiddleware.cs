using System.Text.Json;
using PhantomPulse.SharedKernel.Contracts;

namespace PhantomPulse.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await WriteErrorResponse(context, ex);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, Exception ex)
    {
        var (status, code, message) = ex switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "not_found", "Resource not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "invalid_request", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred")
        };

        var payload = ApiResponse<object>.Fail(message, new ApiError(code, ex.Message));

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
