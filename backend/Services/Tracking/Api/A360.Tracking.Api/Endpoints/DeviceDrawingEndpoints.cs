using A360.Domain.Entities;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Tracking.Api.Endpoints;

public static class DeviceDrawingEndpoints
{
    private const string EntityType = "DeviceDrawing";

    public static RouteGroupBuilder MapDeviceDrawingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-drawings").WithTags("Device Drawings");

        group.MapPost("", CreateDeviceDrawingAsync).WithName("CreateDeviceDrawing");

        return group;
    }

    private static async Task<IResult> CreateDeviceDrawingAsync(
        CreateDeviceDrawingRequest request,
        IDeviceDrawingRepository deviceDrawingRepository,
        IZoneDrawingRepository zoneDrawingRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var deviceDrawing = await deviceDrawingRepository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()?.UserName),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, deviceDrawing.Id, deviceDrawing.DeviceName, EventAction.Created, cancellationToken);

        // The client redraws the zone together with the device it just placed in it.
        var zoneDrawings = await zoneDrawingRepository.GetByZoneIdAsync(deviceDrawing.ZoneId, cancellationToken);

        var response = new CustomerAnalyticsDrawingResponse(
            zoneDrawings.Select(ZoneDrawingResponse.FromEntity).ToList(),
            [DeviceDrawingResponse.FromEntity(deviceDrawing)]);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
