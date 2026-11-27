using A360.Security;
using A360.Tracking.Api.Alerts;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;
using MongoDB.Driver;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Alert feed for the web bell and the mobile app (polled every 10 s): new alerts since a time, the unacknowledged count,
/// acknowledge one / all, and which notification channels this server can deliver. See docs/MOBILE-ALERTS.md.
/// </summary>
public static class AlertFeedEndpoints
{
    public static RouteGroupBuilder MapAlertFeedEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/alerts").WithTags("Alert feed");
        group.MapGet("/feed", GetFeedAsync).WithName("GetAlertFeed");
        group.MapGet("/channels", GetChannels).WithName("GetAlertChannels");
        group.MapPost("/acknowledge-all", AcknowledgeAllAsync).WithName("AcknowledgeAllAlerts");
        group.MapPost("/{alertId}/acknowledge", AcknowledgeAsync).WithName("AcknowledgeAlert");
        return group;
    }

    public sealed record FeedAlert(string AlertId, DateTime RaisedAt, string Severity, string EventType, string? AssetId, string AssetName, string? Zone,
        string? SiteName, string? Message, string Status, string? RuleName, List<string> Channels, bool Sound, string? AcknowledgedBy, DateTime? AcknowledgedAt);

    public sealed record FeedResponse(DateTime ServerTime, int Unacknowledged, List<FeedAlert> Items);

    /// <param name="since">ISO time: alerts raised, or acknowledged, after it (newest first). Without it: the latest alerts.</param>
    /// <param name="status">Only this status, e.g. Open for the bell.</param>
    /// <param name="limit">1-200, default 20.</param>
    private static async Task<IResult> GetFeedAsync(IMongoDatabase database, CancellationToken ct, DateTime? since = null, string? status = null, int? limit = null)
    {
        var alerts = database.GetCollection<IoTAlert>("IoTAlert");
        var f = Builders<IoTAlert>.Filter;
        var parts = new List<FilterDefinition<IoTAlert>> { f.Ne(a => a.IsDeleted, true) };
        if (since is not null)
        {
            var t = since.Value.ToUniversalTime();
            parts.Add(f.Or(f.Gt(a => a.RaisedAt, t), f.Gt(a => a.AcknowledgedAt, t)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = ContractText.Canonical(status, IoTAlertStatus.All) ?? status.Trim();
            parts.Add(s == IoTAlertStatus.Open ? f.Or(f.Eq(a => a.Status, s), f.Eq(a => a.Status, null)) : f.Eq(a => a.Status, s));
        }

        var rows = await alerts.Find(f.And(parts)).SortByDescending(a => a.RaisedAt).Limit(Math.Clamp(limit ?? 20, 1, 200)).ToListAsync(ct);
        var unacknowledged = (int)await alerts.CountDocumentsAsync(f.And(f.Ne(a => a.IsDeleted, true), f.Or(f.Eq(a => a.Status, IoTAlertStatus.Open), f.Eq(a => a.Status, null))), cancellationToken: ct);

        var ruleIds = rows.Select(a => a.AlertRuleId).Where(id => id is not null).Distinct().ToList();
        var rules = ruleIds.Count == 0 ? [] : await database.GetCollection<AlertRule>("AlertRule").Find(r => ruleIds.Contains(r.Id)).ToListAsync(ct);
        var ruleById = rules.ToDictionary(r => r.Id);
        var zoneNames = await A360.Repository.Repositories.ZoneNames.GetAsync(database, ct);
        var items = rows.Select(a =>
        {
            var rule = a.AlertRuleId is not null ? ruleById.GetValueOrDefault(a.AlertRuleId) : null;
            return new FeedAlert(a.AlertId, a.RaisedAt, a.RuleSeverity ?? rule?.Severity ?? a.Severity, a.EventType, a.AssetRegistrationId, a.AssetName, zoneNames.ResolveGeofence(a.GeofenceId ?? rule?.GeofenceId, a.GeofenceName ?? rule?.GeofenceName),
                a.SiteName, a.Message, a.Status ?? IoTAlertStatus.Open, rule?.Name, rule?.NotificationChannels ?? [], rule?.NotifySound ?? false, a.AcknowledgedBy, a.AcknowledgedAt);
        }).ToList();
        return Results.Ok(new FeedResponse(DateTime.UtcNow, unacknowledged, items));
    }

    private static async Task<IResult> AcknowledgeAsync(string alertId, IMongoDatabase database, ICurrentUserAccessor currentUser, CancellationToken ct)
    {
        var alerts = database.GetCollection<IoTAlert>("IoTAlert");
        var alert = await alerts.Find(a => a.AlertId == alertId.Trim() && a.IsDeleted != true).FirstOrDefaultAsync(ct);
        if (alert is null)
        {
            return Results.NotFound(new { message = $"Alert '{alertId}' was not found." });
        }

        if ((alert.Status ?? IoTAlertStatus.Open) == IoTAlertStatus.Open)
        {
            await Apply(alerts, alert, currentUser.GetCurrentUser(), ct);
        }

        return Results.Ok(new { alertId = alert.AlertId, status = alert.Status, acknowledgedBy = alert.AcknowledgedBy, acknowledgedAt = alert.AcknowledgedAt });
    }

    /// <summary>Acknowledges every Open alert.</summary>
    private static async Task<IResult> AcknowledgeAllAsync(IMongoDatabase database, ICurrentUserAccessor currentUser, CancellationToken ct)
    {
        var alerts = database.GetCollection<IoTAlert>("IoTAlert");
        var open = await alerts.Find(a => a.IsDeleted != true && (a.Status == IoTAlertStatus.Open || a.Status == null)).ToListAsync(ct);
        var user = currentUser.GetCurrentUser();
        foreach (var alert in open)
        {
            await Apply(alerts, alert, user, ct);
        }

        return Results.Ok(new { acknowledged = open.Count });
    }

    private static async Task Apply(IMongoCollection<IoTAlert> alerts, IoTAlert alert, CurrentUser? user, CancellationToken ct)
    {
        alert.ApplyStatus(IoTAlertStatus.Acknowledged, user);
        alert.UpdatedBy = user?.UserName;
        alert.UpdatedAt = DateTime.UtcNow;
        await alerts.ReplaceOneAsync(a => a.Id == alert.Id, alert, cancellationToken: ct);
    }

    /// <summary>Which channels this server can deliver. Email needs the Smtp__* settings.</summary>
    private static IResult GetChannels(AlertEmailNotifier email) => Results.Ok(new
    {
        inApp = new { available = true },
        push = new { available = true },
        email = new { available = email.Configured, missing = email.MissingSettings() }
    });
}
