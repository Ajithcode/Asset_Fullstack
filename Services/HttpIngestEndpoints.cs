using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StackExchange.Redis;
using WorkerNetworkService.Settings;

namespace WorkerNetworkService.Services;

/// <summary>
/// Maps one endpoint per entry in Http:Methods. Each request's payload (body, or query string for GET)
/// is added to the Redis stream Http:TargetStream, and RedisStreamIngestWorker then stores it in MongoDB.
/// </summary>
public static class HttpIngestEndpoints
{
    public static void MapHttpIngest(this WebApplication app, HttpSettings settings)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HttpIngestEndpoints));

        foreach (var endpoint in settings.Methods)
        {
            var method = endpoint.Method.ToUpperInvariant();
            app.MapMethods(endpoint.Url, [method], async (HttpRequest request, IConnectionMultiplexer redis) =>
            {
                string payload;
                if (method == "GET")
                {
                    payload = Uri.UnescapeDataString(request.QueryString.Value?.TrimStart('?') ?? "");
                }
                else
                {
                    using var reader = new StreamReader(request.Body);
                    payload = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
                }

                if (string.IsNullOrWhiteSpace(payload))
                    return Results.BadRequest("Empty payload.");

                var id = await redis.GetDatabase().StreamAddAsync(settings.TargetStream,
                [
                    new NameValueEntry(settings.PayloadField, payload),
                    new NameValueEntry("source", "http"),
                    new NameValueEntry("endpoint", endpoint.Name),
                ]);
                logger.LogInformation("HTTP received {Method} {Url} from {Remote} ({Bytes} bytes): {Preview}",
                    method, endpoint.Url, request.HttpContext.Connection.RemoteIpAddress, payload.Length,
                    payload.Length > 200 ? payload[..200] + "..." : payload);
                return Results.Ok(new { received = true, id = id.ToString() });
            }).WithName(endpoint.Name);

            logger.LogInformation("HTTP endpoint {Name}: {Method} {Url}", endpoint.Name, method, endpoint.Url);
        }
    }
}
