using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace A360.Security;

/// <summary>
/// Settings for <see cref="A360AccessGuardMiddleware"/>: which permission-matrix module this service belongs to and
/// which API paths are open without signing in.
/// </summary>
public sealed class A360AccessOptions
{
    /// <summary>Module in Administration › Role &amp; Access Control (e.g. "Assets"); null = sign-in only, no role check.</summary>
    public string? Module { get; set; }

    /// <summary>API path prefixes that need no token (e.g. "/api/auth/login").</summary>
    public string[] PublicPaths { get; set; } = [];
}

public static class A360AccessGuardExtensions
{
    public const string AdministrationClientName = "A360AccessAdministration";

    /// <summary>
    /// Registers the access guard. Each service calls this with its module so that, when the frontend calls the service
    /// directly on its own port (not through the API gateway), the same rules apply: every /api request needs a valid
    /// token, and creates / edits / deletes need the matching permission for <paramref name="module"/>.
    /// </summary>
    public static IServiceCollection AddA360AccessGuard(
        this IServiceCollection services,
        IConfiguration configuration,
        string? module,
        params string[] publicPaths)
    {
        services.AddSingleton(new A360AccessOptions { Module = module, PublicPaths = publicPaths });
        services.AddMemoryCache();
        services.AddHttpClient(AdministrationClientName, client =>
        {
            client.BaseAddress = new Uri(configuration["Services:AdministrationUrl"] ?? "http://localhost:5460/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    /// <summary>Adds the guard to the pipeline; call it right after UseAuthentication().</summary>
    public static IApplicationBuilder UseA360AccessGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<A360AccessGuardMiddleware>();
    }
}

/// <summary>
/// Sign-in and role-based access for a service's own API. Requests without a valid bearer token get 401; a POST
/// (create), PUT / PATCH (edit) or DELETE (delete) is let through only when the caller's roles allow that action on the
/// service's module. The caller's access comes from the Administration service (GET /api/users/me with the caller's
/// token) and is cached for a minute per token. Same rules as the API gateway's check, for calls that skip the gateway.
/// </summary>
public sealed class A360AccessGuardMiddleware(
    RequestDelegate next,
    A360AccessOptions options,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<A360AccessGuardMiddleware> logger)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>POSTs ending in one of these only read data (searches, previews, exports, checks): they need View.</summary>
    private static readonly string[] ReadOnlyPostSuffixes = ["search", "query", "filter", "preview", "export", "validate", "count", "lookup"];

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (HttpMethods.IsOptions(request.Method)
            || !request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || options.PublicPaths.Any(path => request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await DenyAsync(context, StatusCodes.Status401Unauthorized, "Sign in to continue.");
            return;
        }

        var action = ActionOf(request);
        if (options.Module is null || action is null)
        {
            await next(context);
            return;
        }

        var access = await GetAccessAsync(request.Headers.Authorization.ToString(), context.RequestAborted);
        if (access is null)
        {
            await DenyAsync(context, StatusCodes.Status503ServiceUnavailable, "Your permissions could not be checked. Try again in a moment.");
            return;
        }

        if (!access.Allows(options.Module, action))
        {
            logger.LogInformation("Denied {Action} on {Module} for {User}: {Method} {Path}", action, options.Module, access.Email, request.Method, request.Path);
            await DenyAsync(context, StatusCodes.Status403Forbidden, $"Your role doesn't allow you to {action} in {options.Module}. Ask an administrator for access.");
            return;
        }

        await next(context);
    }

    /// <summary>create / edit / delete for a write, view for a read-only POST, null for reads.</summary>
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
        var key = "a360-access:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
        if (cache.TryGetValue(key, out CallerAccess? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient(A360AccessGuardExtensions.AdministrationClientName);
            using var message = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
            message.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await client.SendAsync(message, cancellationToken);
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
