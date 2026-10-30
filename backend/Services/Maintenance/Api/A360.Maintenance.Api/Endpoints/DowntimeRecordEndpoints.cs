
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class DowntimeRecordEndpoints
{
    public static RouteGroupBuilder MapDowntimeRecordEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/downtime-tracking")
            .WithTags("DowntimeTracking");

        group.MapGet("", GetDowntimeRecordsAsync)
            .WithName("GetDowntimeRecords");

        group.MapGet("/{id}", GetDowntimeRecordByIdAsync)
            .WithName("GetDowntimeRecordById");

        group.MapGet("/asset/{assetId}", GetDowntimeRecordsByAssetIdAsync)
            .WithName("GetDowntimeRecordsByAssetId");

        group.MapPost("", CreateDowntimeRecordAsync)
            .WithName("CreateDowntimeRecord");

        group.MapPut("/{id}", UpdateDowntimeRecordAsync)
            .WithName("UpdateDowntimeRecord");

        group.MapDelete("/{id}", DeleteDowntimeRecordAsync)
            .WithName("DeleteDowntimeRecord");

        return group;
    }

    private static async Task<IResult> GetDowntimeRecordsAsync(
        IDowntimeRecordRepository repository,
        CancellationToken cancellationToken)
    {
        var downtimeRecords = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            downtimeRecords.Select(DowntimeRecordResponse.FromEntity));
    }

    private static async Task<IResult> GetDowntimeRecordByIdAsync(
        string id,
        IDowntimeRecordRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid downtimetracking id." });
        }

        var downtimeRecord = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (downtimeRecord is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "DowntimeRecord",
            downtimeRecord.Id,
            downtimeRecord.AssetName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(DowntimeRecordResponse.FromEntity(downtimeRecord));
    }

    private static async Task<IResult> GetDowntimeRecordsByAssetIdAsync(
        string assetId,
        IDowntimeRecordRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var downtimeRecords = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            downtimeRecords.Select(DowntimeRecordResponse.FromEntity));
    }

    private static async Task<IResult> CreateDowntimeRecordAsync(
        CreateDowntimeRecordRequest request,
        IDowntimeRecordRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var downtimeRecord = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "DowntimeRecord",
            downtimeRecord.Id,
            downtimeRecord.AssetName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/downtime-tracking/{downtimeRecord.Id}",
            DowntimeRecordResponse.FromEntity(downtimeRecord));
    }

    private static async Task<IResult> UpdateDowntimeRecordAsync(
        string id,
        UpdateDowntimeRecordRequest request,
        IDowntimeRecordRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid downtimetracking id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var downtimeRecord = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (downtimeRecord is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(downtimeRecord);

        var updated = await repository.UpdateAsync(
            id,
            downtimeRecord,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "DowntimeRecord",
            downtimeRecord.Id,
            downtimeRecord.AssetName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(DowntimeRecordResponse.FromEntity(downtimeRecord));
    }

    private static async Task<IResult> DeleteDowntimeRecordAsync(
        string id,
        IDowntimeRecordRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid downtimetracking id." });
        }

        var downtimeRecord = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (downtimeRecord is null)
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
            "DowntimeRecord",
            downtimeRecord.Id,
            downtimeRecord.AssetName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
