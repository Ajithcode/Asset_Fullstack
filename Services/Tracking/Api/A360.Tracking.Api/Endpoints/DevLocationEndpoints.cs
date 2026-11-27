using A360.Repository.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Dev-only tools for the fast zone tracking (404 unless DevTools:Enabled=true): latency per stage of the last zone changes
/// (device timestamp -> server receive -> worker read -> decision -> state saved -> published -> WebSocket sent) and the ground-truth
/// logger (a tester taps "I'm in this zone now"; the system latency per move is computed against the first matching decision).
/// Under /api/real-time-location/dev so the gateway's existing route forwards it.
/// </summary>
public static class DevLocationEndpoints
{
    private static readonly string[] Stages = ["deviceToRecv", "recvToRead", "readToDecision", "decisionToSaved", "savedToPublished", "publishedToWs", "total"];

    public static void MapDevLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/real-time-location/dev").WithTags("Dev location tools");
        group.MapGet("/time", (IConfiguration c) => Enabled(c) ? Results.Ok(new { serverMs = Now() }) : Results.NotFound());
        group.MapGet("/latency", GetLatencyAsync);
        group.MapGet("/tags", GetTagsAsync);
        group.MapGet("/zones", GetZonesAsync);
        group.MapPost("/ground-truth", PostGroundTruthAsync);
        group.MapGet("/ground-truth", GetGroundTruthAsync);
    }

    private static bool Enabled(IConfiguration c) => c.GetValue("DevTools:Enabled", false);
    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static long L(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsNumeric: true } v ? v.ToInt64() : 0;
    private static string? S(BsonDocument d, string n) => d.GetValue(n, BsonNull.Value) is { IsString: true } v ? v.AsString : null;

    private static Dictionary<string, long?> StageTimes(BsonDocument d)
    {
        long ts = L(d, "deviceTsMs"), recv = L(d, "recvMs"), read = L(d, "readMs"), dec = L(d, "decidedMs"), saved = L(d, "savedMs"),
            pub = L(d, "publishedMs"), ws = L(d, "wsSentMs");
        long? Diff(long a, long b) => a > 0 && b > 0 ? b - a : null;
        return new Dictionary<string, long?>
        {
            ["deviceToRecv"] = Diff(ts, recv), ["recvToRead"] = Diff(recv, read), ["readToDecision"] = Diff(read, dec),
            ["decisionToSaved"] = Diff(dec, saved), ["savedToPublished"] = Diff(saved, pub), ["publishedToWs"] = Diff(pub, ws), ["total"] = Diff(ts, ws),
        };
    }

    private static async Task<IResult> GetLatencyAsync(IMongoDatabase db, IConfiguration c, CancellationToken ct, int limit = 30)
    {
        if (!Enabled(c)) return Results.NotFound();
        var rows = await db.GetCollection<BsonDocument>("LocationLatency").Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("atMs")).Limit(Math.Clamp(limit, 1, 500)).ToListAsync(ct);
        var names = await ZoneNames.GetAsync(db, ct);
        var moves = rows.Select(d => new
        {
            id = d["_id"].ToString(), tag = S(d, "tag"), kind = S(d, "kind"), atMs = L(d, "atMs"),
            toZone = names.Resolve(S(d, "toZone"), null) ?? S(d, "toZone"), fromZone = names.Resolve(S(d, "fromZone"), null) ?? S(d, "fromZone"),
            stages = StageTimes(d),
        }).ToList();
        var stats = Stages.ToDictionary(st => st, st =>
        {
            var v = moves.Select(m => m.stages[st]).Where(x => x.HasValue).Select(x => x!.Value).OrderBy(x => x).ToList();
            return v.Count == 0 ? null : new { n = v.Count, p50 = v[(v.Count - 1) / 2], p95 = v[(int)Math.Ceiling(0.95 * (v.Count - 1))] };
        });
        return Results.Ok(new { serverMs = Now(), stats, moves });
    }

    private static async Task<IResult> GetTagsAsync(IMongoDatabase db, IConfiguration c, CancellationToken ct)
    {
        if (!Enabled(c)) return Results.NotFound();
        var rows = await db.GetCollection<BsonDocument>("TrackingTag").Find(new BsonDocument("is_deleted", new BsonDocument("$ne", true)))
            .Project(new BsonDocument { { "tag_id", 1 }, { "asset_registration_id", 1 } }).ToListAsync(ct);
        return Results.Ok(rows.Select(d => new { tagId = S(d, "tag_id"), asset = S(d, "asset_registration_id") })
            .Where(t => t.tagId != null).OrderBy(t => t.asset).ThenBy(t => t.tagId));
    }

    private static async Task<IResult> GetZonesAsync(IMongoDatabase db, IConfiguration c, CancellationToken ct)
    {
        if (!Enabled(c)) return Results.NotFound();
        var plots = await db.GetCollection<BsonDocument>("maps_plots")
            .Find(new BsonDocument { { "kind", "device" }, { "is_deleted", new BsonDocument("$ne", true) } }).ToListAsync(ct);
        var names = await ZoneNames.GetAsync(db, ct);
        var zones = plots.Select(p => S(p, "zone_id") ?? (p.GetValue("location", BsonNull.Value) is { IsBsonDocument: true } l ? S(l.AsBsonDocument, "zone_id") : null))
            .Where(z => z != null).Distinct().Select(z => new { zoneId = z, name = names.Resolve(z, null) ?? z }).OrderBy(z => z.name);
        return Results.Ok(zones);
    }

    public sealed record GroundTruthRequest(string TagId, string ZoneId, long? ClientMs, long? ClockOffsetMs, string? Note);

    private static async Task<IResult> PostGroundTruthAsync(GroundTruthRequest body, IMongoDatabase db, IConfiguration c, CancellationToken ct)
    {
        if (!Enabled(c)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(body.TagId) || string.IsNullOrWhiteSpace(body.ZoneId)) return Results.BadRequest("tagId and zoneId are required");
        var received = Now();
        // The tap time on the server clock: the phone's own clock minus the offset it measured against /dev/time; else our receive time.
        var tap = body.ClientMs is { } cm ? cm - (body.ClockOffsetMs ?? 0) : received;
        var doc = new BsonDocument
        {
            { "at", new BsonDateTime(tap) }, { "tapMs", tap }, { "receivedMs", received }, { "tagId", body.TagId.Trim().ToLowerInvariant() },
            { "zoneId", body.ZoneId }, { "note", body.Note is null ? BsonNull.Value : body.Note },
        };
        await db.GetCollection<BsonDocument>("GroundTruthEvent").InsertOneAsync(doc, cancellationToken: ct);
        return Results.Ok(new { id = doc["_id"].ToString(), tapMs = tap });
    }

    /// <summary>Each tap with the first decision into that zone for that tag within 2 min after it (the shadow log is not used: only live decisions have WebSocket stages).</summary>
    private static async Task<IResult> GetGroundTruthAsync(IMongoDatabase db, IConfiguration c, CancellationToken ct, int limit = 50)
    {
        if (!Enabled(c)) return Results.NotFound();
        var taps = await db.GetCollection<BsonDocument>("GroundTruthEvent").Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("tapMs")).Limit(Math.Clamp(limit, 1, 500)).ToListAsync(ct);
        var names = await ZoneNames.GetAsync(db, ct);
        var latency = db.GetCollection<BsonDocument>("LocationLatency");
        var result = new List<object>();
        foreach (var t in taps)
        {
            var tap = L(t, "tapMs");
            var match = await latency.Find(new BsonDocument { { "tag", S(t, "tagId") }, { "toZone", S(t, "zoneId") },
                    { "decidedMs", new BsonDocument { { "$gte", tap - 5000 }, { "$lte", tap + 120_000 } } } })
                .Sort(Builders<BsonDocument>.Sort.Ascending("decidedMs")).FirstOrDefaultAsync(ct);
            result.Add(new
            {
                id = t["_id"].ToString(), tag = S(t, "tagId"), zoneId = S(t, "zoneId"), zone = names.Resolve(S(t, "zoneId"), null) ?? S(t, "zoneId"),
                tapMs = tap, note = S(t, "note"),
                decisionMs = match == null ? (long?)null : L(match, "decidedMs") - tap,
                wsMs = match != null && L(match, "wsSentMs") > 0 ? L(match, "wsSentMs") - tap : (long?)null,
                matched = match != null,
            });
        }
        return Results.Ok(result);
    }
}
