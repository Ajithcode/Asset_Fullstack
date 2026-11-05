using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace A360.ApiGateway.Middlewares;

/// <summary>
/// Requires a valid bearer token for every request passing through the gateway, except the public
/// endpoints needed to check health and browse swagger.
/// </summary>
public sealed class GatewayAuthenticationMiddleware
{
    // Sign-up and sign-in happen before the caller has a token.
    private static readonly string[] PublicAuthPaths =
    [
        "/api/auth/roles",
        "/api/auth/register",
        "/api/auth/login",
        "/api/auth/verify-otp",
        "/api/auth/verify-totp",
        "/api/auth/refresh",
        "/api/auth/logout"
    ];

    private readonly RequestDelegate _next;

    public GatewayAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || IsPublicPath(context.Request.Path)
            || IsUploadedFile(context.Request)
            || context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
    }

    // Files saved by the Media service (random file names under /uploads/<category>/) are read by <img> / download
    // links, which can't send a bearer token. Only reading is public; uploading still needs a signed-in user.
    private static bool IsUploadedFile(HttpRequest request)
    {
        return (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicPath(PathString path)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/');

        if (PublicAuthPaths.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

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
