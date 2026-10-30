using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Configuration: versioned connectivity, telemetry and alert settings of an IoT device, addressed by device id (e.g. TS-80214).</summary>
/// <remarks>
/// Edits go into the device's single Draft (Save Draft, alert rule actions, Restore). Apply turns the draft into a Queued version,
/// and the device confirming it at check-in makes it Applied and supersedes the previous one.
/// </remarks>
public static class DeviceConfigurationEndpoints
{
    private const string EntityType = "DeviceConfiguration";
    private const string SystemUser = "System";

    public static RouteGroupBuilder MapDeviceConfigurationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-configurations").WithTags("Device Configurations");

        // Literal segments take precedence over /{deviceId}, so this is not read as a device id.
        group.MapGet("/options", () => Results.Ok(DeviceConfigurationOptionsResponse.Instance)).WithName("GetDeviceConfigurationOptions");

        group.MapGet("/{deviceId}", GetOverviewAsync).WithName("GetDeviceConfiguration");

        group.MapPut("/{deviceId}/draft", SaveDraftAsync).WithName("SaveDeviceConfigurationDraft");
        group.MapDelete("/{deviceId}/draft", DiscardDraftAsync).WithName("DiscardDeviceConfigurationDraft");
        group.MapPost("/{deviceId}/validate", ValidateAsync).WithName("ValidateDeviceConfiguration");
        group.MapPost("/{deviceId}/apply", ApplyAsync).WithName("ApplyDeviceConfiguration");

        group.MapPost("/{deviceId}/draft/alert-rules", AddAlertRuleAsync).WithName("AddDeviceAlertRule");
        group.MapPut("/{deviceId}/draft/alert-rules/{ruleId}", UpdateAlertRuleAsync).WithName("UpdateDeviceAlertRule");
        group.MapPatch("/{deviceId}/draft/alert-rules/{ruleId}/active", SetAlertRuleActiveAsync).WithName("SetDeviceAlertRuleActive");
        group.MapDelete("/{deviceId}/draft/alert-rules/{ruleId}", DeleteAlertRuleAsync).WithName("DeleteDeviceAlertRule");

        group.MapGet("/{deviceId}/versions", GetVersionsAsync).WithName("GetDeviceConfigurationVersions");
        group.MapGet("/{deviceId}/versions/{version:int}", GetVersionAsync).WithName("GetDeviceConfigurationVersion");
        group.MapPost("/{deviceId}/versions/{version:int}/restore", RestoreVersionAsync).WithName("RestoreDeviceConfigurationVersion");
        group.MapPost("/{deviceId}/versions/{version:int}/confirm", ConfirmVersionAsync).WithName("ConfirmDeviceConfigurationVersion");
        group.MapGet("/{deviceId}/activity", GetActivityAsync).WithName("GetDeviceConfigurationActivity");

        group.MapGet("/{deviceId}/firmware", GetFirmwareAsync).WithName("GetDeviceFirmware");
        group.MapPut("/{deviceId}/firmware/schedule", ScheduleFirmwareUpdateAsync).WithName("ScheduleDeviceFirmwareUpdate");
        group.MapDelete("/{deviceId}/firmware/schedule", CancelFirmwareUpdateAsync).WithName("CancelDeviceFirmwareUpdate");

