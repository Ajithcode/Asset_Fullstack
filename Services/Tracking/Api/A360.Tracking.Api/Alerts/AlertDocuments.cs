using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace A360.Tracking.Api.Alerts;

// Collections owned by the alert engine / desktop notifications (Tracking service).

/// <summary>A browser that receives desktop notifications (Web Push subscription), per signed-in user.</summary>
[BsonIgnoreExtraElements]
public sealed class PushSubscriptionDocument
{
    public const string CollectionName = "PushSubscription";

    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [BsonElement("p256dh")]
    public string P256dh { get; set; } = string.Empty;

    [BsonElement("auth")]
    public string Auth { get; set; } = string.Empty;

    [BsonElement("user_name")]
    public string? UserName { get; set; }

    [BsonElement("client_id")]
    public string? ClientId { get; set; }

    [BsonElement("tenant_id")]
    public string? TenantId { get; set; }

    /// <summary>Tracking API base the service worker calls back for Acknowledge / Remind me (e.g. http://localhost:5490/).</summary>
    [BsonElement("api_url")]
    public string? ApiUrl { get; set; }

    /// <summary>Frontend origin opened when the notification is clicked.</summary>
    [BsonElement("app_url")]
    public string? AppUrl { get; set; }

    [BsonElement("user_agent")]
    public string? UserAgent { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Desktop notification schedule of one alert: when to send next and how many times.</summary>
[BsonIgnoreExtraElements]
public sealed class AlertNotificationDocument
{
    public const string CollectionName = "AlertNotification";

    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("alert_id")]
    public string AlertId { get; set; } = string.Empty;

    [BsonElement("alert_rule_id")]
    public string? AlertRuleId { get; set; }

    [BsonElement("client_id")]
    public string? ClientId { get; set; }

    [BsonElement("tenant_id")]
    public string? TenantId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string? Severity { get; set; }

    [BsonElement("sound")]
    public bool Sound { get; set; } = true;

    [BsonElement("interval_seconds")]
    public int IntervalSeconds { get; set; }

    /// <summary>Notifications to send; 0 = until the alert is acknowledged.</summary>
    [BsonElement("repeat")]
    public int Repeat { get; set; }

    [BsonElement("sent_count")]
    public int SentCount { get; set; }

    [BsonElement("next_at")]
    public DateTime? NextAt { get; set; }

    [BsonElement("done")]
    public bool Done { get; set; }

    /// <summary>Secret the notification's Acknowledge / Remind me buttons send back (the service worker has no sign-in token).</summary>
    [BsonElement("action_token")]
    public string ActionToken { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An asset currently inside a watched floor plan zone (heard by a gateway plotted in it).</summary>
[BsonIgnoreExtraElements]
public sealed class ZonePresenceDocument
{
    public const string CollectionName = "ZonePresence";

    /// <summary>"{zoneId}|{assetId}"</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("zone_id")]
    public string ZoneId { get; set; } = string.Empty;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("gateway_id")]
    public string? GatewayId { get; set; }

    [BsonElement("entered_at")]
    public DateTime EnteredAt { get; set; }

    [BsonElement("last_seen_at")]
    public DateTime LastSeenAt { get; set; }

    /// <summary>Dwell rules already alerted for this stay.</summary>
    [BsonElement("dwell_alerted_rule_ids")]
    public List<string> DwellAlertedRuleIds { get; set; } = [];

    /// <summary>Entry rules already alerted (or skipped) for this stay; an entry waits for the rule's debounce.</summary>
    [BsonElement("entry_alerted_rule_ids")]
    public List<string> EntryAlertedRuleIds { get; set; } = [];

    /// <summary>Exit / location-lost rules already handled since the asset went missing.</summary>
    [BsonElement("exit_alerted_rule_ids")]
    public List<string> ExitAlertedRuleIds { get; set; } = [];

    /// <summary>When no zone gateway heard the asset any more; cleared if it comes back within the debounce.</summary>
    [BsonElement("gone_since")]
    public DateTime? GoneSince { get; set; }

    /// <summary>Already inside when the zone started being watched: no entry alert for this stay.</summary>
    [BsonElement("initial")]
    public bool Initial { get; set; }

    [BsonElement("registration_id")]
    public string? RegistrationId { get; set; }

    /// <summary>Asset category / sub-category / type, for the rules' asset-group scope after the asset left.</summary>
    [BsonElement("asset_groups")]
    public List<string> AssetGroups { get; set; } = [];
}

/// <summary>Zones the engine has started watching; the first look only records who is already inside (no alerts).</summary>
[BsonIgnoreExtraElements]
public sealed class WatchedZoneDocument
{
    public const string CollectionName = "ZoneWatch";

    [BsonId]
    public string ZoneId { get; set; } = string.Empty;

    [BsonElement("started_at")]
    public DateTime StartedAt { get; set; }
}

/// <summary>Web Push (VAPID) key pair, generated once and kept in the database so subscriptions stay valid.</summary>
[BsonIgnoreExtraElements]
public sealed class PushKeysDocument
{
    public const string CollectionName = "PushSettings";
    public const string SingletonId = "vapid";

    [BsonId]
    public string Id { get; set; } = SingletonId;

    [BsonElement("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    [BsonElement("private_key")]
    public string PrivateKey { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
