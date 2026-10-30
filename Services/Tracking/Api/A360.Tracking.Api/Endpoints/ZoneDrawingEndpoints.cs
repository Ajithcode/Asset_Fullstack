using A360.Domain.Entities;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Tracking.Api.Endpoints;

public static class ZoneDrawingEndpoints
{
    private const string EntityType = "ZoneDrawing";

    public static RouteGroupBuilder MapZoneDrawingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/zone-drawings").WithTags("Zone Drawings");

        group.MapPost("", CreateZoneDrawingAsync).WithName("CreateZoneDrawing");

        return group;
    }

    private static async Task<IResult> CreateZoneDrawingAsync(
        CreateZoneDrawingRequest request,
        IZoneDrawingRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zoneDrawing = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()?.UserName),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, zoneDrawing.Id, zoneDrawing.ZoneName, EventAction.Created, cancellationToken);

        // Only POST exists, so there is no GET-by-id URL to return as the Location header.
        return Results.Json(ZoneDrawingResponse.FromEntity(zoneDrawing), statusCode: StatusCodes.Status201Created);
    }
}
