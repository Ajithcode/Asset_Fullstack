using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ApiSyncStatusEndpoints
{
    public static RouteGroupBuilder MapApiSyncStatusEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/api-sync-statuses")
            .WithTags("ApiSyncStatuses");

        group.MapGet("", GetApiSyncStatusesAsync)
            .WithName("GetApiSyncStatuses");

        group.MapGet("/{id}", GetApiSyncStatusByIdAsync)
            .WithName("GetApiSyncStatusById");

        group.MapPost("", CreateApiSyncStatusAsync)
            .WithName("CreateApiSyncStatus");

        group.MapPut("/{id}", UpdateApiSyncStatusAsync)
            .WithName("UpdateApiSyncStatus");

        group.MapPatch("/{id}/status", UpdateApiSyncStatusStatusAsync)
            .WithName("UpdateApiSyncStatusStatus");

        group.MapDelete("/{id}", DeleteApiSyncStatusAsync)
            .WithName("DeleteApiSyncStatus");

        return group;
    }

    private static async Task<IResult> GetApiSyncStatusesAsync(
        string? search,
        string? status,
        bool? isSuccess,
        string? clientId,
        IApiSyncStatusRepository repository,
        CancellationToken cancellationToken)
    {
        var apiSyncStatuses = await repository.SearchAsync(
            new ApiSyncStatusFilter(search, status, isSuccess, clientId),
            cancellationToken);

        return Results.Ok(
            apiSyncStatuses.Select(ApiSyncStatusResponse.FromEntity));
    }

    private static async Task<IResult> GetApiSyncStatusByIdAsync(
        string id,
        IApiSyncStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid API sync status id." });
        }

        var apiSyncStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (apiSyncStatus is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ApiSyncStatus",
            apiSyncStatus.Id,
            apiSyncStatus.StatusName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ApiSyncStatusResponse.FromEntity(apiSyncStatus));
    }

    private static async Task<IResult> CreateApiSyncStatusAsync(
        CreateApiSyncStatusRequest request,
        IApiSyncStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var apiSyncStatus = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            apiSyncStatus.Code,
            apiSyncStatus.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An API sync status with code '{apiSyncStatus.Code}' already exists." });
        }

        apiSyncStatus = await repository.CreateAsync(
            apiSyncStatus,
            cancellationToken);

        await eventLogger.LogAsync(
            "ApiSyncStatus",
            apiSyncStatus.Id,
            apiSyncStatus.StatusName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/api-sync-statuses/{apiSyncStatus.Id}",
            ApiSyncStatusResponse.FromEntity(apiSyncStatus));
    }

    private static async Task<IResult> UpdateApiSyncStatusAsync(
        string id,
        UpdateApiSyncStatusRequest request,
        IApiSyncStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid API sync status id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var apiSyncStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (apiSyncStatus is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(apiSyncStatus);

        var existing = await repository.GetByCodeAsync(
            apiSyncStatus.Code,
            apiSyncStatus.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An API sync status with code '{apiSyncStatus.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            apiSyncStatus,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ApiSyncStatus",
            apiSyncStatus.Id,
            apiSyncStatus.StatusName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ApiSyncStatusResponse.FromEntity(apiSyncStatus));
    }

    private static async Task<IResult> UpdateApiSyncStatusStatusAsync(
        string id,
        UpdateApiSyncStatusStatusRequest request,
        IApiSyncStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid API sync status id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var apiSyncStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (apiSyncStatus is null)
        {
            return Results.NotFound();
        }

        apiSyncStatus.Status = request.Status;
        apiSyncStatus.UpdatedBy = request.UpdatedBy;
        apiSyncStatus.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            apiSyncStatus,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ApiSyncStatus",
            apiSyncStatus.Id,
            apiSyncStatus.StatusName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ApiSyncStatusResponse.FromEntity(apiSyncStatus));
    }

    private static async Task<IResult> DeleteApiSyncStatusAsync(
        string id,
        IApiSyncStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid API sync status id." });
        }

        var apiSyncStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (apiSyncStatus is null)
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
            "ApiSyncStatus",
            apiSyncStatus.Id,
            apiSyncStatus.StatusName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
