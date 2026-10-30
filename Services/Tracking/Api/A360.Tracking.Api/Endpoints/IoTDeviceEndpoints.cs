using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>IoT Device Management: connected devices, addressed by device id (e.g. VS-30018).</summary>
public static class IoTDeviceEndpoints
{
    private const string EntityType = "IoTDevice";
    private const int MaxSearchResults = 100;

    public static RouteGroupBuilder MapIoTDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-devices").WithTags("IoT Devices");

        group.MapGet("", GetIoTDevicesAsync).WithName("GetIoTDevices");
        // Literal segments take precedence over /{deviceId}, so these are not read as device ids.
        group.MapGet("/device-types", GetDeviceTypesAsync).WithName("GetIoTDeviceTypes");
        group.MapGet("/models", GetModelsAsync).WithName("GetIoTDeviceModelOptions");
        group.MapGet("/connectivities", () => Results.Ok(DeviceModelConnectivity.All)).WithName("GetIoTDeviceConnectivities");
        group.MapGet("/gateways", GetGatewaysAsync).WithName("GetIoTDeviceGateways");
        group.MapGet("/{deviceId}", GetIoTDeviceAsync).WithName("GetIoTDeviceById");
        group.MapPost("", CreateIoTDeviceAsync).WithName("CreateIoTDevice");
        group.MapPut("/{deviceId}", UpdateIoTDeviceAsync).WithName("UpdateIoTDevice");
        group.MapDelete("/{deviceId}", DeleteIoTDeviceAsync).WithName("DeleteIoTDevice");

        return group;
    }

    /// <summary>
    /// Every device; with search or limit, the Device dropdown search by id or name (at most limit, default 50).
    /// </summary>
    private static async Task<IResult> GetIoTDevicesAsync(
        string? search,
        int? limit,
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        if (search is null && limit is null)
        {
            var all = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(TrackingRecords.Listed(all).Select(IoTDeviceResponse.FromEntity).ToList());
        }

        var devices = await repository.SearchAsync(search, Math.Clamp(limit ?? 50, 1, MaxSearchResults), cancellationToken);
        return Results.Ok(devices.Select(IoTDeviceResponse.FromEntity));
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

    private static async Task<IResult> GetGatewaysAsync(
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var gateways = await repository.GetGatewaysAsync(cancellationToken);
        return Results.Ok(gateways.Select(GatewayOptionResponse.FromEntity));
    }

    private static async Task<IResult> GetIoTDeviceAsync(
        string deviceId,
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        return device is null
            ? NotFound(deviceId)
            : Results.Ok(IoTDeviceResponse.FromEntity(device));
    }

    private static async Task<IResult> CreateIoTDeviceAsync(
        CreateIoTDeviceRequest request,
        IIoTDeviceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var device = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.DeviceIdExistsAsync(device.DeviceId, cancellationToken))
        {
            return Results.Conflict(new { message = $"IoT device '{device.DeviceId}' is already registered." });
        }

        await repository.CreateAsync(device, cancellationToken);
        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceName}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/iot-devices/{device.DeviceId}", IoTDeviceResponse.FromEntity(device));
    }

    /// <summary>Takes the same body as create; its DeviceId must match the one in the URL.</summary>
    private static async Task<IResult> UpdateIoTDeviceAsync(
        string deviceId,
        CreateIoTDeviceRequest request,
        IIoTDeviceRepository repository,
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

        var currentUser = currentUserAccessor.GetCurrentUser();
        var device = request.ToEntity(currentUser).Replacing(existing, currentUser);
        device.DeviceId = existing.DeviceId;

        if (!await repository.UpdateAsync(existing.Id, device, cancellationToken))
        {
            return NotFound(deviceId);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceName}", EventAction.Updated, cancellationToken);

        return Results.Ok(IoTDeviceResponse.FromEntity(device));
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

    private static IResult NotFound(string deviceId)
    {
        return Results.NotFound(new { message = $"IoT device '{deviceId}' was not found." });
    }
}
