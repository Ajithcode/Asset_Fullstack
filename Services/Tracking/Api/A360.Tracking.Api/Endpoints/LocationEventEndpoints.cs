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

        group.MapGet("", GetLocationEventsAsync).WithName("GetLocationEvents");
        group.MapGet("/{eventId}", GetLocationEventAsync).WithName("GetLocationEventById");
        group.MapPost("", CreateLocationEventAsync).WithName("CreateLocationEvent");
        group.MapPut("/{eventId}", UpdateLocationEventAsync).WithName("UpdateLocationEvent");
        group.MapDelete("/{eventId}", DeleteLocationEventAsync).WithName("DeleteLocationEvent");

        return group;
    }

    /// <summary>Most recent event first.</summary>
    private static async Task<IResult> GetLocationEventsAsync(
        ILocationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var locationEvents = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(locationEvents)
            .OrderByDescending(locationEvent => locationEvent.EventTime)
            .Select(LocationEventResponse.FromEntity)
            .ToList());
    }

    private static async Task<IResult> GetLocationEventAsync(
        string eventId,
        ILocationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var locationEvent = await repository.GetByEventIdAsync(eventId.Trim(), cancellationToken);
        return locationEvent is null
            ? NotFound(eventId)
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

    /// <summary>Takes the same body as create; the event keeps its event id.</summary>
    private static async Task<IResult> UpdateLocationEventAsync(
        string eventId,
        CreateLocationEventRequest request,
        ILocationEventRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByEventIdAsync(eventId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(eventId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var locationEvent = request.ToEntity(existing.EventId, currentUser).Replacing(existing, currentUser);

        if (!await repository.UpdateAsync(existing.Id, locationEvent, cancellationToken))
        {
            return NotFound(eventId);
        }

        await eventLogger.LogAsync(EntityType, locationEvent.EventId, $"{locationEvent.AssetId} - {locationEvent.EventType}", EventAction.Updated, cancellationToken);

        return Results.Ok(LocationEventResponse.FromEntity(locationEvent));
    }

    private static async Task<IResult> DeleteLocationEventAsync(
        string eventId,
        ILocationEventRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var locationEvent = await repository.GetByEventIdAsync(eventId.Trim(), cancellationToken);
        if (locationEvent is null || !await repository.DeleteAsync(locationEvent.Id, cancellationToken))
        {
            return NotFound(eventId);
        }

        await eventLogger.LogAsync(EntityType, locationEvent.EventId, $"{locationEvent.AssetId} - {locationEvent.EventType}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string eventId)
    {
        return Results.NotFound(new { message = $"Location event '{eventId}' was not found." });
    }
}
