using Microsoft.Extensions.Options;

namespace A360.Tracking.Api.Locating;

/// <summary>
/// Tag locating: a WebSocket that looks up a tag's latest gateway (NetworkTimeSeries) and bridges the front end to that
/// gateway's MQTT action / response topics. Uses the IMongoDatabase the Tracking service already registers.
/// </summary>
public static class LocatingExtensions
{
    public static IServiceCollection AddLocating(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocatingSettings>(configuration.GetSection(LocatingSettings.SectionName));
        services.AddSingleton<TagGatewayLookup>();
        services.AddSingleton<LocateCommandStore>();
        services.AddSingleton<GatewayMqttBridge>();
        services.AddHostedService(sp => sp.GetRequiredService<GatewayMqttBridge>());
        return services;
    }

    public static WebApplication MapLocating(this WebApplication app)
    {
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

        // ws://host:5490/ws/locate?access_token=<JWT>&tagid=c3000099138f  (protocol: see LocatingSession).
        // Browsers can't send a bearer header on a WebSocket, so the sign-in token comes as ?access_token=
        // (read by AddA360JwtAuthentication); an Authorization header works too. No valid token = 401, no socket.
        app.Map("/ws/locate", async (HttpContext context, IServiceProvider services) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
                return Results.BadRequest("WebSocket connection expected.");

            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Json(new { message = "Sign in to continue. Pass the token as ?access_token=." }, statusCode: StatusCodes.Status401Unauthorized);

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var session = ActivatorUtilities.CreateInstance<LocatingSession>(services, socket);
            await session.RunAsync(context.Request.Query["tagid"], context.RequestAborted);
            return Results.Empty;
        }).ExcludeFromDescription();

        var group = app.MapGroup("/api/locating").WithTags("Locating");

        // Plain HTTP lookup, handy for checking a tag without opening a socket (sign-in required, like every /api route).
        group.MapGet("/tags/{tagid}/gateway", async (string tagid, TagGatewayLookup lookup,
            IOptions<LocatingSettings> settings, CancellationToken ct) =>
        {
            var found = await lookup.FindLatestAsync(TagGatewayLookup.NormalizeId(tagid), ct);
            return found is null
                ? Results.NotFound(new { message = $"No NetworkTimeSeries data with a gatewayid for tag '{tagid}'." })
                : Results.Ok(new
                {
                    tagid = found.TagId,
                    gatewayid = found.GatewayId,
                    lastSeen = found.LastSeen,
                    rssi = found.Rssi,
                    topics = new
                    {
                        action = settings.Value.ActionTopicFor(found.GatewayId),
                        response = settings.Value.ResponseTopicFor(found.GatewayId),
                    },
                });
        });

        // Latest LED / buzzer commands sent to a tag and how they ended (sent / accepted / completed / failed / timeout).
        group.MapGet("/tags/{tagid}/commands", async (string tagid, LocateCommandStore store, CancellationToken ct, int? limit) =>
        {
            var docs = await store.RecentForTagAsync(TagGatewayLookup.NormalizeId(tagid), Math.Clamp(limit ?? 20, 1, 100), ct);
            var json = MongoDB.Bson.BsonExtensionMethods.ToJson(new MongoDB.Bson.BsonArray(docs), new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson });
            return Results.Content(json, "application/json");
        });

        group.MapGet("/health", (GatewayMqttBridge bridge) => Results.Ok(new { mqttConnected = bridge.IsConnected }));

        return app;
    }
}
