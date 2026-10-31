
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class KpiMasterEndpoints
{
    public static RouteGroupBuilder MapKpiMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/kpi-masters")
            .WithTags("KpiMasters");

        group.MapGet("", GetKpiMastersAsync)
            .WithName("GetKpiMasters");

        group.MapGet("/{id}", GetKpiMasterByIdAsync)
            .WithName("GetKpiMasterById");

        group.MapGet("/asset/{assetId}", GetKpiMastersByAssetIdAsync)
            .WithName("GetKpiMastersByAssetId");

        group.MapPost("", CreateKpiMasterAsync)
            .WithName("CreateKpiMaster");

        group.MapPut("/{id}", UpdateKpiMasterAsync)
            .WithName("UpdateKpiMaster");

        group.MapDelete("/{id}", DeleteKpiMasterAsync)
            .WithName("DeleteKpiMaster");

        return group;
    }

    private static async Task<IResult> GetKpiMastersAsync(
        IKpiMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var kpiMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            kpiMasters.Select(KpiMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetKpiMasterByIdAsync(
        string id,
        IKpiMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid kpi id." });
        }

        var kpiMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (kpiMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "KpiMaster",
            kpiMaster.Id,
            kpiMaster.KpiId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(KpiMasterResponse.FromEntity(kpiMaster));
    }

    private static async Task<IResult> GetKpiMastersByAssetIdAsync(
        string assetId,
        IKpiMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var kpiMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            kpiMasters.Select(KpiMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateKpiMasterAsync(
        CreateKpiMasterRequest request,
        IKpiMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var kpiMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "KpiMaster",
            kpiMaster.Id,
            kpiMaster.KpiId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/kpi-masters/{kpiMaster.Id}",
            KpiMasterResponse.FromEntity(kpiMaster));
    }

    private static async Task<IResult> UpdateKpiMasterAsync(
        string id,
        UpdateKpiMasterRequest request,
        IKpiMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid kpi id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var kpiMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (kpiMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(kpiMaster);

        var updated = await repository.UpdateAsync(
            id,
            kpiMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "KpiMaster",
            kpiMaster.Id,
            kpiMaster.KpiId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(KpiMasterResponse.FromEntity(kpiMaster));
    }

    private static async Task<IResult> DeleteKpiMasterAsync(
        string id,
        IKpiMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid kpi id." });
        }

        var kpiMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (kpiMaster is null)
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
            "KpiMaster",
            kpiMaster.Id,
            kpiMaster.KpiId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
