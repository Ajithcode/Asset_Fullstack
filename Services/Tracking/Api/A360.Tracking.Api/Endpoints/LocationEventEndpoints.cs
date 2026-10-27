using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Real-Time Location → Recent Location Events, addressed by event id (e.g. LE000001).</summary>
public static class LocationEventEndpoints
{
    private const string EntityType = "LocationEvent";
    private const string SequenceName = "LocationEvent";
    private const string EventIdPrefix = "LE";

    public static RouteGroupBuilder MapLocationEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/location-events").WithTags("Location Events");

        group.MapGet("/{eventId}", GetLocationEventAsync).WithName("GetLocationEventById");
        group.MapPost("", CreateLocationEventAsync).WithName("CreateLocationEvent");

        return group;
    }

    private static async Task<IResult> GetLocationEventAsync(
        string eventId,
        ILocationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var locationEvent = await repository.GetByEventIdAsync(eventId.Trim(), cancellationToken);
        return locationEvent is null
            ? Results.NotFound(new { message = $"Location event '{eventId}' was not found." })
            : Results.Ok(LocationEventResponse.FromEntity(locationEvent));
    }

    private static async Task<IResult> CreateLocationEventAsync(
        CreateLocationEventRequest request,
        ILocationEventRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var locationEvent = request.ToEntity($"{EventIdPrefix}{nextSequence:D6}", currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(locationEvent, cancellationToken);
        await eventLogger.LogAsync(EntityType, locationEvent.EventId, $"{locationEvent.AssetId} - {locationEvent.EventType}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/location-events/{locationEvent.EventId}", LocationEventResponse.FromEntity(locationEvent));
    }
}
