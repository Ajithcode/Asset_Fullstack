using A360.Tracking.Api.Telemetry;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Tracking &amp; Telemetry page: KPIs, tracked assets (one per asset, not per tag) with estimated location and live
/// readings, gateways, recent events, and an asset's telemetry trend. Read-only; built from the live tag readings.
/// </summary>
public static class TrackingTelemetryEndpoints
{
    public static RouteGroupBuilder MapTrackingTelemetryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tracking-telemetry").WithTags("Tracking & Telemetry");

        group.MapGet("/overview", GetOverviewAsync).WithName("GetTrackingTelemetryOverview");
        group.MapGet("/events", GetEventsAsync).WithName("GetTrackingTelemetryEvents");
        group.MapGet("/assets/{assetId}/trend", GetTrendAsync).WithName("GetTrackingTelemetryAssetTrend");

        return group;
    }

    /// <param name="range">1h, 24h (default) or 7d.</param>
    /// <param name="siteId">Master Data site id; all sites when empty.</param>
    private static async Task<IResult> GetOverviewAsync(TelemetryOverviewService service, CancellationToken cancellationToken, string? range = "24h", string? siteId = null)
    {
        return Results.Ok(await service.GetOverviewAsync(Normalize(range), siteId, cancellationToken));
    }

    /// <summary>Recent telemetry &amp; location events, paged (newest first).</summary>
    /// <param name="assetIds">Comma-separated asset ids to limit the events to (the page's asset filters).</param>
    /// <param name="category">Telemetry, Location, Connectivity or Device.</param>
    private static async Task<IResult> GetEventsAsync(
        TelemetryOverviewService service,
        CancellationToken cancellationToken,
        string? range = "24h",
        string? siteId = null,
        int page = 1,
        int pageSize = 10,
        string? assetIds = null,
        string? category = null)
    {
        var ids = (assetIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Results.Ok(await service.GetEventsAsync(Normalize(range), siteId, page, pageSize, ids, category, cancellationToken));
    }

    private static async Task<IResult> GetTrendAsync(string assetId, TelemetryOverviewService service, CancellationToken cancellationToken, string? range = "24h")
    {
        var trend = await service.GetTrendAsync(assetId.Trim(), Normalize(range), cancellationToken);
        return trend is null ? Results.NotFound(new { message = $"No tracked tags are linked to '{assetId}'." }) : Results.Ok(trend);
    }

    private static string Normalize(string? range) =>
        range is not null && TelemetryOverviewService.Ranges.ContainsKey(range.Trim().ToLowerInvariant()) ? range.Trim().ToLowerInvariant() : "24h";
}
