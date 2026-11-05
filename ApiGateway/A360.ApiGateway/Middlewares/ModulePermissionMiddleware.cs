using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Yarp.ReverseProxy.Model;

namespace A360.ApiGateway.Middlewares;

/// <summary>
/// Role-based access for writes. Runs in the reverse-proxy pipeline, after the route is matched: a POST (create),
/// PUT / PATCH (edit) or DELETE (delete) to a module's service is let through only when the caller's roles allow that
/// action on the module, as set in Administration › Role &amp; Access Control. The caller's access comes from the
/// Administration service (GET /api/users/me with the caller's own token) and is cached briefly per token.
/// </summary>
public sealed class ModulePermissionMiddleware(
    RequestDelegate next,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<ModulePermissionMiddleware> logger)
{
    public const string AdministrationClientName = "AdministrationAccess";

    /// <summary>How long a user's access is reused before it is read again (role changes apply within this time).</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Gateway cluster -> permission-matrix module. Clusters not listed (sign-in, media, event log, workflows) aren't checked.</summary>
    private static readonly Dictionary<string, string> ClusterModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asset-cluster"] = "Assets",
        ["tracking-cluster"] = "Tracking & IoT",
        ["maintenance-cluster"] = "Maintenance",
        ["inspection-cluster"] = "Inspection",
        ["wip-cluster"] = "WIP / Operations",
        ["dashboard-cluster"] = "Dashboard",
        ["report-analytics-cluster"] = "Reports & Analytics",
        ["administration-cluster"] = "Administration",
        ["master-data-management-cluster"] = "Master Data Management"
    };

    /// <summary>POSTs ending in one of these only read data (searches, previews, exports, checks): they need View.</summary>
    private static readonly string[] ReadOnlyPostSuffixes = ["search", "query", "filter", "preview", "export", "validate", "count", "lookup"];

    public async Task InvokeAsync(HttpContext context)
    {
        var action = ActionOf(context.Request);
        var clusterId = context.GetReverseProxyFeature().Route.Config.ClusterId;
        if (action is null || clusterId is null || !ClusterModules.TryGetValue(clusterId, out var module))
        {
            await next(context);
            return;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        var access = await GetAccessAsync(authorization, context.RequestAborted);
        if (access is null)
        {
            await DenyAsync(context, StatusCodes.Status503ServiceUnavailable, "Your permissions could not be checked. Try again in a moment.");
            return;
        }

        if (!access.Allows(module, action))
        {
            logger.LogInformation("Denied {Action} on {Module} for {User}: {Method} {Path}", action, module, access.Email, context.Request.Method, context.Request.Path);
            await DenyAsync(context, StatusCodes.Status403Forbidden, $"Your role doesn't allow you to {action} in {module}. Ask an administrator for access.");
            return;
        }

        await next(context);
    }

    /// <summary>create / edit / delete for a write, view for a read-only POST, null for reads (not checked here).</summary>
    private static string? ActionOf(HttpRequest request)
    {
        var method = request.Method;
        if (HttpMethods.IsPost(method))
        {
            var last = (request.Path.Value ?? string.Empty).TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
            return ReadOnlyPostSuffixes.Any(suffix => last.Equals(suffix, StringComparison.OrdinalIgnoreCase) || last.EndsWith("-" + suffix, StringComparison.OrdinalIgnoreCase))
                ? "view"
                : "create";
        }

        if (HttpMethods.IsPut(method) || HttpMethods.IsPatch(method))
        {
            return "edit";
        }

        return HttpMethods.IsDelete(method) ? "delete" : null;
    }

    private async Task<CallerAccess?> GetAccessAsync(string authorization, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        var key = "access:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
        if (cache.TryGetValue(key, out CallerAccess? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient(AdministrationClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET /api/users/me returned {Status}; write refused.", (int)response.StatusCode);
                return null;
            }

            var access = await response.Content.ReadFromJsonAsync<CallerAccess>(Json, cancellationToken);
            if (access is not null)
            {
                cache.Set(key, access, CacheFor);
            }

            return access;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or FormatException)
        {
            logger.LogWarning(exception, "Could not read the caller's access; write refused.");
            return null;
        }
    }

    private static Task DenyAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { message });
    }

    private sealed record ModuleAccess(string Module, List<string>? Actions);

    private sealed record CallerAccess(string? Email, bool FullAccess, List<ModuleAccess>? Modules)
    {
        public bool Allows(string module, string action) =>
            FullAccess
            || (Modules ?? []).Any(m => string.Equals(m.Module, module, StringComparison.OrdinalIgnoreCase)
                && (m.Actions ?? []).Contains(action, StringComparer.OrdinalIgnoreCase));
    }
}
