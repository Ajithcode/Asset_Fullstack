using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Asset Lifecycle page: stage changes (Edit Stage, Move to Next Stage) and manual events of a registered asset.
/// </summary>
/// <remarks>
/// The rest of the page is read from existing records: the registration (purchase, warranty, location),
/// assignments, condition assessments, service requests, disposals and Maintenance work orders.
/// The registration itself is never changed here.
/// </remarks>
public static class AssetLifecycleEndpoints
{
    private const string EntityType = "AssetLifecycle";
    private const int TitleMaxLength = 200;
    private const int TextMaxLength = 500;

    public static RouteGroupBuilder MapAssetLifecycleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-lifecycle").WithTags("Asset Lifecycle");

        group.MapGet("/{assetRegistrationId}", GetAsync).WithName("GetAssetLifecycle");
        group.MapPut("/{assetRegistrationId}/stages/{stageKey}", SaveStageAsync).WithName("SaveAssetLifecycleStage");
        group.MapPost("/{assetRegistrationId}/events", AddEventAsync).WithName("AddAssetLifecycleEvent");

        return group;
    }

    /// <summary>Stored stages and events; empty lists while nothing has been recorded for the asset.</summary>
    private static async Task<IResult> GetAsync(
        string assetRegistrationId,
        IAssetLifecycleRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (record is not null)
        {
            return Results.Ok(AssetLifecycleResponse.FromEntity(record));
        }

        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        return asset is null
            ? AssetNotFound(assetRegistrationId)
            : Results.Ok(new AssetLifecycleResponse(asset.AssetRegistrationId, asset.BasicInformation.AssetTag, [], []));
    }

    private static async Task<IResult> SaveStageAsync(
        string assetRegistrationId,
        string stageKey,
        SaveAssetLifecycleStageRequest body,
        IAssetLifecycleRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var key = AssetLifecycleOptions.StageKeys.Normalize(stageKey);
        var status = AssetLifecycleOptions.StageStatuses.Normalize(body.Status);
        var previous = string.IsNullOrWhiteSpace(body.CompletePrevious) ? null : AssetLifecycleOptions.StageKeys.Normalize(body.CompletePrevious);
        var errors = new Dictionary<string, string[]>();
        if (key is null)
        {
            errors["StageKey"] = [$"StageKey must be one of: {string.Join(", ", AssetLifecycleOptions.StageKeys.All)}"];
        }
        if (status is null)
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AssetLifecycleOptions.StageStatuses.All)}"];
        }
        if (status is "Completed" or "Current" && body.Date is null)
        {
            errors["Date"] = ["Completed and current stages need a date"];
        }
        if (!string.IsNullOrWhiteSpace(body.CompletePrevious) && previous is null)
        {
            errors["CompletePrevious"] = [$"CompletePrevious must be one of: {string.Join(", ", AssetLifecycleOptions.StageKeys.All)}"];
        }
        if (body.Notes?.Trim().Length > TextMaxLength || body.Owner?.Trim().Length > TitleMaxLength)
        {
            errors["Notes"] = [$"Owner and notes cannot be longer than {TitleMaxLength} and {TextMaxLength} characters"];
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var (record, problem) = await GetOrNewAsync(assetRegistrationId, repository, assetRepository, currentUserAccessor, cancellationToken);
        if (record is null)
        {
            return problem!;
        }

        var user = currentUserAccessor.GetCurrentUser()?.UserName;
        var now = DateTime.UtcNow;
        var stage = record.Stages.FirstOrDefault(s => s.Key == key);
        var becameCurrent = status == "Current" && stage?.Status != "Current";

        // Only one stage is current: the one that was current is completed.
        if (status == "Current")
        {
            foreach (var other in record.Stages.Where(s => s.Key != key && s.Status == "Current"))
            {
                other.Status = "Completed";
                other.UpdatedBy = user;
                other.UpdatedAt = now;
            }
            if (previous is not null && previous != key)
            {
                var prior = record.Stages.FirstOrDefault(s => s.Key == previous);
                if (prior is null)
                {
                    record.Stages.Add(new AssetLifecycleStage { Key = previous, Status = "Completed", Date = body.Date, UpdatedBy = user, UpdatedAt = now });
                }
                else
                {
                    prior.Status = "Completed";
                    prior.UpdatedBy = user;
                    prior.UpdatedAt = now;
                }
            }
        }

        if (stage is null)
        {
            stage = new AssetLifecycleStage { Key = key! };
            record.Stages.Add(stage);
        }
        stage.Status = status!;
        stage.Date = body.Date?.Date;
        stage.Owner = body.Owner?.Trim();
        stage.Notes = body.Notes?.Trim();
        stage.UpdatedBy = user;
        stage.UpdatedAt = now;

        if (becameCurrent)
        {
            record.Events.Add(NewEvent(body.Date!.Value, $"Moved to {Label(key!)} stage", user, "Stage"));
        }

        await SaveAsync(record, user, repository, cancellationToken);
        await eventLogger.LogAsync(EntityType, record.Id, $"{record.AssetTag ?? record.AssetRegistrationId} - {Label(key!)} {status}", EventAction.Updated, cancellationToken);
        return Results.Ok(AssetLifecycleResponse.FromEntity(record));
    }

    private static async Task<IResult> AddEventAsync(
        string assetRegistrationId,
        AddAssetLifecycleEventRequest body,
        IAssetLifecycleRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var kind = AssetLifecycleOptions.EventKinds.Normalize(body.Kind ?? "Other");
        var errors = new Dictionary<string, string[]>();
        if (body.Date is null)
        {
            errors["Date"] = ["Date is required"];
        }
        if (string.IsNullOrWhiteSpace(body.Title))
        {
            errors["Title"] = ["Title is required"];
        }
        else if (body.Title.Trim().Length > TitleMaxLength)
        {
            errors["Title"] = [$"Title cannot be longer than {TitleMaxLength} characters"];
        }
        if (kind is null)
        {
            errors["Kind"] = [$"Kind must be one of: {string.Join(", ", AssetLifecycleOptions.EventKinds.All)}"];
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var (record, problem) = await GetOrNewAsync(assetRegistrationId, repository, assetRepository, currentUserAccessor, cancellationToken);
        if (record is null)
        {
            return problem!;
        }

        var user = currentUserAccessor.GetCurrentUser()?.UserName;
        var by = string.IsNullOrWhiteSpace(body.By) ? user : body.By.Trim();
        var added = NewEvent(body.Date!.Value, body.Title!.Trim(), by, kind!);
        record.Events.Add(added);

        await SaveAsync(record, user, repository, cancellationToken);
        await eventLogger.LogAsync(EntityType, record.Id, $"{record.AssetTag ?? record.AssetRegistrationId} - {added.Title}", EventAction.Updated, cancellationToken);
        return Results.Created($"/api/asset-lifecycle/{record.AssetRegistrationId}", AssetLifecycleResponse.FromEntity(record));
    }

    private static async Task<(AssetLifecycleRecord? Record, IResult? Problem)> GetOrNewAsync(
        string assetRegistrationId,
        IAssetLifecycleRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (record is not null)
        {
            return (record, null);
        }

        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null)
        {
            return (null, AssetNotFound(assetRegistrationId));
        }

        var user = currentUserAccessor.GetCurrentUser();
        return (new AssetLifecycleRecord
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetTag = asset.BasicInformation.AssetTag,
            CreatedBy = user?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = user?.ClientId ?? asset.ClientId,
            TenantId = user?.TenantId ?? asset.TenantId,
            IsDeleted = false
        }, null);
    }

    private static async Task SaveAsync(AssetLifecycleRecord record, string? user, IAssetLifecycleRepository repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Id))
        {
            await repository.CreateAsync(record, cancellationToken);
            return;
        }

        record.UpdatedBy = user;
        record.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(record.Id, record, cancellationToken);
    }

    private static AssetLifecycleEvent NewEvent(DateTime date, string title, string? by, string kind) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        Date = date.Date,
        Title = title,
        By = by,
        Kind = kind,
        CreatedAt = DateTime.UtcNow
    };

    private static string Label(string key) => key switch
    {
        "operation" => "In Operation",
        _ => char.ToUpperInvariant(key[0]) + key[1..]
    };

    private static IResult AssetNotFound(string assetRegistrationId) =>
        Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });
}
