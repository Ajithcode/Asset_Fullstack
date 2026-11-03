using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>IoT Tracking Dashboard → Utilization Rate (by Asset Type): daily running / idle time per asset, addressed by id (e.g. UT000001).</summary>
public static class AssetUtilizationEndpoints
{
    private const string EntityType = "AssetUtilization";
    private const string SequenceName = "AssetUtilization";
    private const string UtilizationIdPrefix = "UT";

    public static RouteGroupBuilder MapAssetUtilizationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-utilization").WithTags("Asset Utilization");

        group.MapGet("", GetUtilizationAsync).WithName("GetAssetUtilization");
        group.MapGet("/by-asset-type", GetUtilizationByAssetTypeAsync).WithName("GetAssetUtilizationByAssetType");
        group.MapGet("/{utilizationId}", GetUtilizationRecordAsync).WithName("GetAssetUtilizationById");
        group.MapPost("", CreateUtilizationAsync).WithName("CreateAssetUtilization");
        group.MapPut("/{utilizationId}", UpdateUtilizationAsync).WithName("UpdateAssetUtilization");
        group.MapDelete("/{utilizationId}", DeleteUtilizationAsync).WithName("DeleteAssetUtilization");

        return group;
    }

    /// <summary>Daily records of the last <paramref name="days"/> days (default 7), newest day first.</summary>
    private static async Task<IResult> GetUtilizationAsync(
        IAssetUtilizationRepository repository,
        CancellationToken cancellationToken,
        int? days = null,
        string? assetType = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = DashboardPeriod.LastDays(days);
        var records = await repository.GetInRangeAsync(period.FromUtc, period.ToUtc, assetType, cancellationToken);
        return Results.Ok(records.Select(AssetUtilizationResponse.FromEntity).ToList());
    }

    /// <summary>The dashboard bars: running % and idle % per asset type over the last <paramref name="days"/> days (default 7).</summary>
    private static async Task<IResult> GetUtilizationByAssetTypeAsync(
        IAssetUtilizationRepository repository,
        CancellationToken cancellationToken,
        int? days = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = DashboardPeriod.LastDays(days);
        var records = await repository.GetInRangeAsync(period.FromUtc, period.ToUtc, cancellationToken: cancellationToken);
        return Results.Ok(UtilizationByAssetTypeResponse.FromRecords(period, records));
    }

    private static async Task<IResult> GetUtilizationRecordAsync(
        string utilizationId,
        IAssetUtilizationRepository repository,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetByUtilizationIdAsync(utilizationId.Trim(), cancellationToken);
        return record is null ? NotFound(utilizationId) : Results.Ok(AssetUtilizationResponse.FromEntity(record));
    }

    /// <summary>One record per asset per day: a second record for the same asset and day is a 409; update that one instead.</summary>
    private static async Task<IResult> CreateUtilizationAsync(
        CreateAssetUtilizationRequest request,
        IAssetUtilizationRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var dateUtc = DateTime.SpecifyKind(request.DateUtc, DateTimeKind.Utc);
        var duplicate = await repository.GetByAssetAndDateAsync(request.AssetRegistrationId!.Trim(), dateUtc, cancellationToken);
        if (duplicate is not null)
        {
            return Results.Conflict(new
            {
                message = $"Asset '{duplicate.AssetRegistrationId}' already has utilization for {dateUtc:yyyy-MM-dd} ({duplicate.UtilizationId}); update that record instead."
            });
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var record = request.ToEntity($"{UtilizationIdPrefix}{nextSequence:D6}", currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(record, cancellationToken);
        await eventLogger.LogAsync(EntityType, record.UtilizationId, Describe(record.AssetRegistrationId, record.Date), EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-utilization/{record.UtilizationId}", AssetUtilizationResponse.FromEntity(record));
    }

    /// <summary>Takes the same body as create; the record keeps its id. Moving it onto another record's asset and day is a 409.</summary>
    private static async Task<IResult> UpdateUtilizationAsync(
        string utilizationId,
        CreateAssetUtilizationRequest request,
        IAssetUtilizationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByUtilizationIdAsync(utilizationId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(utilizationId);
        }

        var dateUtc = DateTime.SpecifyKind(request.DateUtc, DateTimeKind.Utc);
        var duplicate = await repository.GetByAssetAndDateAsync(request.AssetRegistrationId!.Trim(), dateUtc, cancellationToken);
        if (duplicate is not null && duplicate.Id != existing.Id)
        {
            return Results.Conflict(new
            {
                message = $"Asset '{duplicate.AssetRegistrationId}' already has utilization for {dateUtc:yyyy-MM-dd} ({duplicate.UtilizationId})."
            });
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var record = request.ToEntity(existing.UtilizationId, currentUser).Replacing(existing, currentUser);

        if (!await repository.UpdateAsync(existing.Id, record, cancellationToken))
        {
            return NotFound(utilizationId);
        }

        await eventLogger.LogAsync(EntityType, record.UtilizationId, Describe(record.AssetRegistrationId, record.Date), EventAction.Updated, cancellationToken);
        return Results.Ok(AssetUtilizationResponse.FromEntity(record));
    }

    private static async Task<IResult> DeleteUtilizationAsync(
        string utilizationId,
        IAssetUtilizationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetByUtilizationIdAsync(utilizationId.Trim(), cancellationToken);
        if (record is null || !await repository.DeleteAsync(record.Id, cancellationToken))
        {
            return NotFound(utilizationId);
        }

        await eventLogger.LogAsync(EntityType, record.UtilizationId, Describe(record.AssetRegistrationId, record.Date), EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static string Describe(string assetRegistrationId, DateTime date) => $"{assetRegistrationId} - {date:yyyy-MM-dd}";

    private static IResult NotFound(string utilizationId)
    {
        return Results.NotFound(new { message = $"Asset utilization '{utilizationId}' was not found." });
    }
}
