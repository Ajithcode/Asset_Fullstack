using A360.Tracking.Api.Telemetry;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Real-Time Location page (read-only): assets in view with movement / freshness / confidence, zone trail and location events.</summary>
public static class RealTimeLocationEndpoints
{
    public static RouteGroupBuilder MapRealTimeLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/real-time-location").WithTags("Real-Time Location");

        group.MapGet("/overview", GetOverviewAsync).WithName("GetRealTimeLocationOverview");
        group.MapGet("/assets/{assetId}/trail", GetTrailAsync).WithName("GetRealTimeLocationTrail");
        group.MapGet("/events", GetEventsAsync).WithName("GetRealTimeLocationEvents");

        return group;
    }

    /// <param name="floorPlanId">The floor plan shown on the map; "assets in view" are the assets located on it. All floors when empty.</param>
    private static async Task<IResult> GetOverviewAsync(RealTimeLocationService service, CancellationToken cancellationToken, string? siteId = null, string? floorPlanId = null)
    {
        return Results.Ok(await service.GetOverviewAsync(siteId, floorPlanId, cancellationToken));
    }

    /// <param name="range">30m (default), 1h, 24h or 7d.</param>
    private static async Task<IResult> GetTrailAsync(string assetId, RealTimeLocationService service, CancellationToken cancellationToken, string? range = "30m")
    {
        var key = range is not null && RealTimeLocationService.TrailRanges.ContainsKey(range.Trim().ToLowerInvariant()) ? range.Trim().ToLowerInvariant() : "30m";
        var trail = await service.GetTrailAsync(assetId.Trim(), key, cancellationToken);
        return trail is null ? Results.NotFound(new { message = $"No tracked tags are linked to '{assetId}'." }) : Results.Ok(trail);
    }

    /// <param name="range">1h, 24h (default) or 7d.</param>
    /// <param name="eventType">e.g. Zone Entry, Zone Exit, Floor Change, Stale Location, Location Lost, Geofence Entry…</param>
    private static async Task<IResult> GetEventsAsync(
        RealTimeLocationService service,
        CancellationToken cancellationToken,
        string? range = "24h",
        string? siteId = null,
        int page = 1,
        int pageSize = 10,
        string? assetId = null,
        string? eventType = null)
    {
        var key = range is not null && TelemetryOverviewService.Ranges.ContainsKey(range.Trim().ToLowerInvariant()) ? range.Trim().ToLowerInvariant() : "24h";
        return Results.Ok(await service.GetEventsAsync(key, siteId, page, pageSize, assetId, eventType, cancellationToken));
    }
}
