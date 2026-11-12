using System.Net.WebSockets;
using System.Text.Json;
using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Endpoints;

public static class NetworkStatusEndpoints
{
    private const int DefaultLivePollSeconds = 5;

    // An asset's readings (RSSI moves with the asset) are re-read more often than the overall counts.
    private const int DefaultAssetLivePollSeconds = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapNetworkStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        // Lives under /api/dashboards so the gateway's existing dashboard route forwards it.
        var group = routes.MapGroup("/api/dashboards/network-status").WithTags("Network Status");

        group.MapGet("", GetNetworkStatusCountsAsync).WithName("GetNetworkStatusCounts");
        group.MapGet("/tags", GetTagRecordCountsAsync).WithName("GetTagRecordCounts");
        group.MapGet("/live", StreamNetworkStatusCountsAsync).WithName("StreamNetworkStatusCounts");

        // One asset's tags with their latest reading (battery, RSSI, online status, time), and the same as a live WebSocket.
        group.MapGet("/assets/{assetId}", GetAssetTelemetryAsync).WithName("GetAssetTelemetry");
        group.MapGet("/assets/{assetId}/live", StreamAssetTelemetryAsync).WithName("StreamAssetTelemetry");

        // Assets per gateway (by the gateway of each tag's newest reading), and the same as a live WebSocket.
        group.MapGet("/gateways", GetGatewayAssetsAsync).WithName("GetGatewayAssets");
        group.MapGet("/gateways/live", StreamGatewayAssetsAsync).WithName("StreamGatewayAssets");

        return group;
    }

    private static async Task<IResult> GetGatewayAssetsAsync(INetworkStatusRepository repository, CancellationToken cancellationToken)
    {
        var gateways = await repository.GetGatewayTagsAsync(cancellationToken);

        return Results.Ok(gateways.Select(GatewayAssetsResponse.FromGateway).ToList());
    }

    // WebSocket: sends the assets per gateway on connect, then again whenever an asset's tag moves to another gateway.
    private static Task StreamGatewayAssetsAsync(
        HttpContext context,
        INetworkStatusRepository repository,
        IConfiguration configuration)
    {
        var pollSeconds = configuration.GetValue("NetworkStatus:GatewayLivePollSeconds", DefaultLivePollSeconds);

        return SendWhenChangedAsync(context, pollSeconds, async cancellationToken =>
            (await repository.GetGatewayTagsAsync(cancellationToken)).Select(GatewayAssetsResponse.FromGateway).ToList());
    }

    /// <summary><paramref name="assetId"/> is the asset registration id (e.g. AR000006) or the asset tag (e.g. TB123).</summary>
    private static async Task<IResult> GetAssetTelemetryAsync(
        string assetId,
        INetworkStatusRepository repository,
        CancellationToken cancellationToken)
    {
        var telemetry = await repository.GetAssetTagTelemetryAsync(assetId, cancellationToken);

        return telemetry is null
            ? Results.NotFound(new { message = $"Asset '{assetId}' was not found." })
            : Results.Ok(AssetTelemetryResponse.FromTelemetry(telemetry, DateTime.UtcNow));
    }

    // WebSocket: sends the asset's tag readings on connect, then again whenever a reading (RSSI, battery, status...) changes.
    private static Task StreamAssetTelemetryAsync(
        string assetId,
        HttpContext context,
        INetworkStatusRepository repository,
        IConfiguration configuration)
    {
        var pollSeconds = configuration.GetValue("NetworkStatus:AssetLivePollSeconds", DefaultAssetLivePollSeconds);

        return SendWhenChangedAsync(context, pollSeconds, async cancellationToken =>
        {
            var telemetry = await repository.GetAssetTagTelemetryAsync(assetId, cancellationToken);
            return telemetry is null
                ? new AssetTelemetryResponse(assetId, null, null, [])
                : AssetTelemetryResponse.FromTelemetry(telemetry, DateTime.UtcNow);
        });
    }

    private static async Task<IResult> GetNetworkStatusCountsAsync(
        INetworkStatusRepository repository,
        CancellationToken cancellationToken)
    {
        var counts = await repository.GetStatusCountsAsync(cancellationToken);

        return Results.Ok(NetworkStatusCountResponse.FromCounts(counts));
    }

    // Tags ordered by how many records they have, highest first, with the asset mapped to each tag.
    private static async Task<IResult> GetTagRecordCountsAsync(
        INetworkStatusRepository repository,
        CancellationToken cancellationToken)
    {
        var tags = await repository.GetTagRecordCountsAsync(cancellationToken);

        return Results.Ok(tags.Select(TagRecordCountResponse.FromTagRecordCount));
    }

    // WebSocket: sends the online/offline counts on connect, then again whenever they change.
    private static Task StreamNetworkStatusCountsAsync(
        HttpContext context,
        INetworkStatusRepository repository,
        IConfiguration configuration)
    {
        var pollSeconds = configuration.GetValue("NetworkStatus:LivePollSeconds", DefaultLivePollSeconds);

        return SendWhenChangedAsync(context, pollSeconds, async cancellationToken =>
            NetworkStatusCountResponse.FromCounts(await repository.GetStatusCountsAsync(cancellationToken)));
    }

    // Accepts the WebSocket, sends read()'s result on connect and again whenever it changes.
    // NetworkTimeSeries is a time-series collection (no change streams), so the data is re-read on an interval.
    private static async Task SendWhenChangedAsync<T>(HttpContext context, int pollSeconds, Func<CancellationToken, Task<T>> read)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection expected.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, pollSeconds)));

        var closeTask = WaitForCloseAsync(socket, connection);
        byte[]? lastSent = null;

        try
        {
            do
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(await read(connection.Token), JsonOptions);

                if (lastSent is null || !payload.AsSpan().SequenceEqual(lastSent))
                {
                    await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, connection.Token);
                    lastSent = payload;
                }
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

    // Reads (and discards) client messages so a close frame is noticed and the send loop stops.
    private static async Task WaitForCloseAsync(WebSocket socket, CancellationTokenSource connection)
    {
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, connection.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
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
