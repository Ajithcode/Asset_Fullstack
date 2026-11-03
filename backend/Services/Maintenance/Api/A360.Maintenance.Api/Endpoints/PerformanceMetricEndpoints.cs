
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class PerformanceMetricEndpoints
{
    public static RouteGroupBuilder MapPerformanceMetricEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/performance-metrics")
            .WithTags("PerformanceMetrics");

        group.MapGet("", GetPerformanceMetricsAsync)
            .WithName("GetPerformanceMetrics");

        group.MapGet("/{id}", GetPerformanceMetricByIdAsync)
            .WithName("GetPerformanceMetricById");

        group.MapGet("/asset/{assetId}", GetPerformanceMetricsByAssetIdAsync)
            .WithName("GetPerformanceMetricsByAssetId");

        group.MapPost("", CreatePerformanceMetricAsync)
            .WithName("CreatePerformanceMetric");

        group.MapPut("/{id}", UpdatePerformanceMetricAsync)
            .WithName("UpdatePerformanceMetric");

        group.MapDelete("/{id}", DeletePerformanceMetricAsync)
            .WithName("DeletePerformanceMetric");

        return group;
    }

    private static async Task<IResult> GetPerformanceMetricsAsync(
        IPerformanceMetricRepository repository,
        CancellationToken cancellationToken)
    {
        var performanceMetrics = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            performanceMetrics.Select(PerformanceMetricResponse.FromEntity));
    }

    private static async Task<IResult> GetPerformanceMetricByIdAsync(
        string id,
        IPerformanceMetricRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid performancemetrics id." });
        }

        var performanceMetric = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (performanceMetric is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PerformanceMetric",
            performanceMetric.Id,
            performanceMetric.AssetName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PerformanceMetricResponse.FromEntity(performanceMetric));
    }

    private static async Task<IResult> GetPerformanceMetricsByAssetIdAsync(
        string assetId,
        IPerformanceMetricRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var performanceMetrics = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            performanceMetrics.Select(PerformanceMetricResponse.FromEntity));
    }

    private static async Task<IResult> CreatePerformanceMetricAsync(
        CreatePerformanceMetricRequest request,
        IPerformanceMetricRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var performanceMetric = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "PerformanceMetric",
            performanceMetric.Id,
            performanceMetric.AssetName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/performance-metrics/{performanceMetric.Id}",
            PerformanceMetricResponse.FromEntity(performanceMetric));
    }

    private static async Task<IResult> UpdatePerformanceMetricAsync(
        string id,
        UpdatePerformanceMetricRequest request,
        IPerformanceMetricRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid performancemetrics id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var performanceMetric = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (performanceMetric is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(performanceMetric);

        var updated = await repository.UpdateAsync(
            id,
            performanceMetric,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PerformanceMetric",
            performanceMetric.Id,
            performanceMetric.AssetName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PerformanceMetricResponse.FromEntity(performanceMetric));
    }

    private static async Task<IResult> DeletePerformanceMetricAsync(
        string id,
        IPerformanceMetricRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid performancemetrics id." });
        }

        var performanceMetric = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (performanceMetric is null)
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
            "PerformanceMetric",
            performanceMetric.Id,
            performanceMetric.AssetName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
