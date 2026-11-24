using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Contracts;

/// <summary>Save Draft: the settings of every Device Configuration tab.</summary>
/// <remarks>A section or field left out keeps its value from the current draft (or the latest version), so each tab can send only its own part.</remarks>
/// <param name="AlertRules">Replaces every rule when given; rules without a RuleId get a new one.</param>
/// <param name="DeploymentMode">One of <see cref="ConfigurationDeploymentMode.All"/>.</param>
/// <param name="ScheduledAt">Required when DeploymentMode is Scheduled.</param>
/// <param name="RolloutTarget">One of <see cref="ConfigurationRolloutTarget.All"/>.</param>
/// <param name="DeviceGroup">Required when RolloutTarget is a device group.</param>
public sealed record SaveDeviceConfigurationRequest(
    string? Note,
    ConnectivitySettingsRequest? Connectivity,
    TelemetrySettingsRequest? Telemetry,
    IReadOnlyList<DeviceAlertRuleRequest>? AlertRules,
    string? DeploymentMode,
    DateTime? ScheduledAt,
    string? RolloutTarget,
    string? DeviceGroup)
{
    public void ApplyTo(DeviceConfiguration draft)
    {
        if (Note is not null)
        {
            draft.Note = ContractText.TrimOrNull(Note);
        }

        Connectivity?.ApplyTo(draft.Connectivity);
        Telemetry?.ApplyTo(draft.Telemetry);

        if (AlertRules is not null)
        {
            draft.AlertRules = AlertRules.Select(rule => rule.ToEntity(rule.RuleId, draft.Telemetry)).ToList();
        }

        draft.DeploymentMode = ContractText.Canonical(DeploymentMode, ConfigurationDeploymentMode.All) ?? draft.DeploymentMode;
        draft.ScheduledAt = draft.DeploymentMode == ConfigurationDeploymentMode.Scheduled
            ? ScheduledAt?.ToUniversalTime() ?? draft.ScheduledAt
            : null;

        draft.RolloutTarget = ContractText.Canonical(RolloutTarget, ConfigurationRolloutTarget.All) ?? draft.RolloutTarget;
        draft.DeviceGroup = draft.RolloutTarget == ConfigurationRolloutTarget.DeviceGroup
            ? ContractText.TrimOrNull(DeviceGroup) ?? draft.DeviceGroup
            : null;
    }
}

/// <param name="Region">One of <see cref="LoRaWanRegion.All"/>.</param>
/// <param name="JoinMode">One of <see cref="LoRaWanJoinMode.All"/>.</param>
/// <param name="ReportingIntervalUnit">One of <see cref="ConfigurationIntervalUnit.All"/>.</param>
/// <param name="DataRate">One of <see cref="LoRaWanDataRate.All"/>; ignored while ADR is on.</param>
public sealed record ConnectivitySettingsRequest(
    string? Protocol,
    string? Region,
    string? JoinMode,
    string? AppEui,
    string? DevEui,
    int? ReportingInterval,
    string? ReportingIntervalUnit,
    bool? ConfirmedUplink,
    bool? AdaptiveDataRate,
    string? DataRate)
{
    public void ApplyTo(DeviceConnectivitySettings settings)
    {
        settings.Protocol = ContractText.TrimOrNull(Protocol) ?? settings.Protocol;
        settings.Region = ContractText.Canonical(Region, LoRaWanRegion.All) ?? settings.Region;
        settings.JoinMode = ContractText.Canonical(JoinMode, LoRaWanJoinMode.All) ?? settings.JoinMode;
        settings.AppEui = ContractText.TrimOrNull(AppEui)?.ToUpperInvariant() ?? settings.AppEui;
        settings.DevEui = ContractText.TrimOrNull(DevEui)?.ToUpperInvariant() ?? settings.DevEui;
        settings.ReportingInterval = ReportingInterval ?? settings.ReportingInterval;
        settings.ReportingIntervalUnit = ContractText.Canonical(ReportingIntervalUnit, ConfigurationIntervalUnit.All) ?? settings.ReportingIntervalUnit;
        settings.ConfirmedUplink = ConfirmedUplink ?? settings.ConfirmedUplink;
        settings.AdaptiveDataRate = AdaptiveDataRate ?? settings.AdaptiveDataRate;
        // With ADR on the network negotiates the data rate.
        settings.DataRate = settings.AdaptiveDataRate
            ? LoRaWanDataRate.Auto
            : ContractText.Canonical(DataRate, LoRaWanDataRate.All) ?? settings.DataRate;
    }
}

