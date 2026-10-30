using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Tag Management → Tag IDs, addressed by tag id (e.g. RFID-009183).</summary>
public static class TrackingTagEndpoints
{
    private const string EntityType = "TrackingTag";

    public static RouteGroupBuilder MapTrackingTagEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tag-ids").WithTags("Tag IDs");

        group.MapGet("", GetTagsAsync).WithName("GetTrackingTags");
        group.MapGet("/{tagId}", GetTagAsync).WithName("GetTrackingTagById");
        group.MapPost("", CreateTagAsync).WithName("CreateTrackingTag");
        group.MapPut("/{tagId}", UpdateTagAsync).WithName("UpdateTrackingTag");
        group.MapDelete("/{tagId}", DeleteTagAsync).WithName("DeleteTrackingTag");

        return group;
    }

    private static async Task<IResult> GetTagsAsync(
        ITrackingTagRepository repository,
        CancellationToken cancellationToken)
    {
        var tags = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(tags).Select(TrackingTagResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetTagAsync(
        string tagId,
        ITrackingTagRepository repository,
        CancellationToken cancellationToken)
    {
        var tag = await repository.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        return tag is null
            ? NotFound(tagId)
            : Results.Ok(TrackingTagResponse.FromEntity(tag));
    }

    private static async Task<IResult> CreateTagAsync(
        CreateTrackingTagRequest request,
        ITrackingTagRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var tag = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.TagIdExistsAsync(tag.TagId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Tag ID '{tag.TagId}' is already registered." });
        }

        await repository.CreateAsync(tag, cancellationToken);
        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Created, cancellationToken);

        return Results.Created($"/api/tag-ids/{tag.TagId}", TrackingTagResponse.FromEntity(tag));
    }

    /// <summary>Takes the same body as create; its TagId must match the one in the URL.</summary>
    private static async Task<IResult> UpdateTagAsync(
        string tagId,
        CreateTrackingTagRequest request,
        ITrackingTagRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (TrackingRecords.KeyChanged("TagId", tagId, request.TagId) is { } keyChanged)
        {
            return keyChanged;
        }

        var existing = await repository.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(tagId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var tag = request.ToEntity(currentUser).Replacing(existing, currentUser);
        tag.TagId = existing.TagId;

        if (!await repository.UpdateAsync(existing.Id, tag, cancellationToken))
        {
            return NotFound(tagId);
        }

        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Updated, cancellationToken);

        return Results.Ok(TrackingTagResponse.FromEntity(tag));
    }

    private static async Task<IResult> DeleteTagAsync(
        string tagId,
        ITrackingTagRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var tag = await repository.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        if (tag is null || !await repository.DeleteAsync(tag.Id, cancellationToken))
        {
            return NotFound(tagId);
        }

        await eventLogger.LogAsync(EntityType, tag.TagId, tag.TagId, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string tagId)
    {
        return Results.NotFound(new { message = $"Tag ID '{tagId}' was not found." });
    }
}