        return group;
    }

    private static async Task<IResult> GetOverviewAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var health = await repositories.Health.GetByDeviceIdAsync(context.Device.DeviceId, cancellationToken);
        var editable = context.Draft ?? context.Current ?? context.CreateDefaults();

        return Results.Ok(new DeviceConfigurationOverviewResponse(
            ConfigurationDeviceResponse.FromEntity(context.Device, health),
            context.Current is null ? null : DeviceConfigurationResponse.FromEntity(context.Current),
            context.Running?.Version,
            context.Draft is null ? null : DeviceConfigurationResponse.FromEntity(context.Draft),
            DeviceConfigurationResponse.FromEntity(editable),
            DeviceAlertCountsResponse.FromRules(editable.AlertRules),
            context.ToFirmwareResponse(),
            context.Validate(editable)));
    }

    private static async Task<IResult> SaveDraftAsync(
        string deviceId,
        SaveDeviceConfigurationRequest request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var draft = context.EnsureDraft(repositories.CurrentUser);
        request.ApplyTo(draft);

        var mergedErrors = draft.ValidateMerged();
        if (mergedErrors.Count > 0)
        {
            return Results.ValidationProblem(mergedErrors);
        }

        return await PersistDraftAsync(context, draft, repositories, cancellationToken)
            ?? Results.Ok(DeviceConfigurationResponse.FromEntity(draft));
    }

    private static async Task<IResult> DiscardDraftAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        if (context.Draft is null)
        {
            return Results.NotFound(new { message = $"Device '{context.Device.DeviceId}' has no draft configuration." });
        }

        // Removed outright rather than soft-deleted so its version number can be used by the next draft.
        await repositories.Configurations.DeleteAsync(context.Draft.Id, cancellationToken);
        await repositories.EventLogger.LogAsync(EntityType, context.Draft.Id, VersionName(context.Draft), EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    /// <summary>Validate Configuration: checks the draft (or current version) with any unsaved changes from the body, without saving.</summary>
    private static async Task<IResult> ValidateAsync(
        string deviceId,
        SaveDeviceConfigurationRequest? request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        if (request?.Validate() is { Count: > 0 } validationErrors)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var candidate = Clone(context.Draft ?? context.Current ?? context.CreateDefaults());
        request?.ApplyTo(candidate);

        var mergedErrors = candidate.ValidateMerged();
        return mergedErrors.Count > 0
            ? Results.ValidationProblem(mergedErrors)
            : Results.Ok(context.Validate(candidate));
    }

    /// <summary>Apply Configuration: saves any changes from the body into the draft, then queues it for the device.</summary>
    private static async Task<IResult> ApplyAsync(
        string deviceId,
        SaveDeviceConfigurationRequest? request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        if (request?.Validate() is { Count: > 0 } validationErrors)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        if (context.Draft is null && request is null)
        {
            return Results.Conflict(new { message = $"Device '{context.Device.DeviceId}' has no draft configuration to apply." });
        }

        var draft = context.EnsureDraft(repositories.CurrentUser);
        request?.ApplyTo(draft);

        var mergedErrors = draft.ValidateMerged();
        if (mergedErrors.Count > 0)
        {
            return Results.ValidationProblem(mergedErrors);
        }

        if (draft.DeploymentMode == ConfigurationDeploymentMode.Scheduled && draft.ScheduledAt <= DateTime.UtcNow)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ScheduledAt"] = ["ScheduledAt must be in the future"] });
        }

        var validation = context.Validate(draft);
        if (!validation.Passed)
        {
            return Results.UnprocessableEntity(validation);
        }

        var userName = repositories.CurrentUser?.UserName;
        var now = DateTime.UtcNow;

        draft.Status = DeviceConfigurationStatus.Queued;
        draft.AppliedAt = now;
        draft.AppliedBy = userName;
        draft.Activity.Add(new DeviceConfigurationActivity { Message = AppliedMessage(draft), At = now, By = userName });

        var saveError = await PersistDraftAsync(context, draft, repositories, cancellationToken);
        if (saveError is not null)
        {
            return saveError;
        }

        // A version the device never confirmed is replaced by the newer one.
        foreach (var queued in context.Versions.Where(version => version.Status == DeviceConfigurationStatus.Queued && version.Id != draft.Id))
        {
            Supersede(queued, draft, now, userName);
            await repositories.Configurations.UpdateAsync(queued.Id, queued, cancellationToken);
        }

        await repositories.EventLogger.LogAsync(EntityType, draft.Id, VersionName(draft), EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceConfigurationResponse.FromEntity(draft));
    }

    private static async Task<IResult> AddAlertRuleAsync(
        string deviceId,
        DeviceAlertRuleRequest request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        return await EditAlertRulesAsync(deviceId, request, repositories, cancellationToken, draft =>
        {
            var rule = request.ToEntity(null, draft.Telemetry);
            draft.AlertRules.Add(rule);
            return rule;
        }, created: true);
    }

    private static async Task<IResult> UpdateAlertRuleAsync(
        string deviceId,
        string ruleId,
        DeviceAlertRuleRequest request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        return await EditAlertRulesAsync(deviceId, request, repositories, cancellationToken, draft =>
        {
            var index = draft.AlertRules.FindIndex(rule => rule.RuleId == ruleId);
            if (index < 0)
            {
                return null;
            }

            draft.AlertRules[index] = request.ToEntity(ruleId, draft.Telemetry);
            return draft.AlertRules[index];
        });
    }

    private static async Task<IResult> SetAlertRuleActiveAsync(
        string deviceId,
        string ruleId,
        SetDeviceAlertRuleActiveRequest request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        if (request.Active is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Active"] = ["Active is required"] });
        }

        return await EditAlertRulesAsync(deviceId, null, repositories, cancellationToken, draft =>
        {
            var rule = draft.AlertRules.FirstOrDefault(existing => existing.RuleId == ruleId);
            if (rule is not null)
            {
                rule.Active = request.Active.Value;
            }

            return rule;
        });
    }

    private static async Task<IResult> DeleteAlertRuleAsync(
        string deviceId,
        string ruleId,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        var result = await EditAlertRulesAsync(deviceId, null, repositories, cancellationToken, draft =>
        {
            var rule = draft.AlertRules.FirstOrDefault(existing => existing.RuleId == ruleId);
            if (rule is not null)
            {
                draft.AlertRules.Remove(rule);
            }

            return rule;
        });

        return result is IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK } ? Results.NoContent() : result;
    }

    /// <summary>Applies one alert rule change to the device's draft (creating it from the latest version if needed) and saves it.</summary>
    /// <param name="change">Returns the changed rule, or null when the rule id was not found.</param>
    private static async Task<IResult> EditAlertRulesAsync(
        string deviceId,
        DeviceAlertRuleRequest? request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken,
        Func<DeviceConfiguration, DeviceAlertRule?> change,
        bool created = false)
    {
        if (request?.Validate() is { Count: > 0 } validationErrors)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var draft = context.EnsureDraft(repositories.CurrentUser);
        var rule = change(draft);
        if (rule is null)
        {
            return Results.NotFound(new { message = "Alert rule was not found." });
        }

        var mergedErrors = draft.ValidateMerged();
        if (mergedErrors.Count > 0)
        {
            return Results.ValidationProblem(mergedErrors);
        }

        var saveError = await PersistDraftAsync(context, draft, repositories, cancellationToken);
        if (saveError is not null)
        {
            return saveError;
        }

        return created
            ? Results.Created($"/api/device-configurations/{context.Device.DeviceId}/draft/alert-rules/{rule.RuleId}", rule)
            : Results.Ok(rule);
    }

    private static async Task<IResult> GetVersionsAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        return context is null
            ? DeviceNotFound(deviceId)
            : Results.Ok(context.Versions.Select(DeviceConfigurationVersionResponse.FromEntity));
    }

    /// <summary>View changes: the full settings of one version; compare with the previous version to show what changed.</summary>
    private static async Task<IResult> GetVersionAsync(string deviceId, int version, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var configuration = context.Versions.FirstOrDefault(existing => existing.Version == version);
        return configuration is null
            ? VersionNotFound(context.Device.DeviceId, version)
            : Results.Ok(DeviceConfigurationResponse.FromEntity(configuration));
    }

    /// <summary>Restore: copies an earlier version's settings into the draft, replacing any unsaved draft settings.</summary>
    private static async Task<IResult> RestoreVersionAsync(string deviceId, int version, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var source = context.Versions.FirstOrDefault(existing => existing.Version == version);
        if (source is null)
        {
            return VersionNotFound(context.Device.DeviceId, version);
        }

        if (source.Status == DeviceConfigurationStatus.Draft)
        {
            return Results.Conflict(new { message = $"v{version} is the current draft." });
        }

        var copy = Clone(source);
        var draft = context.EnsureDraft(repositories.CurrentUser);
        draft.Connectivity = copy.Connectivity;
        draft.Telemetry = copy.Telemetry;
        draft.AlertRules = copy.AlertRules;
        draft.DeploymentMode = copy.DeploymentMode;
        draft.ScheduledAt = null;
        draft.RolloutTarget = copy.RolloutTarget;
        draft.DeviceGroup = copy.DeviceGroup;
        draft.RestoredFromVersion = version;
        draft.Note = $"Restored from v{version}";
        draft.Activity.Add(new DeviceConfigurationActivity
        {
            Message = $"Draft v{draft.Version} restored from v{version}",
            At = DateTime.UtcNow,
            By = repositories.CurrentUser?.UserName
        });

        return await PersistDraftAsync(context, draft, repositories, cancellationToken)
            ?? Results.Ok(DeviceConfigurationResponse.FromEntity(draft));
    }

    /// <summary>Called when the device reports at check-in that it runs a queued version.</summary>
    private static async Task<IResult> ConfirmVersionAsync(string deviceId, int version, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var configuration = context.Versions.FirstOrDefault(existing => existing.Version == version);
        if (configuration is null)
        {
            return VersionNotFound(context.Device.DeviceId, version);
        }

        if (configuration.Status != DeviceConfigurationStatus.Queued)
        {
            return Results.Conflict(new { message = $"v{version} is {configuration.Status}; only a Queued version can be confirmed." });
        }

        var now = DateTime.UtcNow;
        configuration.Status = DeviceConfigurationStatus.Applied;
        configuration.ConfirmedAt = now;
        configuration.Activity.Add(new DeviceConfigurationActivity
        {
            Message = $"v{version} confirmed by {context.Device.DeviceId} at check-in",
            At = now,
            By = SystemUser
        });
        await repositories.Configurations.UpdateAsync(configuration.Id, configuration, cancellationToken);

        foreach (var previous in context.Versions.Where(existing => existing.Status == DeviceConfigurationStatus.Applied && existing.Id != configuration.Id))
        {
            Supersede(previous, configuration, now, SystemUser);
            await repositories.Configurations.UpdateAsync(previous.Id, previous, cancellationToken);
        }

        await repositories.EventLogger.LogAsync(EntityType, configuration.Id, VersionName(configuration), EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceConfigurationResponse.FromEntity(configuration));
    }

    private static async Task<IResult> GetActivityAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        return Results.Ok(context.Versions
            .SelectMany(configuration => configuration.Activity.Select(activity =>
                new DeviceConfigurationActivityResponse(configuration.Version, activity.Message, activity.At, activity.By)))
            .OrderByDescending(activity => activity.At));
    }

    private static async Task<IResult> GetFirmwareAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        return context is null ? DeviceNotFound(deviceId) : Results.Ok(context.ToFirmwareResponse());
    }

    /// <summary>Records a firmware update for the device; the update itself is carried out by the device platform.</summary>
    private static async Task<IResult> ScheduleFirmwareUpdateAsync(
        string deviceId,
        ScheduleFirmwareUpdateRequest request,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var errors = new Dictionary<string, string[]>();
        var targetVersion = ContractText.TrimOrNull(request.TargetVersion) ?? context.Firmware?.LatestVersion;

        if (targetVersion is null)
        {
            errors["TargetVersion"] = ["TargetVersion is required because no latest firmware is recorded for this model"];
        }
        else if (FirmwareVersionText.Parse(targetVersion) is null)
        {
            errors["TargetVersion"] = ["TargetVersion must be a version number such as v1.7.0"];
        }

        if (request.ScheduledAt is null)
        {
            errors["ScheduledAt"] = ["ScheduledAt is required"];
        }
        else if (request.ScheduledAt.Value.ToUniversalTime() <= DateTime.UtcNow)
        {
            errors["ScheduledAt"] = ["ScheduledAt must be in the future"];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var device = context.Device;
        device.PendingFirmwareVersion = targetVersion;
        device.FirmwareUpdateScheduledAt = request.ScheduledAt!.Value.ToUniversalTime();
        device.UpdatedAt = DateTime.UtcNow;
        device.UpdatedBy = repositories.CurrentUser?.UserName;
        await repositories.Devices.UpdateAsync(device.Id, device, cancellationToken);
        await repositories.EventLogger.LogAsync("IoTDevice", device.DeviceId, $"{device.DeviceId} firmware update to {targetVersion}", EventAction.Updated, cancellationToken);

        return Results.Ok(context.ToFirmwareResponse());
    }

    private static async Task<IResult> CancelFirmwareUpdateAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
    {
        var context = await DeviceContext.LoadAsync(deviceId, repositories, cancellationToken);
        if (context is null)
        {
            return DeviceNotFound(deviceId);
        }

        var device = context.Device;
        if (device.PendingFirmwareVersion is null)
        {
            return Results.NotFound(new { message = $"Device '{device.DeviceId}' has no firmware update scheduled." });
        }

        device.PendingFirmwareVersion = null;
        device.FirmwareUpdateScheduledAt = null;
        device.UpdatedAt = DateTime.UtcNow;
        device.UpdatedBy = repositories.CurrentUser?.UserName;
        await repositories.Devices.UpdateAsync(device.Id, device, cancellationToken);
        await repositories.EventLogger.LogAsync("IoTDevice", device.DeviceId, $"{device.DeviceId} firmware update cancelled", EventAction.Updated, cancellationToken);

        return Results.NoContent();
    }

    /// <summary>Creates or updates the draft; returns an error result when another request created the same version first.</summary>
    private static async Task<IResult?> PersistDraftAsync(
        DeviceContext context,
        DeviceConfiguration draft,
        DeviceConfigurationDependencies repositories,
        CancellationToken cancellationToken)
    {
        draft.UpdatedAt = DateTime.UtcNow;
        draft.UpdatedBy = repositories.CurrentUser?.UserName;

        if (!string.IsNullOrEmpty(draft.Id))
        {
            await repositories.Configurations.UpdateAsync(draft.Id, draft, cancellationToken);
            return null;
        }

        try
        {
            await repositories.Configurations.CreateAsync(draft, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return Results.Conflict(new { message = $"Another change created v{draft.Version} of '{context.Device.DeviceId}' at the same time. Reload and try again." });
        }

        await repositories.EventLogger.LogAsync(EntityType, draft.Id, VersionName(draft), EventAction.Created, cancellationToken);
        return null;
    }

    private static void Supersede(DeviceConfiguration configuration, DeviceConfiguration replacement, DateTime at, string? by)
    {
        configuration.Status = DeviceConfigurationStatus.Superseded;
        configuration.UpdatedAt = at;
        configuration.UpdatedBy = by;
        configuration.Activity.Add(new DeviceConfigurationActivity { Message = $"v{configuration.Version} superseded by v{replacement.Version}", At = at, By = by });
    }

    private static string AppliedMessage(DeviceConfiguration configuration)
    {
        var target = configuration.RolloutTarget == ConfigurationRolloutTarget.DeviceGroup
            ? $"device group {configuration.DeviceGroup}"
            : configuration.DeviceId;

        return configuration.DeploymentMode == ConfigurationDeploymentMode.Scheduled
            ? $"v{configuration.Version} scheduled for {target} at {configuration.ScheduledAt:dd MMM yyyy HH:mm} UTC"
            : $"v{configuration.Version} applied to {target}, queued until next check-in";
    }

    private static DeviceConfiguration Clone(DeviceConfiguration configuration)
    {
        return BsonSerializer.Deserialize<DeviceConfiguration>(configuration.ToBsonDocument());
    }

    private static string VersionName(DeviceConfiguration configuration) => $"{configuration.DeviceId} v{configuration.Version}";

    private static IResult DeviceNotFound(string deviceId) =>
        Results.NotFound(new { message = $"IoT device '{deviceId}' was not found." });

    private static IResult VersionNotFound(string deviceId, int version) =>
        Results.NotFound(new { message = $"Configuration v{version} of '{deviceId}' was not found." });

    /// <summary>A device with its model, firmware row and configuration versions (newest first).</summary>
    private sealed class DeviceContext
    {
        private DeviceContext(IoTDevice device, DeviceModel? model, FirmwareVersion? firmware, List<DeviceConfiguration> versions)
        {
            Device = device;
            Model = model;
            Firmware = firmware;
            Versions = versions;
        }

        public IoTDevice Device { get; }
        public DeviceModel? Model { get; }
        public FirmwareVersion? Firmware { get; }
        public List<DeviceConfiguration> Versions { get; }

        public DeviceConfiguration? Draft => Versions.FirstOrDefault(version => version.Status == DeviceConfigurationStatus.Draft);

        public DeviceConfiguration? Current => Versions.FirstOrDefault(version =>
            version.Status is DeviceConfigurationStatus.Queued or DeviceConfigurationStatus.Applied);

        public DeviceConfiguration? Running => Versions.FirstOrDefault(version => version.Status == DeviceConfigurationStatus.Applied);

        public static async Task<DeviceContext?> LoadAsync(string deviceId, DeviceConfigurationDependencies repositories, CancellationToken cancellationToken)
        {
            var device = await repositories.Devices.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
            if (device is null)
            {
                return null;
            }

            // Devices name their model either by number ("EM300-TH") or with the manufacturer ("Milesight EM300-TH").
            var model = await repositories.Models.GetByModelNumberAsync(device.Model, cancellationToken);
            if (model is null && device.Model.LastIndexOf(' ') is var space and > 0)
            {
                model = await repositories.Models.GetByModelNumberAsync(device.Model[(space + 1)..], cancellationToken);
            }

            var firmware = await repositories.Firmware.GetByDeviceModelAsync(device.Model, cancellationToken);
            if (firmware is null && model is not null)
            {
                firmware = await repositories.Firmware.GetByDeviceModelAsync($"{model.Manufacturer} {model.ModelNumber}", cancellationToken)
                    ?? await repositories.Firmware.GetByDeviceModelAsync(model.ModelNumber, cancellationToken);
            }

            var versions = await repositories.Configurations.GetVersionsAsync(device.DeviceId, cancellationToken);
            return new DeviceContext(device, model, firmware, versions.ToList());
        }

        /// <summary>The draft, or a new one copied from the latest version (or defaults) and numbered after it; not yet saved.</summary>
        public DeviceConfiguration EnsureDraft(CurrentUser? currentUser)
        {
            if (Draft is { } existing)
            {
                return existing;
            }

            var source = Versions.FirstOrDefault();
            var draft = source is null ? CreateDefaults() : Clone(source);
            var now = DateTime.UtcNow;

            draft.Id = null!;
            draft.Version = (source?.Version ?? 0) + 1;
            draft.Status = DeviceConfigurationStatus.Draft;
            draft.Note = null;
            draft.RestoredFromVersion = null;
            draft.AppliedAt = null;
            draft.AppliedBy = null;
            draft.ConfirmedAt = null;
            draft.CreatedAt = now;
            draft.CreatedBy = currentUser?.UserName;
            draft.UpdatedAt = null;
            draft.UpdatedBy = null;
            draft.ClientId = currentUser?.ClientId ?? Device.ClientId;
            draft.TenantId = currentUser?.TenantId ?? Device.TenantId;
            draft.IsDeleted = false;
            draft.Activity = [new DeviceConfigurationActivity { Message = $"Draft v{draft.Version} created", At = now, By = currentUser?.UserName }];

            Versions.Insert(0, draft);
            return draft;
        }

        /// <summary>A starting configuration for a device that has none, built from its model's protocol and measurements.</summary>
        public DeviceConfiguration CreateDefaults()
        {
            var protocol = Model?.Protocol ?? Model?.Connectivity;
            var isLoRaWan = protocol?.Contains("LoRaWAN", StringComparison.OrdinalIgnoreCase) == true;

            var parameters = (Model?.SupportedMeasurements ?? [])
                .Select(name => new TelemetryParameter
                {
                    Name = name,
                    Type = TelemetryParameterType.Measurement,
                    Unit = DeviceMeasurement.All.FirstOrDefault(measurement => string.Equals(measurement.Name, name, StringComparison.OrdinalIgnoreCase))?.Unit,
                    Enabled = true,
                    Aggregation = TelemetryAggregation.Average
                })
                .ToList();

            AddDiagnostic(parameters, "Battery Level", "%", TelemetryAggregation.Last, enabled: Device.PowerSource == DevicePowerSource.Battery);
            AddDiagnostic(parameters, "RSSI", "dBm", TelemetryAggregation.Average, enabled: true);
            AddDiagnostic(parameters, "SNR", "dB", TelemetryAggregation.Average, enabled: false);

            return new DeviceConfiguration
            {
                DeviceId = Device.DeviceId,
                Version = 0,
                Connectivity = new DeviceConnectivitySettings
                {
                    Protocol = protocol,
                    JoinMode = isLoRaWan ? LoRaWanJoinMode.Otaa : null,
                    ReportingInterval = 5,
                    ReportingIntervalUnit = ConfigurationIntervalUnit.Minutes,
                    AdaptiveDataRate = isLoRaWan,
                    DataRate = isLoRaWan ? LoRaWanDataRate.Auto : null
                },
                Telemetry = new DeviceTelemetrySettings
                {
                    SamplingIntervalSeconds = 60,
                    BatteryReporting = Device.PowerSource == DevicePowerSource.Battery,
                    BatteryReportInterval = BatteryReportInterval.Daily,
                    Parameters = parameters
                },
                ClientId = Device.ClientId,
                TenantId = Device.TenantId
            };
        }

        public ConfigurationValidationResponse Validate(DeviceConfiguration configuration)
        {
            return ConfigurationValidationResponse.FromChecks(configuration.RunChecks(Device.FirmwareVersion, Model?.FirmwareFrom));
        }

        public DeviceFirmwareResponse ToFirmwareResponse()
        {
            var installed = Device.FirmwareVersion;
            var latest = Firmware?.LatestVersion;
            var installedVersion = FirmwareVersionText.Parse(installed);
            var latestVersion = FirmwareVersionText.Parse(latest);

            return new DeviceFirmwareResponse(
                Model is null ? Device.Model : $"{Model.Manufacturer} {Model.ModelNumber}",
                installed,
                latest,
                installedVersion is not null && latestVersion is not null && latestVersion > installedVersion,
                Firmware?.ReleaseDate,
                Firmware?.ReleaseNotes ?? [],
                Model?.FirmwareFrom,
                FirmwareVersionText.IsAtLeast(installed, Model?.FirmwareFrom),
                Device.PendingFirmwareVersion,
                Device.FirmwareUpdateScheduledAt);
        }

        private static void AddDiagnostic(List<TelemetryParameter> parameters, string name, string unit, string aggregation, bool enabled)
        {
            if (parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            parameters.Add(new TelemetryParameter
            {
                Name = name,
                Type = TelemetryParameterType.Diagnostic,
                Unit = unit,
                Enabled = enabled,
                Aggregation = aggregation
            });
        }
    }
}

/// <summary>The services every Device Configuration endpoint uses, registered as one scoped service to keep handler signatures short.</summary>
internal sealed class DeviceConfigurationDependencies(
    IDeviceConfigurationRepository configurations,
    IIoTDeviceRepository devices,
    IDeviceModelRepository models,
    IFirmwareVersionRepository firmware,
    IDeviceHealthRepository health,
    ICurrentUserAccessor currentUserAccessor,
    IEventLogger eventLogger)
{
    public IDeviceConfigurationRepository Configurations { get; } = configurations;
    public IIoTDeviceRepository Devices { get; } = devices;
    public IDeviceModelRepository Models { get; } = models;
    public IFirmwareVersionRepository Firmware { get; } = firmware;
    public IDeviceHealthRepository Health { get; } = health;
    public IEventLogger EventLogger { get; } = eventLogger;
    public CurrentUser? CurrentUser { get; } = currentUserAccessor.GetCurrentUser();
}