/// <param name="SamplingIntervalSeconds">One of <see cref="TelemetrySamplingInterval.Seconds"/>.</param>
/// <param name="BatteryReportInterval">One of <see cref="Domain.Entities.BatteryReportInterval.All"/>.</param>
/// <param name="Parameters">Replaces every parameter row when given.</param>
public sealed record TelemetrySettingsRequest(
    int? SamplingIntervalSeconds,
    bool? BatteryReporting,
    string? BatteryReportInterval,
    IReadOnlyList<TelemetryParameterRequest>? Parameters)
{
    public void ApplyTo(DeviceTelemetrySettings settings)
    {
        settings.SamplingIntervalSeconds = SamplingIntervalSeconds ?? settings.SamplingIntervalSeconds;
        settings.BatteryReporting = BatteryReporting ?? settings.BatteryReporting;
        settings.BatteryReportInterval = ContractText.Canonical(BatteryReportInterval, Domain.Entities.BatteryReportInterval.All)
            ?? settings.BatteryReportInterval;

        if (Parameters is not null)
        {
            settings.Parameters = Parameters.Select(parameter => parameter.ToEntity()).ToList();
        }
    }
}

/// <param name="Type">One of <see cref="TelemetryParameterType.All"/>; defaults to Measurement.</param>
/// <param name="Aggregation">One of <see cref="TelemetryAggregation.All"/>; defaults to Average.</param>
public sealed record TelemetryParameterRequest(
    string? Name,
    string? Type,
    string? Unit,
    bool? Enabled,
    string? Aggregation,
    double? TransmitThreshold)
{
    public TelemetryParameter ToEntity()
    {
        return new TelemetryParameter
        {
            Name = Name!.Trim(),
            Type = ContractText.Canonical(Type, TelemetryParameterType.All) ?? TelemetryParameterType.Measurement,
            Unit = ContractText.TrimOrNull(Unit),
            Enabled = Enabled ?? true,
            Aggregation = ContractText.Canonical(Aggregation, TelemetryAggregation.All) ?? TelemetryAggregation.Average,
            TransmitThreshold = TransmitThreshold
        };
    }
}

/// <summary>Add Rule / edit rule on the Alerts tab.</summary>
/// <param name="Parameter">Name of a telemetry parameter of the device.</param>
/// <param name="Condition">One of <see cref="DeviceAlertCondition.All"/>.</param>
/// <param name="Unit">Defaults to the parameter's unit.</param>
/// <param name="Severity">One of <see cref="DeviceAlertSeverity.All"/>.</param>
/// <param name="NotifyChannels">Each one of <see cref="DeviceAlertChannel.All"/>.</param>
/// <param name="Active">Defaults to true.</param>
public sealed record DeviceAlertRuleRequest(
    string? RuleId,
    string? Parameter,
    string? Condition,
    double? Value,
    string? Unit,
    string? Severity,
    IReadOnlyList<string>? NotifyChannels,
    bool? Active,
    bool? NotifySound = null)
{
    public DeviceAlertRule ToEntity(string? ruleId, DeviceTelemetrySettings telemetry)
    {
        var parameter = telemetry.Parameters.FirstOrDefault(row => string.Equals(row.Name, Parameter?.Trim(), StringComparison.OrdinalIgnoreCase));

        return new DeviceAlertRule
        {
            RuleId = ContractText.TrimOrNull(ruleId) ?? Guid.NewGuid().ToString("N"),
            Parameter = parameter?.Name ?? Parameter!.Trim(),
            Condition = Condition!.Trim(),
            Value = Value!.Value,
            Unit = ContractText.TrimOrNull(Unit) ?? parameter?.Unit,
            Severity = ContractText.Canonical(Severity, DeviceAlertSeverity.All)!,
            NotifyChannels = (NotifyChannels ?? [])
                .Select(channel => ContractText.Canonical(channel, DeviceAlertChannel.All))
                .OfType<string>()
                .Distinct()
                .ToList(),
            NotifySound = NotifySound ?? true,
            Active = Active ?? true
        };
    }
}

