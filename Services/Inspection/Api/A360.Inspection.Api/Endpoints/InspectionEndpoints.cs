using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class InspectionEndpoints
{
    private const string SequencePrefix = InspectionCodes.InspectionSequencePrefix;
    private const string InspectionCodePrefix = "INS";
    private const string DefaultStatus = "Draft";

    public static RouteGroupBuilder MapInspectionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inspections").WithTags("Inspections");

        group.MapGet("", GetInspectionsAsync).WithName("GetInspections");
        group.MapGet("/{id}", GetInspectionByIdAsync).WithName("GetInspectionById");
        group.MapPost("", CreateInspectionAsync).WithName("CreateInspection");
        group.MapPut("/{id}", UpdateInspectionAsync).WithName("UpdateInspection");
        group.MapDelete("/{id}", DeleteInspectionAsync).WithName("DeleteInspection");

        return group;
    }

    private static async Task<IResult> GetInspectionsAsync(
        IInspectionRepository repository,
        CancellationToken cancellationToken)
    {
        var inspections = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(inspections.Select(InspectionResponse.FromEntity));
    }

    private static async Task<IResult> GetInspectionByIdAsync(
        string id,
        IInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection id." });
        }

        var inspection = await repository.GetByIdAsync(id, cancellationToken);
        if (inspection is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Inspection", inspection.InspectionCode, inspection.Title, EventAction.Viewed, cancellationToken);
        return Results.Ok(InspectionResponse.FromEntity(inspection));
    }

    private static async Task<IResult> CreateInspectionAsync(
        CreateInspectionRequest request,
        IInspectionRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var inspectionCode = await InspectionCodes.NextYearlyAsync(sequenceGenerator, SequencePrefix, InspectionCodePrefix, cancellationToken);

        var entity = request.ToEntity(inspectionCode);
        if (string.IsNullOrWhiteSpace(entity.Status))
        {
            entity.Status = DefaultStatus;
        }

        var inspection = await repository.CreateAsync(entity, cancellationToken);

        await eventLogger.LogAsync("Inspection", inspection.InspectionCode, inspection.Title, EventAction.Created, cancellationToken);

        return Results.Created($"/api/inspections/{inspection.Id}", InspectionResponse.FromEntity(inspection));
    }

    private static async Task<IResult> UpdateInspectionAsync(
        string id,
        UpdateInspectionRequest request,
        IInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var inspection = await repository.GetByIdAsync(id, cancellationToken);
        if (inspection is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(inspection);

        var updated = await repository.UpdateAsync(id, inspection, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Inspection", inspection.InspectionCode, inspection.Title, EventAction.Updated, cancellationToken);
        return Results.Ok(InspectionResponse.FromEntity(inspection));
    }

    private static async Task<IResult> DeleteInspectionAsync(
        string id,
        IInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection id." });
        }

        var inspection = await repository.GetByIdAsync(id, cancellationToken);
        if (inspection is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Inspection", inspection.InspectionCode, inspection.Title, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
