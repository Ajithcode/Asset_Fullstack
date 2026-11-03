using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Replacement → Replacement Device: spare devices, addressed by device id (e.g. BLE-30112).</summary>
public static class ReplacementDeviceEndpoints
{
    private const string EntityType = "ReplacementDevice";

    public static RouteGroupBuilder MapReplacementDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/replacement-devices").WithTags("Replacement Devices");

        group.MapGet("", GetReplacementDevicesAsync).WithName("GetReplacementDevices");
        group.MapGet("/{deviceId}", GetReplacementDeviceAsync).WithName("GetReplacementDeviceById");
        group.MapPost("", CreateReplacementDeviceAsync).WithName("CreateReplacementDevice");
        group.MapPut("/{deviceId}", UpdateReplacementDeviceAsync).WithName("UpdateReplacementDevice");
        group.MapDelete("/{deviceId}", DeleteReplacementDeviceAsync).WithName("DeleteReplacementDevice");

        return group;
    }

    private static async Task<IResult> GetReplacementDevicesAsync(
        IReplacementDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var devices = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(devices).Select(ReplacementDeviceResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetReplacementDeviceAsync(
        string deviceId,
        IReplacementDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        return device is null
            ? NotFound(deviceId)
            : Results.Ok(ReplacementDeviceResponse.FromEntity(device));
    }

    private static async Task<IResult> CreateReplacementDeviceAsync(
        CreateReplacementDeviceRequest request,
        IReplacementDeviceRepository repository,
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
            return Results.Conflict(new { message = $"Replacement device '{device.DeviceId}' is already registered." });
        }

        await repository.CreateAsync(device, cancellationToken);
        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.Model}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/replacement-devices/{device.DeviceId}", ReplacementDeviceResponse.FromEntity(device));
    }

    /// <summary>Takes the same body as create; its DeviceId must match the one in the URL.</summary>
    private static async Task<IResult> UpdateReplacementDeviceAsync(
        string deviceId,
        CreateReplacementDeviceRequest request,
        IReplacementDeviceRepository repository,
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

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.Model}", EventAction.Updated, cancellationToken);

        return Results.Ok(ReplacementDeviceResponse.FromEntity(device));
    }

    private static async Task<IResult> DeleteReplacementDeviceAsync(
        string deviceId,
        IReplacementDeviceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        if (device is null || !await repository.DeleteAsync(device.Id, cancellationToken))
        {
            return NotFound(deviceId);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.Model}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string deviceId)
    {
        return Results.NotFound(new { message = $"Replacement device '{deviceId}' was not found." });
    }
}
