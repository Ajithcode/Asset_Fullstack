using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Health & Status: connectivity, signal, power and health per device.</summary>
public static class DeviceHealthEndpoints
{
    private const string EntityType = "DeviceHealth";

    public static RouteGroupBuilder MapDeviceHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-health").WithTags("Device Health");

        group.MapPost("", CreateDeviceHealthAsync).WithName("CreateDeviceHealth");

        return group;
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

        if (await repository.DeviceIdExistsAsync(device.DeviceId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Health for device '{device.DeviceId}' is already registered." });
        }

        await repository.CreateAsync(device, cancellationToken);
        await eventLogger.LogAsync(EntityType, device.DeviceId, $"{device.DeviceId} - {device.DeviceModel}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-health/{device.Id}", DeviceHealthResponse.FromEntity(device));
    }
}
