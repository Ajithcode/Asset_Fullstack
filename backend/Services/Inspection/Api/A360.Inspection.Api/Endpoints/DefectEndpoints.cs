using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class DefectEndpoints
{
    private const string SequenceName = "defect";
    private const string DefectCodePrefix = "DEF";

    public static RouteGroupBuilder MapDefectEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/defects").WithTags("Defects");

        group.MapGet("", GetDefectsAsync).WithName("GetDefects");
        group.MapGet("/{id}", GetDefectByIdAsync).WithName("GetDefectById");
        group.MapPost("", CreateDefectAsync).WithName("CreateDefect");
        group.MapPut("/{id}", UpdateDefectAsync).WithName("UpdateDefect");
        group.MapDelete("/{id}", DeleteDefectAsync).WithName("DeleteDefect");

        return group;
    }

    private static async Task<IResult> GetDefectsAsync(
        IDefectRepository repository,
        CancellationToken cancellationToken)
    {
        var defects = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(defects.Select(DefectResponse.FromEntity));
    }

    private static async Task<IResult> GetDefectByIdAsync(
        string id,
        IDefectRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid defect id." });
        }

        var defect = await repository.GetByIdAsync(id, cancellationToken);
        if (defect is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Defect", defect.DefectCode, defect.DefectName, EventAction.Viewed, cancellationToken);
        return Results.Ok(DefectResponse.FromEntity(defect));
    }

    private static async Task<IResult> CreateDefectAsync(
        CreateDefectRequest request,
        IDefectRepository repository,
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
        var defectCode = $"{DefectCodePrefix}{nextSequence:D6}";

        var defect = await repository.CreateAsync(request.ToEntity(defectCode), cancellationToken);

        await eventLogger.LogAsync("Defect", defect.DefectCode, defect.DefectName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/defects/{defect.Id}", DefectResponse.FromEntity(defect));
    }

    private static async Task<IResult> UpdateDefectAsync(
        string id,
        UpdateDefectRequest request,
        IDefectRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid defect id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var defect = await repository.GetByIdAsync(id, cancellationToken);
        if (defect is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(defect);

        var updated = await repository.UpdateAsync(id, defect, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Defect", defect.DefectCode, defect.DefectName, EventAction.Updated, cancellationToken);
        return Results.Ok(DefectResponse.FromEntity(defect));
    }

    private static async Task<IResult> DeleteDefectAsync(
        string id,
        IDefectRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid defect id." });
        }

        var defect = await repository.GetByIdAsync(id, cancellationToken);
        if (defect is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("Defect", defect.DefectCode, defect.DefectName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