/// <summary>Turns a rule on or off from the Active toggle.</summary>
public sealed record SetDeviceAlertRuleActiveRequest(bool? Active);

/// <summary>Firmware → Schedule update.</summary>
/// <param name="TargetVersion">Defaults to the latest version of the device's model.</param>
public sealed record ScheduleFirmwareUpdateRequest(string? TargetVersion, DateTime? ScheduledAt);

public sealed record DeviceConfigurationResponse(
    string Id,
    string DeviceId,
    int Version,
    string? Status,
    string? Note,
    DeviceConnectivitySettings Connectivity,
    TelemetrySettingsResponse Telemetry,
    IReadOnlyList<DeviceAlertRule> AlertRules,
    string DeploymentMode,
    DateTime? ScheduledAt,
    string RolloutTarget,
    string? DeviceGroup,
    int? RestoredFromVersion,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? AppliedBy,
    DateTime? AppliedAt,
    DateTime? ConfirmedAt)
{
    public static DeviceConfigurationResponse FromEntity(DeviceConfiguration configuration)
    {
        return new DeviceConfigurationResponse(
            configuration.Id,
            configuration.DeviceId,
            configuration.Version,
            configuration.Status,
            configuration.Note,
            configuration.Connectivity,
            TelemetrySettingsResponse.FromEntity(configuration.Telemetry, configuration.AlertRules),
            configuration.AlertRules,
            configuration.DeploymentMode,
            configuration.ScheduledAt,
            configuration.RolloutTarget,
            configuration.DeviceGroup,
            configuration.RestoredFromVersion,
            configuration.CreatedBy,
            configuration.CreatedAt,
            configuration.UpdatedBy,
            configuration.UpdatedAt,
            configuration.AppliedBy,
            configuration.AppliedAt,
            configuration.ConfirmedAt);
    }
}

/// <param name="TransmittedCount">The "4 of 5 transmitted" count: enabled parameters.</param>
public sealed record TelemetrySettingsResponse(
    int SamplingIntervalSeconds,
    bool BatteryReporting,
    string? BatteryReportInterval,
    int TransmittedCount,
    IReadOnlyList<TelemetryParameterResponse> Parameters)
{
    public static TelemetrySettingsResponse FromEntity(DeviceTelemetrySettings telemetry, IReadOnlyList<DeviceAlertRule> alertRules)
    {
        return new TelemetrySettingsResponse(
            telemetry.SamplingIntervalSeconds,
            telemetry.BatteryReporting,
            telemetry.BatteryReportInterval,
            telemetry.Parameters.Count(parameter => parameter.Enabled),
            telemetry.Parameters
                .Select(parameter => new TelemetryParameterResponse(
                    parameter.Name,
                    parameter.Type,
                    parameter.Unit,
                    parameter.Enabled,
                    parameter.Aggregation,
                    parameter.TransmitThreshold,
                    alertRules.Count(rule => rule.Active && string.Equals(rule.Parameter, parameter.Name, StringComparison.OrdinalIgnoreCase))))
                .ToList());
    }
}

/// <param name="ActiveAlertRules">The Alert Rules column, e.g. "2 active".</param>
public sealed record TelemetryParameterResponse(
    string Name,
    string Type,
    string? Unit,
    bool Enabled,
    string Aggregation,
    double? TransmitThreshold,
    int ActiveAlertRules);

/// <summary>A row of History → Configuration Versions.</summary>
public sealed record DeviceConfigurationVersionResponse(
    int Version,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    DateTime? AppliedAt,
    DateTime? ConfirmedAt,
    string? Note)
{
    public static DeviceConfigurationVersionResponse FromEntity(DeviceConfiguration configuration)
    {
        return new DeviceConfigurationVersionResponse(
            configuration.Version,
            configuration.Status,
            configuration.CreatedBy,
            configuration.CreatedAt,
            configuration.AppliedAt,
            configuration.ConfirmedAt,
            configuration.Note);
    }
}

/// <summary>An entry of History → Activity.</summary>
public sealed record DeviceConfigurationActivityResponse(int Version, string Message, DateTime At, string? By);

/// <summary>A row of the Validation Checklist, e.g. Connectivity parameters complete.</summary>
public sealed record ConfigurationCheckResponse(string Key, string Title, bool Passed, string Detail);

