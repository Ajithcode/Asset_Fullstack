using System.Net.WebSockets;
using System.Text.Json;
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

        // Each unique tag's newest NetworkTimeSeries reading + the zone / floor of the gateway that heard it (maps_plots),
        // and the same as a live WebSocket (under this path so the gateway's existing real-time-location route forwards it).
        group.MapGet("/tags", GetTagLocationsAsync).WithName("GetRealTimeTagLocations");
        group.MapGet("/tags/live", StreamTagLocationsAsync).WithName("StreamRealTimeTagLocations");

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

    private const int DefaultTagLivePollSeconds = 5;
    private const int DefaultTagWindowMinutes = 24 * 60;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="minutes">Only tags heard in the last N minutes (default 1440 = 24 h, max 7 days).</param>
    /// <param name="zonedOnly">Only tags heard by a gateway that is plotted on a floor plan.</param>
    private static async Task<IResult> GetTagLocationsAsync(
        TagLocationService service,
        CancellationToken cancellationToken,
        int? minutes = null,
        string? siteId = null,
        string? floorPlanId = null,
        string? zoneId = null,
        bool zonedOnly = false)
    {
        return Results.Ok(await service.GetLatestAsync(Since(minutes), siteId, floorPlanId, zoneId, zonedOnly, cancellationToken));
    }

    /// <summary>
    /// WebSocket: ws://host/api/real-time-location/tags/live?access_token=&lt;JWT&gt; (same filters as GET /tags).
    /// On connect: {"type":"snapshot", total, online, plotted, withAsset, tags:[TagLocation...]}.
    /// Then, only when something changed: {"type":"update", total, online, plotted, withAsset,
    ///   changed:[TagLocation...]  new tags, or a tag now on another gateway / zone, or its asset link changed (full row),
    ///   seen:[{tagId, gatewayId, timestamp, status, connection, rssi, battery}]  same tag + same gateway: update the timestamp,
    ///   removed:[tagId...]  tags that dropped out of the window / filters}.
    /// </summary>
    private static async Task StreamTagLocationsAsync(
        HttpContext context,
        TagLocationService service,
        IConfiguration configuration,
        int? minutes = null,
        string? siteId = null,
        string? floorPlanId = null,
        string? zoneId = null,
        bool zonedOnly = false)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection expected.");
            return;
        }

        var pollSeconds = Math.Max(1, configuration.GetValue("RealTimeLocation:TagLivePollSeconds", DefaultTagLivePollSeconds));

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        var closeTask = WaitForCloseAsync(socket, connection);

        // What the client already has, per tag: the tag without its reading fields (to spot a gateway / zone / asset
        // change) and the reading fields (to spot a new timestamp on the same tag + gateway).
        Dictionary<string, (string Placement, string Reading)>? sent = null;

        try
        {
            // NetworkTimeSeries is a time-series collection (no change streams), so it is re-read on an interval.
            do
            {
                var snapshot = await service.GetLatestAsync(Since(minutes), siteId, floorPlanId, zoneId, zonedOnly, connection.Token);
                var current = snapshot.Tags.ToDictionary(t => t.TagId, t => (Placement: PlacementKey(t), Reading: ReadingKey(t)));

                object? message = null;
                if (sent is null)
                {
                    message = new { type = "snapshot", snapshot.Total, snapshot.Online, snapshot.Plotted, snapshot.WithAsset, snapshot.Tags };
                }
                else
                {
                    // New tag, or same tag on another gateway (or its zone / asset changed): the whole row.
                    var changed = snapshot.Tags
                        .Where(t => !sent.TryGetValue(t.TagId, out var before) || before.Placement != current[t.TagId].Placement)
                        .ToList();
                    // Same tag + same gateway, only the reading moved on: just the timestamp (and the reading values).
                    var seen = snapshot.Tags
                        .Where(t => sent.TryGetValue(t.TagId, out var before)
                            && before.Placement == current[t.TagId].Placement
                            && before.Reading != current[t.TagId].Reading)
                        .Select(t => new TagSeen(t.TagId, t.GatewayId, t.Timestamp, t.Status, t.Connection, t.Rssi, t.Battery))
                        .ToList();
                    var removed = sent.Keys.Where(id => !current.ContainsKey(id)).ToList();

                    if (changed.Count > 0 || seen.Count > 0 || removed.Count > 0)
                    {
                        message = new { type = "update", snapshot.Total, snapshot.Online, snapshot.Plotted, snapshot.WithAsset, changed, seen, removed };
                    }
                }

                if (message is not null)
                {
                    var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
                    await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, connection.Token);
                }

                sent = current;
            }
            while (await timer.WaitForNextTickAsync(connection.Token));
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            // Client closed the connection or went away.
        }

        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        await closeTask;
    }

    /// <summary>A tag seen again by the same gateway: only its reading changed.</summary>
    private sealed record TagSeen(string TagId, string GatewayId, DateTime Timestamp, string? Status, string Connection, double? Rssi, double? Battery);

    // Everything except the reading: gateway, zone / floor, tag registration, asset, match issues.
    private static string PlacementKey(TagLocation t) => JsonSerializer.Serialize(
        t with { Timestamp = default, Status = null, Connection = string.Empty, Rssi = null, Battery = null }, JsonOptions);

    private static string ReadingKey(TagLocation t) => $"{t.Timestamp:O}|{t.Status}|{t.Connection}|{t.Rssi}|{t.Battery}";

    private static DateTime Since(int? minutes) =>
        DateTime.UtcNow - TimeSpan.FromMinutes(Math.Clamp(minutes ?? DefaultTagWindowMinutes, 1, 7 * 24 * 60));

    // Reads (and discards) client messages so a close frame is noticed and the send loop stops.
    private static async Task WaitForCloseAsync(WebSocket socket, CancellationTokenSource connection)
    {
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, connection.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
        }
        finally
        {
            connection.Cancel();
        }
    }
}
