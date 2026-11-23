using System.Text;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using NetworkServer.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var section = sp.GetRequiredService<IConfiguration>().GetSection("Mongo");
    return new MongoClient(section["ConnectionString"] ?? "mongodb://localhost:27017")
        .GetDatabase(section["Database"] ?? "Asset360");
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = ConfigurationOptions.Parse(sp.GetRequiredService<IConfiguration>()["Redis:ConnectionString"] ?? "localhost:6379");
    options.AbortOnConnectFail = false; // keep running and reconnect when Redis comes back
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<RedisStreamPublisher>();
builder.Services.AddSingleton<TimeSeriesWriter>();
builder.Services.AddSingleton<TagAggregator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TagAggregator>());
builder.Services.AddSingleton<MongoMessageStore>();
builder.Services.AddHostedService<MqttBrokerService>();
builder.Services.AddHostedService<MqttBridgeService>();

var app = builder.Build();

app.MapGet("/", () => "NetworkServer running. Send data to /api/ingest/{deviceId} (HTTP) or publish via MQTT.");

// ---- HTTP ingest ----
// POST: body is read as raw bytes, so any content type (text/plain, application/json, octet-stream) works.
//   curl -X POST http://host:5000/api/ingest/sensor1 -d 'temp=23.5,hum=40'
// GET: for devices that can only hit a URL; the query string is the payload.
//   curl 'http://host:5000/api/ingest/sensor1?temp=23.5&hum=40'
async Task<IResult> IngestBody(HttpRequest request, MongoMessageStore store, string? deviceId, CancellationToken ct)
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms, ct);
    return Results.Ok(await store.IngestAsync("http", deviceId, null, ms.ToArray(), ct));
}

async Task<IResult> IngestQuery(HttpRequest request, MongoMessageStore store, string? deviceId, CancellationToken ct)
{
    var raw = Uri.UnescapeDataString(request.QueryString.Value?.TrimStart('?') ?? "");
    return Results.Ok(await store.IngestAsync("http", deviceId, null, Encoding.ASCII.GetBytes(raw), ct));
}

app.MapPost("/api/ingest", (HttpRequest req, MongoMessageStore store, CancellationToken ct) =>
    IngestBody(req, store, null, ct));
app.MapPost("/api/ingest/{deviceId}", (string deviceId, HttpRequest req, MongoMessageStore store, CancellationToken ct) =>
    IngestBody(req, store, deviceId, ct));
app.MapGet("/api/ingest/{deviceId}", (string deviceId, HttpRequest req, MongoMessageStore store, CancellationToken ct) =>
    IngestQuery(req, store, deviceId, ct));

// ---- Read back ----
//   GET /api/messages?deviceId=sensor1&limit=50
app.MapGet("/api/messages", async (string? deviceId, int? limit, MongoMessageStore store, CancellationToken ct) =>
{
    var messages = await store.ListAsync(deviceId, Math.Clamp(limit ?? 100, 1, 1000), ct);
    return Results.Ok(messages.Select(m => new
    {
        id = m.Id.ToString(),
        m.ReceivedAt,
        m.Source,
        m.DeviceId,
        m.Topic,
        m.Format,
        m.Raw,
        Data = ToJsonNode(m.Data),
    }));
});

app.Run();

static JsonNode? ToJsonNode(BsonValue value) => value.BsonType switch
{
    BsonType.Document => new JsonObject(value.AsBsonDocument.Select(e =>
        KeyValuePair.Create(e.Name, ToJsonNode(e.Value)))),
    BsonType.Array => new JsonArray(value.AsBsonArray.Select(ToJsonNode).ToArray()),
    BsonType.Double => JsonValue.Create(value.AsDouble),
    BsonType.Int32 => JsonValue.Create(value.AsInt32),
    BsonType.Int64 => JsonValue.Create(value.AsInt64),
    BsonType.Boolean => JsonValue.Create(value.AsBoolean),
    BsonType.DateTime => JsonValue.Create(value.ToUniversalTime()),
    BsonType.Null => null,
    _ => JsonValue.Create(value.ToString()),
};
