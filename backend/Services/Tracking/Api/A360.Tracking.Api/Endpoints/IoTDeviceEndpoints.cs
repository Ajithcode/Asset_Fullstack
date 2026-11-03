using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>IoT Device Management: connected devices, addressed by device id (e.g. VS-30018).</summary>
public static class IoTDeviceEndpoints
{
    private const string EntityType = "IoTDevice";

    public static RouteGroupBuilder MapIoTDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-devices").WithTags("IoT Devices");

        group.MapGet("", GetIoTDevicesAsync).WithName("GetIoTDevices");
        group.MapGet("/{deviceId}", GetIoTDeviceAsync).WithName("GetIoTDeviceById");
        group.MapPost("", CreateIoTDeviceAsync).WithName("CreateIoTDevice");
        group.MapPut("/{deviceId}", UpdateIoTDeviceAsync).WithName("UpdateIoTDevice");
        group.MapDelete("/{deviceId}", DeleteIoTDeviceAsync).WithName("DeleteIoTDevice");

        return group;
    }

    private static async Task<IResult> GetIoTDevicesAsync(
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var devices = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(devices).Select(IoTDeviceResponse.FromEntity).ToList());
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
