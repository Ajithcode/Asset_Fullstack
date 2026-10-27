using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>IoT Device Management: connected devices, addressed by device id (e.g. VS-30018).</summary>
/// <remarks>Served under /api/iot-devices because /api/devices belongs to the Devices service.</remarks>
public static class IoTDeviceEndpoints
{
    private const string EntityType = "IoTDevice";

    public static RouteGroupBuilder MapIoTDeviceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-devices").WithTags("IoT Devices");

        group.MapGet("/{deviceId}", GetIoTDeviceAsync).WithName("GetIoTDeviceById");
        group.MapPost("", CreateIoTDeviceAsync).WithName("CreateIoTDevice");

        return group;
    }

    private static async Task<IResult> GetIoTDeviceAsync(
        string deviceId,
        IIoTDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        return device is null
            ? Results.NotFound(new { message = $"IoT device '{deviceId}' was not found." })
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
}
