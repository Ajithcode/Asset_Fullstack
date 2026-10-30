
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class PermitMasterEndpoints
{
    public static RouteGroupBuilder MapPermitMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/permit-masters")
            .WithTags("PermitMasters");

        group.MapGet("", GetPermitMastersAsync)
            .WithName("GetPermitMasters");

        group.MapGet("/{id}", GetPermitMasterByIdAsync)
            .WithName("GetPermitMasterById");

        group.MapGet("/asset/{assetId}", GetPermitMastersByAssetIdAsync)
            .WithName("GetPermitMastersByAssetId");

        group.MapGet("/job/{jobId}", GetPermitMastersByJobIdAsync)
            .WithName("GetPermitMastersByJobId");

        group.MapPost("", CreatePermitMasterAsync)
            .WithName("CreatePermitMaster");

        group.MapPut("/{id}", UpdatePermitMasterAsync)
            .WithName("UpdatePermitMaster");

        group.MapDelete("/{id}", DeletePermitMasterAsync)
            .WithName("DeletePermitMaster");

        return group;
    }

    private static async Task<IResult> GetPermitMastersAsync(
        IPermitMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var permitMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            permitMasters.Select(PermitMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetPermitMasterByIdAsync(
        string id,
        IPermitMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit id." });
        }

        var permitMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PermitMaster",
            permitMaster.Id,
            permitMaster.PermitId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PermitMasterResponse.FromEntity(permitMaster));
    }

    private static async Task<IResult> GetPermitMastersByAssetIdAsync(
        string assetId,
        IPermitMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var permitMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            permitMasters.Select(PermitMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetPermitMastersByJobIdAsync(
        string jobId,
        IPermitMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Results.BadRequest(
                new { message = "Job id is required." });
        }

        var permitMasters = await repository.GetByJobIdAsync(
            jobId,
            cancellationToken);

        return Results.Ok(
            permitMasters.Select(PermitMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreatePermitMasterAsync(
        CreatePermitMasterRequest request,
        IPermitMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permitMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "PermitMaster",
            permitMaster.Id,
            permitMaster.PermitId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/permit-masters/{permitMaster.Id}",
            PermitMasterResponse.FromEntity(permitMaster));
    }

    private static async Task<IResult> UpdatePermitMasterAsync(
        string id,
        UpdatePermitMasterRequest request,
        IPermitMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permitMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(permitMaster);

        var updated = await repository.UpdateAsync(
            id,
            permitMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PermitMaster",
            permitMaster.Id,
            permitMaster.PermitId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PermitMasterResponse.FromEntity(permitMaster));
    }

    private static async Task<IResult> DeletePermitMasterAsync(
        string id,
        IPermitMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit id." });
        }

        var permitMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitMaster is null)
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
            "PermitMaster",
            permitMaster.Id,
            permitMaster.PermitId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
