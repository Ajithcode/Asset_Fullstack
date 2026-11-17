using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// IoT Device Management: connected devices, addressed by device id (e.g. VS-30018). Online / offline, battery,
/// signal and last seen come from each device's latest reading in NetworkTimeSeries.
/// </summary>
public static class IoTDeviceEndpoints
{
    private const string EntityType = "IoTDevice";
    private const int DefaultPageSize = 10;

    public static RouteGroupBuilder MapIoTDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-devices").WithTags("IoT Devices");

        group.MapGet("", GetIoTDevicesAsync).WithName("GetIoTDevices");
        // Literal segments take precedence over /{deviceId}, so these are not read as device ids.
        group.MapGet("/summary", GetSummaryAsync).WithName("GetIoTDeviceSummary");
        group.MapGet("/device-types", GetDeviceTypesAsync).WithName("GetIoTDeviceTypes");
        group.MapGet("/models", GetModelsAsync).WithName("GetIoTDeviceModelOptions");
        group.MapGet("/connectivities", GetConnectivitiesAsync).WithName("GetIoTDeviceConnectivities");
        group.MapGet("/gateways", GetGatewaysAsync).WithName("GetIoTDeviceGateways");
        group.MapGet("/{deviceId}", GetIoTDeviceAsync).WithName("GetIoTDeviceById");
        group.MapPost("", CreateIoTDeviceAsync).WithName("CreateIoTDevice");
        group.MapPut("/{deviceId}", UpdateIoTDeviceAsync).WithName("UpdateIoTDevice");
        group.MapDelete("/{deviceId}", DeleteIoTDeviceAsync).WithName("DeleteIoTDevice");

