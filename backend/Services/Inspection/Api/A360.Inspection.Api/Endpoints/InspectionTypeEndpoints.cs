using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class InspectionTypeEndpoints
{
    private const string SequenceName = "inspection_type";
    private const string InspectionTypeCodePrefix = "INS";

    public static RouteGroupBuilder MapInspectionTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inspection-types").WithTags("InspectionTypes");

        group.MapGet("", GetInspectionTypesAsync).WithName("GetInspectionTypes");
        group.MapGet("/{id}", GetInspectionTypeByIdAsync).WithName("GetInspectionTypeById");
        group.MapPost("", CreateInspectionTypeAsync).WithName("CreateInspectionType");
        group.MapPut("/{id}", UpdateInspectionTypeAsync).WithName("UpdateInspectionType");
        group.MapDelete("/{id}", DeleteInspectionTypeAsync).WithName("DeleteInspectionType");

        return group;
    }

    private static async Task<IResult> GetInspectionTypesAsync(
        IInspectionTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var inspectionTypes = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(inspectionTypes.Select(InspectionTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetInspectionTypeByIdAsync(
        string id,
        IInspectionTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection type id." });
        }

        var inspectionType = await repository.GetByIdAsync(id, cancellationToken);
        if (inspectionType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionType", inspectionType.InspectionTypeCode, inspectionType.InspectionTypeName, EventAction.Viewed, cancellationToken);
        return Results.Ok(InspectionTypeResponse.FromEntity(inspectionType));
    }

    private static async Task<IResult> CreateInspectionTypeAsync(
        CreateInspectionTypeRequest request,
        IInspectionTypeRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var inspectionTypeCode = $"{InspectionTypeCodePrefix}{nextSequence:D6}";

        var inspectionType = await repository.CreateAsync(request.ToEntity(inspectionTypeCode), cancellationToken);

        await eventLogger.LogAsync("InspectionType", inspectionType.InspectionTypeCode, inspectionType.InspectionTypeName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/inspection-types/{inspectionType.Id}", InspectionTypeResponse.FromEntity(inspectionType));
    }

    private static async Task<IResult> UpdateInspectionTypeAsync(
        string id,
        UpdateInspectionTypeRequest request,
        IInspectionTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection type id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var inspectionType = await repository.GetByIdAsync(id, cancellationToken);
        if (inspectionType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(inspectionType);

        var updated = await repository.UpdateAsync(id, inspectionType, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionType", inspectionType.InspectionTypeCode, inspectionType.InspectionTypeName, EventAction.Updated, cancellationToken);
        return Results.Ok(InspectionTypeResponse.FromEntity(inspectionType));
    }

    private static async Task<IResult> DeleteInspectionTypeAsync(
        string id,
        IInspectionTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection type id." });
        }

        var inspectionType = await repository.GetByIdAsync(id, cancellationToken);
        if (inspectionType is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionType", inspectionType.InspectionTypeCode, inspectionType.InspectionTypeName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