/// <summary>Validate Configuration: whether the configuration can be applied, and each check.</summary>
public sealed record ConfigurationValidationResponse(bool Passed, int PassedCount, int TotalCount, IReadOnlyList<ConfigurationCheckResponse> Checks)
{
    public static ConfigurationValidationResponse FromChecks(IReadOnlyList<ConfigurationCheckResponse> checks)
    {
        var passedCount = checks.Count(check => check.Passed);
        return new ConfigurationValidationResponse(passedCount == checks.Count, passedCount, checks.Count, checks);
    }
}

/// <summary>
/// The page header and the Overview → Device Status card, joined from two sources:
/// the registered device (IoT Device Management: name, model, linked asset, configured gateway, firmware) and its latest
/// NetworkTimeSeries reading (online / offline, last seen, battery, RSSI, position, and the gateway that relayed it).
/// The reading is matched on meta.tagid = device id, or for a gateway on meta.gatewayid = device id; the relaying
/// gateway id is matched back to a registered gateway device for its name.
/// </summary>
/// <param name="Status">Online / Offline from the latest reading; without readings, the last stored health status.</param>
/// <param name="Gateway">The gateway that relayed the latest reading, else the configured one.</param>
/// <param name="ConfiguredGateway">The gateway set when the device was added.</param>
/// <param name="SnrDb">NetworkTimeSeries has no SNR, so this comes from the last stored health report.</param>
/// <param name="StatusSource">NetworkTimeSeries when a reading was found, otherwise Device record.</param>
public sealed record ConfigurationDeviceResponse(
    string DeviceId,
    string DeviceName,
    string DeviceType,
    string Model,
    string ModelDisplayName,
    string? Gateway,
    string? GatewayName,
    string? ConfiguredGateway,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? SiteName,
    string? Status,
    bool NeverReported,
    DateTime? LastSeenAt,
    int? BatteryLevel,
    bool LowBattery,
    string PowerSource,
    int? RssiDbm,
    string? SignalQuality,
    double? SnrDb,
    double? Latitude,
    double? Longitude,
    string? FirmwareVersion,
    string StatusSource)
{
    public const string FromReadings = "NetworkTimeSeries";
    public const string FromDeviceRecord = "Device record";

    /// <param name="model">The device's model, for "Milesight EM300-TH".</param>
    /// <param name="reading">The device's latest NetworkTimeSeries reading, if any.</param>
    /// <param name="gateway">The registered gateway device that relayed the reading (or the configured one), if found.</param>
    public static ConfigurationDeviceResponse From(
        IoTDevice device,
        DeviceModel? model,
        DeviceHealth? health,
        NetworkReading? reading,
        IoTDevice? gateway)
    {
        var battery = reading is not null ? reading.Battery : health?.BatteryLevel ?? device.BatteryLevel;
        var rssi = reading is not null ? reading.Rssi : health?.SignalStrengthDbm;

        return new ConfigurationDeviceResponse(
            device.DeviceId,
            device.DeviceName,
            device.DeviceType,
            device.Model,
            model is null || string.IsNullOrWhiteSpace(model.Manufacturer) ? device.Model : $"{model.Manufacturer} {model.ModelNumber}",
            gateway?.DeviceId ?? reading?.GatewayId ?? device.Gateway,
            gateway?.DeviceName,
            device.Gateway,
            device.AssetRegistrationId,
            device.AssetCode,
            device.AssetName,
            device.SiteName,
            reading is not null
                ? string.Equals(reading.Status, "online", StringComparison.OrdinalIgnoreCase) ? DeviceConnection.Online : DeviceConnection.Offline
                : health?.Connectivity ?? DeviceConnection.Offline,
            reading is null,
            reading?.Timestamp ?? health?.LastSeenAt ?? device.LastSeenAt,
            battery,
            battery < DeviceConnection.LowBatteryPercent,
            device.PowerSource,
            rssi,
            rssi switch { null => null, >= -55 => "Excellent", >= -70 => "Good", >= -85 => "Fair", _ => "Poor" },
            health?.SnrDb,
            reading?.Latitude,
            reading?.Longitude,
            device.FirmwareVersion,
            reading is null ? FromDeviceRecord : FromReadings);
    }
}

