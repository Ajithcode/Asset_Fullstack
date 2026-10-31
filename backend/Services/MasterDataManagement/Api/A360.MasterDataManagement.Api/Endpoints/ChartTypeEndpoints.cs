using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ChartTypeEndpoints
{
    public static RouteGroupBuilder MapChartTypeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/chart-types")
            .WithTags("ChartTypes");

        group.MapGet("", GetChartTypesAsync)
            .WithName("GetChartTypes");

        group.MapGet("/{id}", GetChartTypeByIdAsync)
            .WithName("GetChartTypeById");

        group.MapPost("", CreateChartTypeAsync)
            .WithName("CreateChartType");

        group.MapPut("/{id}", UpdateChartTypeAsync)
            .WithName("UpdateChartType");

        group.MapPatch("/{id}/status", UpdateChartTypeStatusAsync)
            .WithName("UpdateChartTypeStatus");

        group.MapDelete("/{id}", DeleteChartTypeAsync)
            .WithName("DeleteChartType");

        return group;
    }

    private static async Task<IResult> GetChartTypesAsync(
        string? search,
        string? status,
        string? category,
        string? clientId,
        IChartTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var chartTypes = await repository.SearchAsync(
            new ChartTypeFilter(search, status, category, clientId),
            cancellationToken);

        return Results.Ok(
            chartTypes.Select(ChartTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetChartTypeByIdAsync(
        string id,
        IChartTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid chart type id." });
        }

        var chartType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (chartType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChartType",
            chartType.Id,
            chartType.ChartTypeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ChartTypeResponse.FromEntity(chartType));
    }

    private static async Task<IResult> CreateChartTypeAsync(
        CreateChartTypeRequest request,
        IChartTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var chartType = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            chartType.Code,
            chartType.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A chart type with code '{chartType.Code}' already exists." });
        }

        chartType = await repository.CreateAsync(
            chartType,
            cancellationToken);

        await eventLogger.LogAsync(
            "ChartType",
            chartType.Id,
            chartType.ChartTypeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/chart-types/{chartType.Id}",
            ChartTypeResponse.FromEntity(chartType));
    }

    private static async Task<IResult> UpdateChartTypeAsync(
        string id,
        UpdateChartTypeRequest request,
        IChartTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid chart type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var chartType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (chartType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(chartType);

        var existing = await repository.GetByCodeAsync(
            chartType.Code,
            chartType.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A chart type with code '{chartType.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            chartType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChartType",
            chartType.Id,
            chartType.ChartTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ChartTypeResponse.FromEntity(chartType));
    }

    private static async Task<IResult> UpdateChartTypeStatusAsync(
        string id,
        UpdateChartTypeStatusRequest request,
        IChartTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid chart type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var chartType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (chartType is null)
        {
            return Results.NotFound();
        }

        chartType.Status = request.Status;
        chartType.UpdatedBy = request.UpdatedBy;
        chartType.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            chartType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ChartType",
            chartType.Id,
            chartType.ChartTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ChartTypeResponse.FromEntity(chartType));
    }

    private static async Task<IResult> DeleteChartTypeAsync(
        string id,
        IChartTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid chart type id." });
        }

        var chartType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (chartType is null)
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
            "ChartType",
            chartType.Id,
            chartType.ChartTypeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
