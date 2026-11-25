using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using A360.Security;

namespace A360.Asset.Api.Services;

/// <summary>A criticality level from the MDM Criticality master, e.g. Critical / CRIT-A / #C62828.</summary>
/// <param name="Rank">Severity for sorting: 4 = Critical, 3 = High, 2 = Medium, 1 = Low, 0 = any other level.</param>
public sealed record CriticalityLevelInfo(string Name, string? Code, string? Color, int Rank, int? MinScore = null, int? MaxScore = null, string? Id = null);

/// <summary>Criticality levels owned by the Master Data Management service (GET /api/master-data/criticalities).</summary>
public interface ICriticalityLevelClient
{
    /// <summary>Active levels, most severe first. Falls back to Critical / High / Medium / Low when MDM is unreachable.</summary>
    Task<IReadOnlyList<CriticalityLevelInfo>> GetLevelsAsync(CancellationToken cancellationToken = default);
}

/// <remarks>
/// Levels change rarely, so they are cached per client for five minutes. The caller's token is passed on because
/// MDM checks the sign-in. The fallback list is cached for 30 seconds only, so MDM is asked again soon.
/// </remarks>
public sealed class CriticalityLevelClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    ILogger<CriticalityLevelClient> logger) : ICriticalityLevelClient
{
    public const string ClientName = "A360MasterData";

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FallbackCacheFor = TimeSpan.FromSeconds(30);

    public static readonly IReadOnlyList<CriticalityLevelInfo> Defaults =
    [
        new("Critical", null, "#C62828", 4, 85, 100),
        new("High", null, "#EF6C00", 3, 65, 84),
        new("Medium", null, "#F9A825", 2, 40, 64),
        new("Low", null, "#2E7D32", 1, 20, 39)
    ];

    public async Task<IReadOnlyList<CriticalityLevelInfo>> GetLevelsAsync(CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext;
        var clientId = context?.User.FindFirst(A360ClaimTypes.ClientId)?.Value ?? string.Empty;
        var key = "asset-criticality-levels:" + clientId;
        if (cache.TryGetValue(key, out IReadOnlyList<CriticalityLevelInfo>? cached) && cached is not null)
        {
            return cached;
        }

        var levels = await FetchAsync(context, cancellationToken);
        cache.Set(key, levels ?? Defaults, levels is null ? FallbackCacheFor : CacheFor);
        return levels ?? Defaults;
    }

    private async Task<IReadOnlyList<CriticalityLevelInfo>?> FetchAsync(HttpContext? context, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/master-data/criticalities?status=Active");
        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        try
        {
            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET /api/master-data/criticalities returned {Status}; using the default levels.", (int)response.StatusCode);
                return null;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<MasterCriticality>>(cancellationToken) ?? [];
            var levels = rows
                .Where(row => !row.IsDeleted && !string.IsNullOrWhiteSpace(row.CriticalityName)
                    && !string.Equals(row.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                .Select(row => new CriticalityLevelInfo(row.CriticalityName!.Trim(), row.Code, row.Color, Security.CriticalityAccess.LevelRank(row.CriticalityName), row.MinScore, row.MaxScore, row.Id))
                .OrderByDescending(level => level.MaxScore ?? -1)
                .ThenByDescending(level => level.Rank)
                .ThenBy(level => level.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return levels.Count == 0 ? null : levels;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(exception, "Could not read the criticality levels from MDM; using the default levels.");
            return null;
        }
    }

    private sealed record MasterCriticality(string? Id, string? CriticalityName, string? Code, string? Color, string? Status, bool IsDeleted, int? MinScore = null, int? MaxScore = null);
}
