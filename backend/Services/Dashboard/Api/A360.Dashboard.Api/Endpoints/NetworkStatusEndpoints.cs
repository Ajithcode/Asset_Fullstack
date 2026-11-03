using System.Net.WebSockets;
using System.Text.Json;
using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Endpoints;

public static class NetworkStatusEndpoints
{
    private const int DefaultLivePollSeconds = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapNetworkStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        // Lives under /api/dashboards so the gateway's existing dashboard route forwards it.
        var group = routes.MapGroup("/api/dashboards/network-status").WithTags("Network Status");

        group.MapGet("", GetNetworkStatusCountsAsync).WithName("GetNetworkStatusCounts");
        group.MapGet("/tags", GetTagRecordCountsAsync).WithName("GetTagRecordCounts");
        group.MapGet("/live", StreamNetworkStatusCountsAsync).WithName("StreamNetworkStatusCounts");

        return group;
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
    // NetworkTimeSeries is a time-series collection (no change streams), so the counts are re-read on an interval.
    private static async Task StreamNetworkStatusCountsAsync(
        HttpContext context,
        INetworkStatusRepository repository,
        IConfiguration configuration)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection expected.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        var pollSeconds = Math.Max(1, configuration.GetValue("NetworkStatus:LivePollSeconds", DefaultLivePollSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));

        var closeTask = WaitForCloseAsync(socket, connection);
        NetworkStatusCountResponse? lastSent = null;

        try
        {
            do
            {
                var counts = NetworkStatusCountResponse.FromCounts(
                    await repository.GetStatusCountsAsync(connection.Token));

                if (counts != lastSent)
                {
                    var payload = JsonSerializer.SerializeToUtf8Bytes(counts, JsonOptions);
                    await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, connection.Token);
                    lastSent = counts;
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
