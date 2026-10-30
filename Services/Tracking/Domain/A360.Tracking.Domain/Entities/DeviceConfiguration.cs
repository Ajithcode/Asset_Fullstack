using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Device Configuration: one version (v1, v2, …) of the connectivity, telemetry and alert settings of one IoT device (e.g. TS-80214 v3).</summary>
/// <remarks>
/// Versions are numbered per device and never edited once applied. A device has at most one Draft, which Save Draft overwrites.
/// The lifecycle is kept in BaseEntity.Status and is one of <see cref="DeviceConfigurationStatus"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class DeviceConfiguration : BaseEntity
{
    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    /// <summary>e.g. Faster reporting for audit.</summary>
    [BsonElement("note")]
    public string? Note { get; set; }

    [BsonElement("connectivity")]
    public DeviceConnectivitySettings Connectivity { get; set; } = new();

    [BsonElement("telemetry")]
    public DeviceTelemetrySettings Telemetry { get; set; } = new();

    [BsonElement("alert_rules")]
    public List<DeviceAlertRule> AlertRules { get; set; } = [];

    /// <summary>One of <see cref="ConfigurationDeploymentMode.All"/>.</summary>
    [BsonElement("deployment_mode")]
    public string DeploymentMode { get; set; } = ConfigurationDeploymentMode.Immediate;

    /// <summary>When a Scheduled deployment is sent to the device.</summary>
    [BsonElement("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    /// <summary>One of <see cref="ConfigurationRolloutTarget.All"/>.</summary>
    [BsonElement("rollout_target")]
    public string RolloutTarget { get; set; } = ConfigurationRolloutTarget.Device;

    /// <summary>e.g. Cold Rooms; set when the rollout target is a device group.</summary>
    [BsonElement("device_group")]
    public string? DeviceGroup { get; set; }

    /// <summary>The version this draft was restored from, if any.</summary>
    [BsonElement("restored_from_version")]
    public int? RestoredFromVersion { get; set; }

    [BsonElement("applied_at")]
    public DateTime? AppliedAt { get; set; }

    [BsonElement("applied_by")]
    public string? AppliedBy { get; set; }

    /// <summary>When the device confirmed this version at check-in.</summary>
    [BsonElement("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>What happened to this version, oldest first, for the History → Activity list.</summary>
    [BsonElement("activity")]
    public List<DeviceConfigurationActivity> Activity { get; set; } = [];
}

public sealed class DeviceConnectivitySettings
{
    /// <summary>e.g. LoRaWAN, BLE.</summary>
    [BsonElement("protocol")]
    public string? Protocol { get; set; }

    /// <summary>LoRaWAN only, one of <see cref="LoRaWanRegion.All"/>.</summary>
    [BsonElement("region")]
    public string? Region { get; set; }

    /// <summary>LoRaWAN only, one of <see cref="LoRaWanJoinMode.All"/>.</summary>
    [BsonElement("join_mode")]
    public string? JoinMode { get; set; }

    [BsonElement("app_eui")]
    public string? AppEui { get; set; }

    [BsonElement("dev_eui")]
    public string? DevEui { get; set; }

    [BsonElement("reporting_interval")]
    public int ReportingInterval { get; set; }

    /// <summary>One of <see cref="ConfigurationIntervalUnit.All"/>.</summary>
    [BsonElement("reporting_interval_unit")]
    public string ReportingIntervalUnit { get; set; } = ConfigurationIntervalUnit.Minutes;

    /// <summary>Require acknowledgement from the network server.</summary>
    [BsonElement("confirmed_uplink")]
    public bool ConfirmedUplink { get; set; }

    [BsonElement("adaptive_data_rate")]
    public bool AdaptiveDataRate { get; set; }

    /// <summary>One of <see cref="LoRaWanDataRate.All"/>; Auto while ADR is on.</summary>
    [BsonElement("data_rate")]
    public string? DataRate { get; set; }
}

public sealed class DeviceTelemetrySettings
{
    /// <summary>One of <see cref="TelemetrySamplingInterval.Seconds"/>.</summary>
    [BsonElement("sampling_interval_seconds")]
    public int SamplingIntervalSeconds { get; set; }

    [BsonElement("battery_reporting")]
    public bool BatteryReporting { get; set; }

    /// <summary>One of <see cref="BatteryReportInterval.All"/>.</summary>
    [BsonElement("battery_report_interval")]
    public string? BatteryReportInterval { get; set; }

    [BsonElement("parameters")]
    public List<TelemetryParameter> Parameters { get; set; } = [];
}

/// <summary>A row of Telemetry Parameters, e.g. Temperature in °C, averaged, sent when it changes by 0.5 or more.</summary>
public sealed class TelemetryParameter
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="TelemetryParameterType.All"/>.</summary>
    [BsonElement("type")]
    public string Type { get; set; } = TelemetryParameterType.Measurement;

    [BsonElement("unit")]
    public string? Unit { get; set; }

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    /// <summary>One of <see cref="TelemetryAggregation.All"/>.</summary>
    [BsonElement("aggregation")]
    public string Aggregation { get; set; } = TelemetryAggregation.Average;

    /// <summary>Minimum change, in <see cref="Unit"/>, that triggers an uplink.</summary>
    [BsonElement("transmit_threshold")]
    public double? TransmitThreshold { get; set; }
}

/// <summary>A row of Threshold Alert Rules, e.g. Temperature &gt; 8 °C is Critical, notify by Email + SMS.</summary>
public sealed class DeviceAlertRule
{
    /// <summary>Stable within the device's versions, so a rule can be edited or deleted by id.</summary>
    [BsonElement("rule_id")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Name of a <see cref="TelemetryParameter"/>.</summary>
    [BsonElement("parameter")]
    public string Parameter { get; set; } = string.Empty;

    /// <summary>One of <see cref="DeviceAlertCondition.All"/>.</summary>
    [BsonElement("condition")]
    public string Condition { get; set; } = string.Empty;

    [BsonElement("value")]
    public double Value { get; set; }

    [BsonElement("unit")]
    public string? Unit { get; set; }

    /// <summary>One of <see cref="DeviceAlertSeverity.All"/>.</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Each one of <see cref="DeviceAlertChannel.All"/>.</summary>
    [BsonElement("notify_channels")]
    public List<string> NotifyChannels { get; set; } = [];

    [BsonElement("active")]
    public bool Active { get; set; }
}

public sealed class DeviceConfigurationActivity
{
    /// <summary>e.g. v3 applied to TS-80214, queued until next check-in.</summary>
    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("at")]
    public DateTime At { get; set; }

    /// <summary>The user, or "System" for device check-ins.</summary>
    [BsonElement("by")]
    public string? By { get; set; }
}

public static class DeviceConfigurationStatus
{
    public const string Draft = "Draft";
    /// <summary>Applied by a user, waiting for the device to confirm at its next check-in (or for the schedule).</summary>
    public const string Queued = "Queued";
    /// <summary>Confirmed by the device; at most one version per device is Applied.</summary>
    public const string Applied = "Applied";
    public const string Superseded = "Superseded";

    public static readonly IReadOnlyList<string> All = [Draft, Queued, Applied, Superseded];
}

public static class ConfigurationDeploymentMode
{
    /// <summary>Apply configuration at next device check-in.</summary>
    public const string Immediate = "Immediate";
    public const string Scheduled = "Scheduled";

    public static readonly IReadOnlyList<string> All = [Immediate, Scheduled];
}

public static class ConfigurationRolloutTarget
{
    public const string Device = "Device";
    public const string DeviceGroup = "Device group";

    public static readonly IReadOnlyList<string> All = [Device, DeviceGroup];
}

public static class LoRaWanRegion
{
    public static readonly IReadOnlyList<string> All =
    [
        "EU (EU868)", "US (US915)", "Australia (AU915)", "UAE (AS923)", "Asia (AS923)",
        "Korea (KR920)", "India (IN865)", "China (CN470)", "Russia (RU864)"
    ];
}

public static class LoRaWanJoinMode
{
    public const string Otaa = "OTAA";
    public const string Abp = "ABP";

    public static readonly IReadOnlyList<string> All = [Otaa, Abp];
}

public static class LoRaWanDataRate
{
    public const string Auto = "Auto (Network Optimized)";

    public static readonly IReadOnlyList<string> All = [Auto, "DR0", "DR1", "DR2", "DR3", "DR4", "DR5"];
}

public static class ConfigurationIntervalUnit
{
    public const string Seconds = "seconds";
    public const string Minutes = "minutes";
    public const string Hours = "hours";

    public static readonly IReadOnlyList<string> All = [Seconds, Minutes, Hours];
}

public static class TelemetrySamplingInterval
{
    public static readonly IReadOnlyList<int> Seconds = [15, 30, 60, 120, 300, 600, 900, 1800, 3600];
}

public static class BatteryReportInterval
{
    public const string Daily = "Daily";

    public static readonly IReadOnlyList<string> All = ["Hourly", "Every 6 hours", "Every 12 hours", Daily, "Weekly"];
}

public static class TelemetryParameterType
{
    public const string Measurement = "Measurement";
    public const string Diagnostic = "Diagnostic";

    public static readonly IReadOnlyList<string> All = [Measurement, Diagnostic];
}

public static class TelemetryAggregation
{
    public const string Average = "Average";
    public const string Last = "Last";

    public static readonly IReadOnlyList<string> All = [Average, "Min", "Max", Last, "Sum"];
}

public static class DeviceAlertCondition
{
    public static readonly IReadOnlyList<string> All = [">", "<", ">=", "<=", "="];
}

public static class DeviceAlertSeverity
{
    public const string Critical = "Critical";
    public const string Warning = "Warning";
    public const string Info = "Info";

    public static readonly IReadOnlyList<string> All = [Critical, Warning, Info];
}

public static class DeviceAlertChannel
{
    public static readonly IReadOnlyList<string> All = ["Email", "SMS", "Push", "In-app"];
}