/// <summary>The Firmware tab and the Overview → Firmware card.</summary>
/// <param name="MinimumVersion">"Config schema needs v1.4.0 or later": the model's lowest compatible firmware.</param>
/// <param name="IsCompatible">Null when the installed or minimum version is unknown.</param>
public sealed record DeviceFirmwareResponse(
    string Model,
    string? InstalledVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    DateTime? LatestReleaseDate,
    IReadOnlyList<string> ReleaseNotes,
    string? MinimumVersion,
    bool? IsCompatible,
    string? PendingVersion,
    DateTime? UpdateScheduledAt);

/// <param name="AlertCounts">Active rules per severity, e.g. 1 Critical, 2 Warning, 1 Info.</param>
public sealed record DeviceAlertCountsResponse(int Critical, int Warning, int Info)
{
    public static DeviceAlertCountsResponse FromRules(IEnumerable<DeviceAlertRule> rules)
    {
        var active = rules.Where(rule => rule.Active).ToList();
        return new DeviceAlertCountsResponse(
            active.Count(rule => rule.Severity == DeviceAlertSeverity.Critical),
            active.Count(rule => rule.Severity == DeviceAlertSeverity.Warning),
            active.Count(rule => rule.Severity == DeviceAlertSeverity.Info));
    }
}

/// <summary>Everything the Device Configuration page shows for one device.</summary>
/// <param name="Current">The latest applied or queued version (the "Configuration Version" card), or null before the first apply.</param>
/// <param name="RunningVersion">The version the device confirmed at check-in ("Running on device").</param>
/// <param name="Draft">The unsaved-to-device draft, if any.</param>
/// <param name="Editable">What the tabs should show: the draft, else the current version, else defaults built from the device model.</param>
/// <param name="AlertCounts">Counted from <paramref name="Editable"/>.</param>
/// <param name="Validation">Checks of <paramref name="Editable"/>.</param>
public sealed record DeviceConfigurationOverviewResponse(
    ConfigurationDeviceResponse Device,
    DeviceConfigurationResponse? Current,
    int? RunningVersion,
    DeviceConfigurationResponse? Draft,
    DeviceConfigurationResponse Editable,
    DeviceAlertCountsResponse AlertCounts,
    DeviceFirmwareResponse Firmware,
    ConfigurationValidationResponse Validation);

/// <summary>Every dropdown and option list of the Device Configuration page.</summary>
public sealed record DeviceConfigurationOptionsResponse(
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> JoinModes,
    IReadOnlyList<string> IntervalUnits,
    IReadOnlyList<string> DataRates,
    IReadOnlyList<int> SamplingIntervalsSeconds,
    IReadOnlyList<string> BatteryReportIntervals,
    IReadOnlyList<string> ParameterTypes,
    IReadOnlyList<string> Aggregations,
    IReadOnlyList<string> AlertConditions,
    IReadOnlyList<string> AlertSeverities,
    IReadOnlyList<string> NotifyChannels,
    IReadOnlyList<string> DeploymentModes,
    IReadOnlyList<string> RolloutTargets,
    IReadOnlyList<string> Statuses)
{
    public static readonly DeviceConfigurationOptionsResponse Instance = new(
        LoRaWanRegion.All,
        LoRaWanJoinMode.All,
        ConfigurationIntervalUnit.All,
        LoRaWanDataRate.All,
        TelemetrySamplingInterval.Seconds,
        BatteryReportInterval.All,
        TelemetryParameterType.All,
        TelemetryAggregation.All,
        DeviceAlertCondition.All,
        DeviceAlertSeverity.All,
        DeviceAlertChannel.All,
        ConfigurationDeploymentMode.All,
        ConfigurationRolloutTarget.All,
        DeviceConfigurationStatus.All);
}

/// <summary>Firmware versions such as "v1.6.2" or "1.4.0", compared numerically.</summary>
internal static class FirmwareVersionText
{
    public static Version? Parse(string? text)
    {
        var trimmed = ContractText.TrimOrNull(text)?.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    /// <summary>Whether installed is the minimum or later; null when either is unknown.</summary>
    /// <remarks>No upper bound: newer firmware keeps accepting older configuration schemas.</remarks>
    public static bool? IsAtLeast(string? installed, string? minimum)
    {
        var installedVersion = Parse(installed);
        var minimumVersion = Parse(minimum);
        return installedVersion is null || minimumVersion is null ? null : installedVersion >= minimumVersion;
    }
}
