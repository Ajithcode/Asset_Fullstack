using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Health & Status: connectivity, signal, power and health per device, addressed by record id.</summary>
public static class DeviceHealthEndpoints
{
    private const string EntityType = "DeviceHealth";

    public static RouteGroupBuilder MapDeviceHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-health").WithTags("Device Health");

        group.MapGet("", GetDeviceHealthListAsync).WithName("GetDeviceHealthList");
        group.MapGet("/{id}", GetDeviceHealthAsync).WithName("GetDeviceHealthById");
        group.MapPost("", CreateDeviceHealthAsync).WithName("CreateDeviceHealth");
        group.MapPut("/{id}", UpdateDeviceHealthAsync).WithName("UpdateDeviceHealth");
        group.MapDelete("/{id}", DeleteDeviceHealthAsync).WithName("DeleteDeviceHealth");

        return group;
    }

    private static async Task<IResult> GetDeviceHealthListAsync(
        IDeviceHealthRepository repository,
        CancellationToken cancellationToken)
    {
        var devices = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(devices).Select(DeviceHealthResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetDeviceHealthAsync(
        string id,
        IDeviceHealthRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        return device is null || device.IsDeleted
            ? NotFound(id)
            : Results.Ok(DeviceHealthResponse.FromEntity(device));
    }

    private static async Task<IResult> CreateDeviceHealthAsync(
        CreateDeviceHealthRequest request,
        IDeviceHealthRepository repository,
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

        if (await repository.DeviceIdExistsAsync(device.DeviceId, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Health for device '{device.DeviceId}' is already registered." });
        }

        await repository.CreateAsync(device, cancellationToken);
        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceModel}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-health/{device.Id}", DeviceHealthResponse.FromEntity(device));
    }

    /// <summary>Takes the same body as create.</summary>
    private static async Task<IResult> UpdateDeviceHealthAsync(
        string id,
        CreateDeviceHealthRequest request,
        IDeviceHealthRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (existing is null || existing.IsDeleted)
        {
            return NotFound(id);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var device = request.ToEntity(currentUser).Replacing(existing, currentUser);

        if (await repository.DeviceIdExistsAsync(device.DeviceId, existing.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Health for device '{device.DeviceId}' is already registered." });
        }

        if (!await repository.UpdateAsync(existing.Id, device, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceModel}", EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceHealthResponse.FromEntity(device));
    }

    private static async Task<IResult> DeleteDeviceHealthAsync(
        string id,
        IDeviceHealthRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var device = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (device is null || device.IsDeleted || !await repository.DeleteAsync(device.Id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceModel}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Device health record '{id}' was not found." });
    }
}
