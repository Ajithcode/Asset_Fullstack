using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

public static class AssetLifecycleOptions
{
    public static readonly OptionList StageKeys = new(
        "planning", "procurement", "received", "installation", "commissioning",
        "operation", "maintenance", "upgrade", "decommissioning", "disposal");

    public static readonly OptionList StageStatuses = new("Completed", "Current", "Planned", "Not Started");

    public static readonly OptionList EventKinds = new(
        "Procurement", "Installation", "Operation", "Maintenance", "Repair", "Condition", "Stage", "Other");
}

/// <summary>Edit Stage / Move to Next Stage.</summary>
/// <param name="Status">Completed, Current, Planned or Not Started. Making a stage Current completes the stage that was current.</param>
/// <param name="CompletePrevious">Key of the stage that was current (worked out on the page), stored as Completed.</param>
public sealed record SaveAssetLifecycleStageRequest(
    string? Status,
    DateTime? Date,
    string? Owner,
    string? Notes,
    string? CompletePrevious);

/// <summary>Add Event.</summary>
public sealed record AddAssetLifecycleEventRequest(
    DateTime? Date,
    string? Title,
    string? By,
    string? Kind);

public sealed record AssetLifecycleStageResponse(string Key, string Status, DateTime? Date, string? Owner, string? Notes, string? UpdatedBy, DateTime UpdatedAt)
{
    public static AssetLifecycleStageResponse FromEntity(AssetLifecycleStage stage) =>
        new(stage.Key, stage.Status, stage.Date, stage.Owner, stage.Notes, stage.UpdatedBy, stage.UpdatedAt);
}

public sealed record AssetLifecycleEventResponse(string EventId, DateTime Date, string Title, string? By, string Kind, DateTime CreatedAt)
{
    public static AssetLifecycleEventResponse FromEntity(AssetLifecycleEvent e) => new(e.EventId, e.Date, e.Title, e.By, e.Kind, e.CreatedAt);
}

/// <summary>Stored stage changes and events; stages not listed are worked out from the asset's own records.</summary>
public sealed record AssetLifecycleResponse(
    string AssetRegistrationId,
    string? AssetTag,
    IReadOnlyCollection<AssetLifecycleStageResponse> Stages,
    IReadOnlyCollection<AssetLifecycleEventResponse> Events)
{
    public static AssetLifecycleResponse FromEntity(AssetLifecycleRecord record) => new(
        record.AssetRegistrationId,
        record.AssetTag,
        record.Stages.Select(AssetLifecycleStageResponse.FromEntity).ToList(),
        record.Events.OrderBy(e => e.Date).ThenBy(e => e.CreatedAt).Select(AssetLifecycleEventResponse.FromEntity).ToList());
}
