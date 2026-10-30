using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Tag Management → Tag IDs, addressed by tag id (e.g. RFID-009183).</summary>
public static class TrackingTagEndpoints
{
    private const string EntityType = "TrackingTag";

    public static RouteGroupBuilder MapTrackingTagEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tag-ids").WithTags("Tag IDs");

        // Literal segments take precedence over /{tagId}, so these are not read as tag ids.
        group.MapGet("/technologies", GetTechnologies).WithName("GetTagTechnologies");
        group.MapGet("/tag-types", GetTagTypes).WithName("GetTagTypes");
        group.MapGet("/statuses", () => Results.Ok(TagStatus.All)).WithName("GetTagStatuses");
        group.MapGet("/{tagId}", GetTagAsync).WithName("GetTrackingTagById");
        group.MapPost("", CreateTagAsync).WithName("CreateTrackingTag");

        return group;
    }

    private static IResult GetTechnologies()
    {
        return Results.Ok(TagTechnology.All.Select(TagTechnologyOptionResponse.FromTechnology));
    }

    /// <summary>The tag types of one technology, or of all technologies when none is given.</summary>
    private static IResult GetTagTypes(string? technology)
    {
        if (string.IsNullOrWhiteSpace(technology))
        {
            return Results.Ok(TagTechnology.TagTypes.Values.SelectMany(types => types).Distinct());
        }

        var canonical = ContractText.Canonical(technology, TagTechnology.All);
        return canonical is null
            ? Results.BadRequest(new { message = $"Technology must be one of: {string.Join(", ", TagTechnology.All)}" })
            : Results.Ok(TagTechnology.TagTypes[canonical]);
    }

    private static async Task<IResult> GetTagAsync(
        string tagId,
        ITrackingTagRepository repository,
        CancellationToken cancellationToken)
    {
        var tag = await repository.GetByTagIdAsync(tagId.Trim(), cancellationToken);
        return tag is null
            ? Results.NotFound(new { message = $"Tag ID '{tagId}' was not found." })
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
}
