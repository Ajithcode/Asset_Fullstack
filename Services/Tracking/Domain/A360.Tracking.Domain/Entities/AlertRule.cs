using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Geofence & Alerts → Alert Rule Builder: what happens when assets enter, exit or remain in a geofence.</summary>
/// <remarks>Rule names are unique within a geofence. Status is kept in BaseEntity.Status and is one of <see cref="AlertRuleStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class AlertRule : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("geofence_id")]
    public string GeofenceId { get; set; } = string.Empty;

    [BsonElement("geofence_name")]
    public string GeofenceName { get; set; } = string.Empty;

    /// <summary>One of <see cref="AlertRuleTrigger.All"/>.</summary>
    [BsonElement("trigger")]
    public string Trigger { get; set; } = string.Empty;

    /// <summary>One of <see cref="AlertRuleSeverity.All"/>.</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>e.g. Forklifts.</summary>
    [BsonElement("asset_group")]
    public string AssetGroup { get; set; } = string.Empty;

    /// <summary>One of <see cref="AlertRuleCondition.All"/>.</summary>
    [BsonElement("condition")]
    public string Condition { get; set; } = string.Empty;

    /// <summary>Only used by the "remains in zone" trigger.</summary>
    [BsonElement("dwell_minutes")]
    public int? DwellMinutes { get; set; }

    [BsonElement("create_alert")]
    public bool CreateAlert { get; set; }

    [BsonElement("notify_team")]
    public bool NotifyTeam { get; set; }

    /// <summary>e.g. Security Team; set when <see cref="NotifyTeam"/> is on.</summary>
    [BsonElement("notify_team_name")]
    public string? NotifyTeamName { get; set; }

    [BsonElement("start_workflow")]
    public bool StartWorkflow { get; set; }

    /// <summary>e.g. Restricted Zone Incident; set when <see cref="StartWorkflow"/> is on.</summary>
    [BsonElement("workflow_name")]
    public string? WorkflowName { get; set; }

    /// <summary>Each one of <see cref="AlertNotificationChannel.All"/>.</summary>
    [BsonElement("notification_channels")]
    public List<string> NotificationChannels { get; set; } = [];
}

public static class AlertRuleTrigger
{
    public const string Enters = "When asset enters zone";
    public const string Exits = "When asset exits zone";
    public const string EntersOrExits = "When asset enters or exits zone";
    public const string RemainsIn = "When asset remains in zone";

    public static readonly IReadOnlyList<string> All = [Enters, Exits, EntersOrExits, RemainsIn];
}

public static class AlertRuleSeverity
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> All = [Low, Medium, High, Critical];
}

public static class AlertRuleCondition
{
    public const string AllHours = "During all hours";
    public const string WorkingHours = "During working hours";
    public const string OutsideWorkingHours = "Outside working hours";

    public static readonly IReadOnlyList<string> All = [AllHours, WorkingHours, OutsideWorkingHours];
}

public static class AlertNotificationChannel
{
    public const string InApp = "In-app";
    public const string Email = "Email";
    public const string Push = "Push";

    public static readonly IReadOnlyList<string> All = [InApp, Email, Push];
}

public static class AlertRuleStatus
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";

    public static readonly IReadOnlyList<string> All = [Active, Disabled];
}
