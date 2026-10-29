using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace A360.ApiGateway.Middlewares;

/// <summary>
/// Requires a valid bearer token for every request passing through the gateway, except the public
/// endpoints needed to check health and browse swagger.
/// </summary>
public sealed class GatewayAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public GatewayAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || IsPublicPath(context.Request.Path)
            || context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
    }

    private static bool IsPublicPath(PathString path)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/');

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        // Gateway's own swagger UI, merged document and health/info endpoints.
        if (segments[0] is "swagger" or "openapi" or "gateway")
        {
            return true;
        }

        // Downstream service swagger documents and health checks, e.g. /project/swagger/v1/swagger.json, /project/health.
        return segments.Length >= 2
            && !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(segments[1], "swagger", StringComparison.OrdinalIgnoreCase)
                || (segments.Length == 2 && string.Equals(segments[1], "health", StringComparison.OrdinalIgnoreCase)));
    }
}
