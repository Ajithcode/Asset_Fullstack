using System.Text.RegularExpressions;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static partial class DeviceConfigurationValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int NoteMaxLength = 200;
    private const int MaxParameters = 50;
    private const int MaxAlertRules = 100;
    private const int MaxReportingInterval = 10_000;
    private const string LoRaWan = "LoRaWAN";

    /// <summary>Checks the request on its own; <see cref="ValidateMerged"/> checks the draft it produces.</summary>
    public static Dictionary<string, string[]> Validate(this SaveDeviceConfigurationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        MaxLength(errors, "Note", request.Note, NoteMaxLength);

        if (request.Connectivity is { } connectivity)
        {
            OneOf(errors, "Connectivity.Region", connectivity.Region, LoRaWanRegion.All);
            OneOf(errors, "Connectivity.JoinMode", connectivity.JoinMode, LoRaWanJoinMode.All);
            OneOf(errors, "Connectivity.ReportingIntervalUnit", connectivity.ReportingIntervalUnit, ConfigurationIntervalUnit.All);
            OneOf(errors, "Connectivity.DataRate", connectivity.DataRate, LoRaWanDataRate.All);
            MaxLength(errors, "Connectivity.Protocol", connectivity.Protocol, NameMaxLength);
            Eui(errors, "Connectivity.AppEui", connectivity.AppEui);
            Eui(errors, "Connectivity.DevEui", connectivity.DevEui);

            if (connectivity.ReportingInterval is < 1 or > MaxReportingInterval)
            {
                errors["Connectivity.ReportingInterval"] = [$"ReportingInterval must be between 1 and {MaxReportingInterval}"];
            }
        }

        if (request.Telemetry is { } telemetry)
        {
            if (telemetry.SamplingIntervalSeconds is { } seconds && !TelemetrySamplingInterval.Seconds.Contains(seconds))
            {
                errors["Telemetry.SamplingIntervalSeconds"] = [$"SamplingIntervalSeconds must be one of: {string.Join(", ", TelemetrySamplingInterval.Seconds)}"];
            }

            OneOf(errors, "Telemetry.BatteryReportInterval", telemetry.BatteryReportInterval, BatteryReportInterval.All);
            ValidateParameters(errors, telemetry.Parameters);
        }

        if (request.AlertRules is { } rules)
        {
            if (rules.Count > MaxAlertRules)
            {
                errors["AlertRules"] = [$"At most {MaxAlertRules} alert rules are allowed"];
            }

            for (var index = 0; index < rules.Count; index++)
            {
                foreach (var (field, messages) in rules[index].Validate())
                {
                    errors[$"AlertRules[{index}].{field}"] = messages;
                }
            }
        }

        OneOf(errors, "DeploymentMode", request.DeploymentMode, ConfigurationDeploymentMode.All);
        OneOf(errors, "RolloutTarget", request.RolloutTarget, ConfigurationRolloutTarget.All);
        MaxLength(errors, "DeviceGroup", request.DeviceGroup, NameMaxLength);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this DeviceAlertRuleRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Parameter))
        {
            errors["Parameter"] = ["Parameter is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Condition))
        {
            errors["Condition"] = ["Condition is required"];
        }
        else if (!DeviceAlertCondition.All.Contains(request.Condition.Trim()))
        {
            errors["Condition"] = [$"Condition must be one of: {string.Join(", ", DeviceAlertCondition.All)}"];
        }

        if (request.Value is null)
        {
            errors["Value"] = ["Value is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Severity))
        {
            errors["Severity"] = ["Severity is required"];
        }
        else
        {
            OneOf(errors, "Severity", request.Severity, DeviceAlertSeverity.All);
        }

        if (request.NotifyChannels?.Any(channel => ContractText.Canonical(channel, DeviceAlertChannel.All) is null) == true)
        {
            errors["NotifyChannels"] = [$"Each notify channel must be one of: {string.Join(", ", DeviceAlertChannel.All)}"];
        }

        MaxLength(errors, "Unit", request.Unit, NameMaxLength);

        return errors;
    }

    /// <summary>Rules that only hold once the request is merged into the draft, since either side may supply each part.</summary>
    public static Dictionary<string, string[]> ValidateMerged(this DeviceConfiguration draft)
    {
        var errors = new Dictionary<string, string[]>();

        var unknownParameters = draft.AlertRules
            .Where(rule => !draft.Telemetry.Parameters.Any(parameter => string.Equals(parameter.Name, rule.Parameter, StringComparison.OrdinalIgnoreCase)))
            .Select(rule => rule.Parameter)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unknownParameters.Count > 0)
        {
            errors["AlertRules"] = [$"Alert rules refer to parameters the device does not have: {string.Join(", ", unknownParameters)}"];
        }

        if (draft.DeploymentMode == ConfigurationDeploymentMode.Scheduled && draft.ScheduledAt is null)
        {
            errors["ScheduledAt"] = ["ScheduledAt is required when DeploymentMode is Scheduled"];
        }

        if (draft.RolloutTarget == ConfigurationRolloutTarget.DeviceGroup && draft.DeviceGroup is null)
        {
            errors["DeviceGroup"] = ["DeviceGroup is required when RolloutTarget is Device group"];
        }

        return errors;
    }

    /// <summary>The Validation Checklist; every check must pass before a configuration is applied.</summary>
    /// <param name="minimumFirmware">The lowest firmware the model's configuration schema supports, if known.</param>
    public static IReadOnlyList<ConfigurationCheckResponse> RunChecks(this DeviceConfiguration configuration, string? installedFirmware, string? minimumFirmware)
    {
        return
        [
            CheckConnectivity(configuration.Connectivity),
            CheckTelemetry(configuration.Telemetry),
            CheckAlertRules(configuration),
            CheckFirmware(installedFirmware, minimumFirmware)
        ];
    }

    private static ConfigurationCheckResponse CheckConnectivity(DeviceConnectivitySettings connectivity)
    {
        const string Title = "Connectivity parameters complete";
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(connectivity.Protocol))
        {
            missing.Add("protocol");
        }

        if (connectivity.ReportingInterval < 1)
        {
            missing.Add("reporting interval");
        }

        var isLoRaWan = connectivity.Protocol?.Contains(LoRaWan, StringComparison.OrdinalIgnoreCase) == true;
        if (isLoRaWan)
        {
            if (connectivity.Region is null) missing.Add("region");
            if (connectivity.JoinMode is null) missing.Add("join mode");
            if (connectivity.DevEui is null) missing.Add("DevEUI");
            // OTAA joins with the AppEUI; ABP devices are provisioned with session keys instead.
            if (connectivity.JoinMode == LoRaWanJoinMode.Otaa && connectivity.AppEui is null) missing.Add("AppEUI");
        }

        return missing.Count == 0
            ? new ConfigurationCheckResponse("connectivity", Title, true, isLoRaWan ? "Region, join mode and keys configured." : "Protocol and reporting interval configured.")
            : new ConfigurationCheckResponse("connectivity", Title, false, $"Missing {string.Join(", ", missing)}.");
    }

    private static ConfigurationCheckResponse CheckTelemetry(DeviceTelemetrySettings telemetry)
    {
        const string Title = "Telemetry mapping complete";
        var enabled = telemetry.Parameters.Where(parameter => parameter.Enabled).ToList();

        if (enabled.Count == 0)
        {
            return new ConfigurationCheckResponse("telemetry", Title, false, "Enable at least one parameter.");
        }

        if (telemetry.SamplingIntervalSeconds < 1)
        {
            return new ConfigurationCheckResponse("telemetry", Title, false, "Choose a sampling interval.");
        }

        var incomplete = enabled
            .Where(parameter => parameter.Unit is null || parameter.TransmitThreshold is null)
            .Select(parameter => parameter.Name)
            .ToList();

        return incomplete.Count == 0
            ? new ConfigurationCheckResponse("telemetry", Title, true, "All enabled parameters mapped.")
            : new ConfigurationCheckResponse("telemetry", Title, false, $"Set a unit and transmit threshold for {string.Join(", ", incomplete)}.");
    }

    private static ConfigurationCheckResponse CheckAlertRules(DeviceConfiguration configuration)
    {
        const string Title = "Alert rules linked";
        var active = configuration.AlertRules.Where(rule => rule.Active).ToList();

        if (active.Count == 0)
        {
            return new ConfigurationCheckResponse("alertRules", Title, false, "Add at least one active threshold rule.");
        }

        // A rule on a parameter that is not transmitted can never fire.
        var unlinked = active
            .Where(rule => !configuration.Telemetry.Parameters.Any(parameter =>
                parameter.Enabled && string.Equals(parameter.Name, rule.Parameter, StringComparison.OrdinalIgnoreCase)))
            .Select(rule => rule.Parameter)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return unlinked.Count == 0
            ? new ConfigurationCheckResponse("alertRules", Title, true, "Threshold rules configured.")
            : new ConfigurationCheckResponse("alertRules", Title, false, $"Rules use parameters that are not transmitted: {string.Join(", ", unlinked)}.");
    }

    private static ConfigurationCheckResponse CheckFirmware(string? installedFirmware, string? minimumFirmware)
    {
        const string Title = "Firmware compatibility verified";

        if (FirmwareVersionText.Parse(installedFirmware) is null)
        {
            return new ConfigurationCheckResponse("firmware", Title, false, "The installed firmware version is unknown.");
        }

        return FirmwareVersionText.IsAtLeast(installedFirmware, minimumFirmware) switch
        {
            true => new ConfigurationCheckResponse("firmware", Title, true, $"{installedFirmware} is compatible."),
            false => new ConfigurationCheckResponse("firmware", Title, false, $"{installedFirmware} is older than the required {minimumFirmware}."),
            null => new ConfigurationCheckResponse("firmware", Title, true, $"{installedFirmware} installed; the model sets no minimum firmware.")
        };
    }

    private static void ValidateParameters(Dictionary<string, string[]> errors, IReadOnlyList<TelemetryParameterRequest>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        if (parameters.Count > MaxParameters)
        {
            errors["Telemetry.Parameters"] = [$"At most {MaxParameters} parameters are allowed"];
            return;
        }

        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var prefix = $"Telemetry.Parameters[{index}]";

            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                errors[$"{prefix}.Name"] = ["Name is required"];
            }
            else
            {
                MaxLength(errors, $"{prefix}.Name", parameter.Name, NameMaxLength);
            }

            OneOf(errors, $"{prefix}.Type", parameter.Type, TelemetryParameterType.All);
            OneOf(errors, $"{prefix}.Aggregation", parameter.Aggregation, TelemetryAggregation.All);
            MaxLength(errors, $"{prefix}.Unit", parameter.Unit, NameMaxLength);

            if (parameter.TransmitThreshold < 0)
            {
                errors[$"{prefix}.TransmitThreshold"] = ["TransmitThreshold cannot be negative"];
            }
        }

        var duplicates = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .GroupBy(parameter => parameter.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            errors["Telemetry.Parameters"] = [$"Parameter names must be unique: {string.Join(", ", duplicates)}"];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void Eui(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !EuiPattern().IsMatch(value.Trim()))
        {
            errors[field] = [$"{field} must be 16 hexadecimal characters"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }

    // A LoRaWAN EUI-64, e.g. 24E124C0002A1B3C.
    [GeneratedRegex("^[0-9A-Fa-f]{16}$")]
    private static partial Regex EuiPattern();
}
