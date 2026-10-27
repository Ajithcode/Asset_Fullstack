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

        group.MapGet("/{deviceId}", GetReplacementDeviceAsync).WithName("GetReplacementDeviceById");
        group.MapPost("", CreateReplacementDeviceAsync).WithName("CreateReplacementDevice");

        return group;
    }

    private static async Task<IResult> GetReplacementDeviceAsync(
        string deviceId,
        IReplacementDeviceRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        return device is null
            ? Results.NotFound(new { message = $"Replacement device '{deviceId}' was not found." })
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
}
