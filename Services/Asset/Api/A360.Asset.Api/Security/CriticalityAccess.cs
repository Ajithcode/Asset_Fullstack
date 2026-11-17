using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using A360.Security;

namespace A360.Asset.Api.Security;

/// <summary>
/// The caller's AssetCriticality permissions (view, assess, approve, approvecritical, configure), from
/// GET /api/users/me resolved with <see cref="AssetCriticalityPermissions.Resolve"/> (same rule as the gateway).
/// </summary>
public interface ICriticalityPermissionService
{
    /// <summary>Null when the Administration service could not be reached.</summary>
    Task<IReadOnlySet<string>?> GetAsync(CancellationToken cancellationToken = default);
}

/// <remarks>Cached for a minute per token, like the access guard, so role changes apply within a minute.</remarks>
public sealed class CriticalityPermissionService(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    ILogger<CriticalityPermissionService> logger) : ICriticalityPermissionService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlySet<string>?> GetAsync(CancellationToken cancellationToken = default)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return new HashSet<string>();
        }

        var key = "asset-criticality-permissions:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
        if (cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient(A360AccessGuardExtensions.AdministrationClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET /api/users/me returned {Status}; criticality permissions unknown.", (int)response.StatusCode);
                return null;
            }

            var me = await response.Content.ReadFromJsonAsync<MyAccess>(Json, cancellationToken);
            if (me is null)
            {
                return null;
            }

            var configured = (me.Modules ?? []).FirstOrDefault(m => string.Equals(m.Module, AssetCriticalityPermissions.Module, StringComparison.OrdinalIgnoreCase));
            var granted = AssetCriticalityPermissions.Resolve(me.FullAccess, configured?.Actions, me.Roles);
            cache.Set(key, granted, CacheFor);
            return granted;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or FormatException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(exception, "Could not read the caller's criticality permissions.");
            return null;
        }
    }

    private sealed record ModuleAccess(string Module, List<string>? Actions);

    private sealed record MyAccess(bool FullAccess, List<ModuleAccess>? Modules, List<string>? Roles);
}

/// <summary>
/// Asset Criticality rules (API-enforced, /api/asset-criticality only): View to read, Assess to assess, Approve for
/// Low / Medium / High, ApproveCritical for Critical and any lowering of an asset's criticality, Configure for the
/// configuration masters.
/// </summary>
public static class CriticalityAccess
{
    private const string PermissionsItem = "asset-criticality-permissions";

    public static bool CanApprove(IReadOnlySet<string> granted, string? level, string? previousLevel) =>
        granted.Contains(RequiredApproval(level, previousLevel));

    /// <summary>approvecritical for Critical or a lowered level, else approve.</summary>
    public static string RequiredApproval(string? level, string? previousLevel) =>
        LevelRank(level) == 4 || IsReduction(previousLevel, level)
            ? AssetCriticalityPermissions.ApproveCritical
            : AssetCriticalityPermissions.Approve;

    public static bool IsReduction(string? from, string? to) =>
        LevelRank(from) > 0 && LevelRank(to) > 0 && LevelRank(to) < LevelRank(from);

    /// <summary>1 = Low … 4 = Critical; 0 for anything else.</summary>
    public static int LevelRank(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "low" => 1,
        "medium" => 2,
        "high" => 3,
        "critical" => 4,
        _ => 0
    };

    /// <summary>The permissions the group filter loaded for this request.</summary>
    public static IReadOnlySet<string> Permissions(this HttpContext context) =>
        context.Items[PermissionsItem] as IReadOnlySet<string> ?? new HashSet<string>();

    public static IResult Forbidden(string message) =>
        Results.Json(new { message }, statusCode: StatusCodes.Status403Forbidden);

    /// <summary>Group filter: 401 without sign-in, 503 when permissions can't be read, 403 without View.</summary>
    public static RouteGroupBuilder RequireCriticalityView(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.User.Identity?.IsAuthenticated != true)
            {
                return Results.Json(new { message = "Sign in to continue." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var granted = await http.RequestServices.GetRequiredService<ICriticalityPermissionService>().GetAsync(http.RequestAborted);
            if (granted is null)
            {
                return Results.Json(new { message = "Your permissions could not be checked. Try again in a moment." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!granted.Contains(AssetCriticalityPermissions.View))
            {
                return Forbidden("Your role doesn't allow you to view Asset Criticality.");
            }

            http.Items[PermissionsItem] = granted;
            return await next(context);
        });
        return group;
    }

    /// <summary>Endpoint filter: 403 unless the caller has <paramref name="permission"/> (runs after the group filter).</summary>
    public static TBuilder RequireCriticalityPermission<TBuilder>(this TBuilder builder, string permission, string action)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
            context.HttpContext.Permissions().Contains(permission)
                ? await next(context)
                : Forbidden($"Your role doesn't allow you to {action}."));
        return builder;
    }
}