        return group;
    }

    /// <summary>
    /// The device table, paged and filtered, with live status. With limit it is the Device dropdown search instead:
    /// at most limit matching devices as a plain list.
    /// </summary>
    private static async Task<IResult> GetIoTDevicesAsync(
        [AsParameters] IoTDeviceListQuery query,
        IIoTDeviceRepository repository,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        CancellationToken cancellationToken)
    {
        var errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (query.Limit is { } limit)
        {
            var found = await repository.SearchAsync(query.Search, limit, cancellationToken);
            var foundLive = await LiveAsync(found, models, readings, cancellationToken);
            return Results.Ok(foundLive.Select(IoTDeviceListItemResponse.From).ToList());
        }

        var all = await LiveAsync(TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken)).ToList(),
            models, readings, cancellationToken);

        var search = ContractText.TrimOrNull(query.Search);
        var deviceType = ContractText.TrimOrNull(query.DeviceType);
        var connectivity = ContractText.TrimOrNull(query.Connectivity);
        var siteId = ContractText.TrimOrNull(query.SiteId);
        var status = ContractText.Canonical(query.Status, DeviceConnection.All);
        var health = ContractText.Canonical(query.Health, IoTDeviceHealth.All);

        var matching = all.Where(live =>
                (deviceType is null || string.Equals(live.Device.DeviceType, deviceType, StringComparison.OrdinalIgnoreCase))
                && (connectivity is null || string.Equals(live.Device.Connectivity, connectivity, StringComparison.OrdinalIgnoreCase))
                && (siteId is null || string.Equals(live.Device.SiteId, siteId, StringComparison.OrdinalIgnoreCase))
                && (status is null || live.Connection == status)
                && (health is null || live.Health == health)
                && (query.Attention != true || live.LowBattery)
                && (search is null || Matches(live, search)))
            .ToList();

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(IoTDeviceListItemResponse.From)
            .ToList();

        return Results.Ok(PagedResponse<IoTDeviceListItemResponse>.Of(items, matching.Count, page, pageSize));
    }

    /// <summary>Total Devices, Online, Offline and Attention Required (battery under 50%) cards.</summary>
    private static async Task<IResult> GetSummaryAsync(
        IIoTDeviceRepository repository,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        CancellationToken cancellationToken)
    {
        var all = await LiveAsync(TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken)).ToList(),
            models, readings, cancellationToken);
        var total = all.Count;

        return Results.Ok(new IoTDeviceSummaryResponse(
            total,
            DeviceCountResponse.Of(all.Count(live => live.Connection == DeviceConnection.Online), total),
            DeviceCountResponse.Of(all.Count(live => live.Connection == DeviceConnection.Offline), total),
            DeviceCountResponse.Of(all.Count(live => live.LowBattery), total),
            all.Count(live => live.NeverReported),
            all.GroupBy(live => live.Device.DeviceType, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count())));
    }

    /// <summary>The device types of the selectable models, so every type offered has at least one model.</summary>
    private static async Task<IResult> GetDeviceTypesAsync(
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetSelectableAsync(null, cancellationToken);
        return Results.Ok(models
            .Select(model => model.DeviceType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<IResult> GetModelsAsync(
        string? deviceType,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetSelectableAsync(deviceType, cancellationToken);
        return Results.Ok(models.Select(DeviceModelOptionResponse.FromEntity));
    }

    /// <summary>
    /// Connectivity dropdown. With model, that model's protocol (e.g. BLE 5.0) and connectivity come first;
    /// then the protocols of the other selectable models and the generic connectivity types.
    /// </summary>
    private static async Task<IResult> GetConnectivitiesAsync(
        string? model,
        string? deviceType,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var options = new List<string>();

        if (!string.IsNullOrWhiteSpace(model)
            && await repository.GetByModelNumberAsync(model.Trim(), cancellationToken) is { } selected)
        {
            options.Add(selected.Protocol ?? string.Empty);
            options.Add(selected.Connectivity);
        }

        var models = await repository.GetSelectableAsync(deviceType, cancellationToken);
        options.AddRange(models.Select(x => x.Protocol ?? string.Empty).Order(StringComparer.OrdinalIgnoreCase));
        options.AddRange(DeviceModelConnectivity.All);

        return Results.Ok(options
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Gateway dropdown; with siteId, only gateways installed at that site.</summary>
    private static async Task<IResult> GetGatewaysAsync(
        string? siteId,
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var gateways = await repository.GetGatewaysAsync(siteId, cancellationToken);
        return Results.Ok(gateways.Select(GatewayOptionResponse.FromEntity));
    }

    /// <summary>The device side panel: identity, association and live status.</summary>
    private static async Task<IResult> GetIoTDeviceAsync(
        string deviceId,
        IIoTDeviceRepository repository,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        return device is null
            ? NotFound(deviceId)
            : Results.Ok(IoTDeviceDetailsResponse.From((await LiveAsync([device], models, readings, cancellationToken))[0]));
    }

    /// <summary>The Add Device form.</summary>
    private static async Task<IResult> CreateIoTDeviceAsync(
        AddIoTDeviceRequest request,
        IIoTDeviceRepository repository,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        ITagAssetLookup assets,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var deviceId = request.DeviceId!.Trim();
        if (await repository.DeviceIdExistsAsync(deviceId, cancellationToken))
        {
            return Results.Conflict(new { message = $"IoT device '{deviceId}' is already registered." });
        }

        if (await repository.SerialNumberExistsAsync(request.SerialNumber!.Trim(), null, cancellationToken))
        {
            return Results.Conflict(new { message = $"Serial number '{request.SerialNumber.Trim()}' is already registered to another device." });
        }

        var (model, asset, problem) = await ResolveAsync(request, models, assets, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var device = request.ToEntity(currentUserAccessor.GetCurrentUser(), model!, asset);

        await repository.CreateAsync(device, cancellationToken);
        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceName}", EventAction.Created, cancellationToken);

        var live = (await LiveAsync([device], models, readings, cancellationToken))[0];
        return Results.Created($"/api/iot-devices/{device.DeviceId}", IoTDeviceDetailsResponse.From(live));
    }

    /// <summary>Takes the same body as Add Device; its DeviceId must match the one in the URL.</summary>
    private static async Task<IResult> UpdateIoTDeviceAsync(
        string deviceId,
        AddIoTDeviceRequest request,
        IIoTDeviceRepository repository,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        ITagAssetLookup assets,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (TrackingRecords.KeyChanged("DeviceId", deviceId, request.DeviceId) is { } keyChanged)
        {
            return keyChanged;
        }

        var existing = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(deviceId);
        }

        if (await repository.SerialNumberExistsAsync(request.SerialNumber!.Trim(), existing.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Serial number '{request.SerialNumber.Trim()}' is already registered to another device." });
        }

        var (model, asset, problem) = await ResolveAsync(request, models, assets, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var device = request.ToEntity(currentUser, model!, asset).Replacing(existing, currentUser);
        device.DeviceId = existing.DeviceId;

        // Kept from the device: what the gateways and the firmware module report or schedule.
        device.Status = existing.Status;
        device.BatteryLevel = existing.BatteryLevel;
        device.LastSeenAt = existing.LastSeenAt;
        device.FirmwareVersion ??= existing.FirmwareVersion;
        device.PendingFirmwareVersion = existing.PendingFirmwareVersion;
        device.FirmwareUpdateScheduledAt = existing.FirmwareUpdateScheduledAt;

        if (!await repository.UpdateAsync(existing.Id, device, cancellationToken))
        {
            return NotFound(deviceId);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceName}", EventAction.Updated, cancellationToken);

        var live = (await LiveAsync([device], models, readings, cancellationToken))[0];
        return Results.Ok(IoTDeviceDetailsResponse.From(live));
    }

    private static async Task<IResult> DeleteIoTDeviceAsync(
        string deviceId,
        IIoTDeviceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        if (device is null || !await repository.DeleteAsync(device.Id, cancellationToken))
        {
            return NotFound(deviceId);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceName}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>The model must exist and match the device type; the linked asset, when given, must exist.</summary>
    private static async Task<(DeviceModel? Model, TagAsset? Asset, IResult? Problem)> ResolveAsync(
        AddIoTDeviceRequest request,
        IDeviceModelRepository models,
        ITagAssetLookup assets,
        CancellationToken cancellationToken)
    {
        var model = await models.GetByModelNumberAsync(request.Model!.Trim(), cancellationToken);
        if (model is null)
        {
            return (null, null, Invalid("Model", $"Model '{request.Model.Trim()}' was not found; pick one from GET /api/iot-devices/models"));
        }

        if (!string.Equals(model.DeviceType, request.DeviceType!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, Invalid("Model", $"Model '{model.ModelNumber}' is a {model.DeviceType}, not a {request.DeviceType.Trim()}"));
        }

        TagAsset? asset = null;
        if (ContractText.TrimOrNull(request.AssetCode) is { } assetCode)
        {
            asset = await assets.FindAsync(assetCode, cancellationToken);
            if (asset is null)
            {
                return (null, null, Invalid("AssetCode", $"No asset found with code or registration id '{assetCode}'"));
            }
        }

        return (model, asset, null);
    }

    /// <summary>Joins each device with its model and its latest NetworkTimeSeries reading.</summary>
    private static async Task<IReadOnlyList<LiveIoTDevice>> LiveAsync(
        IReadOnlyList<IoTDevice> devices,
        IDeviceModelRepository models,
        INetworkReadingRepository readings,
        CancellationToken cancellationToken)
    {
        var latest = await readings.GetLatestAsync(devices.Select(device => device.DeviceId), cancellationToken);
        var modelsByNumber = (await models.GetAllAsync(cancellationToken))
            .Where(model => !model.IsDeleted)
            .GroupBy(model => model.ModelNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return devices.Select(device => new LiveIoTDevice(
                device,
                latest.GetValueOrDefault(NetworkReadingKeys.Normalize(device.DeviceId)),
                modelsByNumber.GetValueOrDefault(device.Model)))
            .ToList();
    }

    private static bool Matches(LiveIoTDevice live, string search)
    {
        var device = live.Device;
        return new[] { device.DeviceId, device.DeviceName, device.SerialNumber, device.AssetCode, device.AssetName, device.AssetRegistrationId, live.Gateway }
            .Any(field => field?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IResult Invalid(string field, string message)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
    }

    private static IResult NotFound(string deviceId)
    {
        return Results.NotFound(new { message = $"IoT device '{deviceId}' was not found." });
    }
}
