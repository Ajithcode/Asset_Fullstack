using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace A360.Security;

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
