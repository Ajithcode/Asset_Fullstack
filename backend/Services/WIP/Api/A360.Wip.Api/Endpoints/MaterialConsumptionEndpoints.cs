
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class MaterialConsumptionEndpoints
{
    public static RouteGroupBuilder MapMaterialConsumptionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/material-consumptions")
            .WithTags("MaterialConsumptions");

        group.MapGet("", GetMaterialConsumptionsAsync)
            .WithName("GetMaterialConsumptions");

        group.MapGet("/{id}", GetMaterialConsumptionByIdAsync)
            .WithName("GetMaterialConsumptionById");

        group.MapGet("/asset/{assetId}", GetMaterialConsumptionsByAssetIdAsync)
            .WithName("GetMaterialConsumptionsByAssetId");

        group.MapGet("/job/{jobId}", GetMaterialConsumptionsByJobIdAsync)
            .WithName("GetMaterialConsumptionsByJobId");

        group.MapPost("", CreateMaterialConsumptionAsync)
            .WithName("CreateMaterialConsumption");

        group.MapPut("/{id}", UpdateMaterialConsumptionAsync)
            .WithName("UpdateMaterialConsumption");

        group.MapDelete("/{id}", DeleteMaterialConsumptionAsync)
            .WithName("DeleteMaterialConsumption");

        return group;
    }

    private static async Task<IResult> GetMaterialConsumptionsAsync(
        IMaterialConsumptionRepository repository,
        CancellationToken cancellationToken)
    {
        var materialConsumptions = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            materialConsumptions.Select(MaterialConsumptionResponse.FromEntity));
    }

    private static async Task<IResult> GetMaterialConsumptionByIdAsync(
        string id,
        IMaterialConsumptionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid material id." });
        }

        var materialConsumption = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (materialConsumption is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaterialConsumption",
            materialConsumption.Id,
            materialConsumption.MaterialId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(MaterialConsumptionResponse.FromEntity(materialConsumption));
    }

    private static async Task<IResult> GetMaterialConsumptionsByAssetIdAsync(
        string assetId,
        IMaterialConsumptionRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var materialConsumptions = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            materialConsumptions.Select(MaterialConsumptionResponse.FromEntity));
    }

    private static async Task<IResult> GetMaterialConsumptionsByJobIdAsync(
        string jobId,
        IMaterialConsumptionRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Results.BadRequest(
                new { message = "Job id is required." });
        }

        var materialConsumptions = await repository.GetByJobIdAsync(
            jobId,
            cancellationToken);

        return Results.Ok(
            materialConsumptions.Select(MaterialConsumptionResponse.FromEntity));
    }

    private static async Task<IResult> CreateMaterialConsumptionAsync(
        CreateMaterialConsumptionRequest request,
        IMaterialConsumptionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var materialConsumption = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "MaterialConsumption",
            materialConsumption.Id,
            materialConsumption.MaterialId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/material-consumptions/{materialConsumption.Id}",
            MaterialConsumptionResponse.FromEntity(materialConsumption));
    }

    private static async Task<IResult> UpdateMaterialConsumptionAsync(
        string id,
        UpdateMaterialConsumptionRequest request,
        IMaterialConsumptionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid material id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var materialConsumption = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (materialConsumption is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(materialConsumption);

        var updated = await repository.UpdateAsync(
            id,
            materialConsumption,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaterialConsumption",
            materialConsumption.Id,
            materialConsumption.MaterialId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(MaterialConsumptionResponse.FromEntity(materialConsumption));
    }

    private static async Task<IResult> DeleteMaterialConsumptionAsync(
        string id,
        IMaterialConsumptionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid material id." });
        }

        var materialConsumption = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (materialConsumption is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaterialConsumption",
            materialConsumption.Id,
            materialConsumption.MaterialId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
