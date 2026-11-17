using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Contracts;

// ---------------------------------------------------------------- Requests

/// <summary>
/// The Register Tag ID form. The tag id is the technology prefix plus <see cref="TagNumber"/> (BLE + 002952 → BLE-002952).
/// <see cref="Value"/> is the MAC address (BLE), EPC (RFID), encoded value (barcode / QR) or UID (NFC).
/// Leave <see cref="AssetCode"/> empty to keep the tag in stock as Available.
/// </summary>
public sealed record RegisterTagRequest(
    string? Technology,
    string? TagType,
    string? TagNumber,
    string? Value,
    string? FirmwareVersion,
    string? SiteId,
    string? SiteName,
    string? LocationName,
    string? AssetCode);

/// <summary>Edits a tag's details. The tag id, technology and assignment are changed with their own endpoints.</summary>
public sealed record UpdateTagRequest(
    string? TagType,
    string? Value,
    string? FirmwareVersion,
    string? SiteId,
    string? SiteName,
    string? LocationName);

/// <summary>Links the tag to an asset (registration id or asset code), reassigning it if it is linked to another one.</summary>
public sealed record AssignTagRequest(
    string? AssetCode,
    string? SiteId,
    string? SiteName,
    string? LocationName,
    string? Note);

public sealed record UnassignTagRequest(string? Reason);

public sealed record DeactivateTagRequest(string? Reason);

/// <summary>A read of the tag by a gateway, portal or handheld scanner.</summary>
public sealed record RecordTagReadRequest(
    string? ReadBy,
    DateTime? ReadAt,
    int? BatteryPercent,
    int? SignalDbm,
    string? SiteId,
    string? SiteName,
    string? LocationName);

/// <summary>Query string of the Tag IDs list (and of /assignments and /available).</summary>
public sealed class TagListQuery
{
    /// <summary>Matches tag id, value (MAC / EPC / barcode), asset code or name, site or location.</summary>
    public string? Search { get; set; }
    public string? Technology { get; set; }
    public string? TagType { get; set; }
    public string? Status { get; set; }

    /// <summary>Valid or Invalid.</summary>
    public string? Validation { get; set; }
    public string? SiteId { get; set; }

    /// <summary>1-based; default 1.</summary>
    public int? Page { get; set; }

    /// <summary>1-200; default 10.</summary>
    public int? PageSize { get; set; }
}

/// <summary>Query string of the Audit Trail tab.</summary>
public sealed class TagAuditQuery
{
    /// <summary>Matches tag id, asset or the event details.</summary>
    public string? Search { get; set; }
    public string? EventType { get; set; }
    public string? Technology { get; set; }
    public string? PerformedBy { get; set; }

    /// <summary>One of <see cref="TagAuditPeriod.All"/>; ignored when From or To is given.</summary>
    public string? Period { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public static class TagAuditPeriod
{
    public const string Today = "today";
    public const string Last7Days = "7d";
    public const string Last30Days = "30d";
    public const string Last90Days = "90d";
    public const string AllTime = "all";

    public static readonly IReadOnlyList<string> All = [Today, Last7Days, Last30Days, Last90Days, AllTime];

    /// <summary>The start of the period in UTC, or null for all time.</summary>
    public static DateTime? StartOf(string? period, DateTime nowUtc)
    {
        return period?.Trim().ToLowerInvariant() switch
        {
            Today => nowUtc.Date,
            Last7Days => nowUtc.AddDays(-7),
            Last30Days => nowUtc.AddDays(-30),
            Last90Days => nowUtc.AddDays(-90),
            _ => null
        };
    }
}

// ---------------------------------------------------------------- Responses

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize, int TotalPages)
{
    public static PagedResponse<T> Of(IReadOnlyList<T> items, long total, int page, int pageSize)
    {
        return new PagedResponse<T>(items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }
}

/// <summary>One stat card: the count now, the count a month ago and the change in percent (null when there were none).</summary>
public sealed record TagCountResponse(int Count, int LastMonthCount, double? ChangePercent);

/// <summary>The four stat cards and the "IDs failing validation" badge.</summary>
public sealed record TagSummaryResponse(
    TagCountResponse Total,
    TagCountResponse Assigned,
    TagCountResponse Available,
    TagCountResponse Inactive,
    int FailingValidation,
    IReadOnlyDictionary<string, int> ByTechnology);

/// <summary>A row of the Tag IDs tab. <see cref="ValueLabel"/> names the value: MAC Address, EPC, Encoded Value or UID.</summary>
public sealed record TagListItemResponse(
    string TagId,
    string Technology,
    string TagType,
    string ValueLabel,
    string? Value,
    DateTime? RegisteredOn,
    string Validation,
    string? ValidationMessage,
    string? Status,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? LocationName,
    DateTime? AssignedAt,
    DateTime? LastSeenAt);

public sealed record LinkedAssetResponse(
    string AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    DateTime? AssignedAt,
    string? AssignedBy);

public sealed record TagLocationResponse(
    string? SiteId,
    string? SiteName,
    string? LocationName,
    DateTime? LastSeenAt,
    string? LastSeenBy);

/// <summary>Battery & Connectivity; <see cref="SignalQuality"/> is Excellent, Good, Fair or Poor.</summary>
public sealed record TagConnectivityResponse(int? BatteryPercent, int? SignalDbm, string? SignalQuality);

/// <summary>The tag side panel: Details tab with its linked asset, location, battery and assignment history.</summary>
public sealed record TagDetailsResponse(
    string Id,
    string TagId,
    string TagNumber,
    string? Status,
    string Technology,
    string TagType,
    string ValueLabel,
    string? Value,
    string? FirmwareVersion,
    DateTime? RegisteredOn,
    string? RegisteredBy,
    string Validation,
    string? ValidationMessage,
    string? InactiveReason,
    LinkedAssetResponse? LinkedAsset,
    TagLocationResponse Location,
    TagConnectivityResponse? Connectivity,
    IReadOnlyList<TagEventResponse> AssignmentHistory);

/// <summary>A row of the Audit Trail: When, Tag ID, Event, Details, Performed By.</summary>
public sealed record TagEventResponse(
    string Id,
    DateTime When,
    string TagId,
    string Technology,
    string Event,
    string Details,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? StatusAfter,
    string PerformedBy)
{
    public static TagEventResponse FromEntity(TagEvent tagEvent) => new(
        tagEvent.Id,
        tagEvent.OccurredAt,
        tagEvent.TagId,
        tagEvent.Technology,
        tagEvent.EventType,
        tagEvent.Details,
        tagEvent.AssetRegistrationId,
        tagEvent.AssetCode,
        tagEvent.AssetName,
        tagEvent.StatusAfter,
        tagEvent.PerformedBy);
}

/// <summary>An option of the Technology dropdown, with the tag types it offers, its tag id prefix and value label.</summary>
public sealed record TagTechnologyOptionResponse(
    string Technology,
    string IdPrefix,
    string ValueLabel,
    IReadOnlyList<string> TagTypes)
{
    public static TagTechnologyOptionResponse FromTechnology(string technology) => new(
        technology,
        TagTechnology.IdPrefixes[technology],
        TagTechnology.ValueLabels[technology],
        TagTechnology.TagTypes[technology]);
}

/// <summary>Every dropdown of the Tag IDs and Assignments tabs and of the Register Tag ID form.</summary>
public sealed record TagOptionsResponse(
    IReadOnlyList<TagTechnologyOptionResponse> Technologies,
    IReadOnlyList<string> TagTypes,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> ValidationResults);

/// <summary>The dropdowns of the Audit Trail tab.</summary>
public sealed record TagAuditOptionsResponse(
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> PerformedBy,
    IReadOnlyList<string> Periods);

/// <summary>The next free tag number for a technology, to prefill the Register form.</summary>
public sealed record NextTagNumberResponse(string Technology, string Prefix, string TagNumber, string TagId);

// ---------------------------------------------------------------- Mapping

internal static class TagMappings
{
    public static string ValueLabelOf(string technology)
    {
        return TagTechnology.ValueLabels.TryGetValue(technology, out var label) ? label : "Value";
    }

    public static TagListItemResponse ToListItem(this TrackingTag tag, TagValidationResult validation) => new(
        tag.TagId,
        tag.Technology,
        tag.TagType,
        ValueLabelOf(tag.Technology),
        tag.Value,
        tag.CreatedAt,
        validation.Result,
        validation.Message,
        tag.Status,
        tag.AssetRegistrationId,
        tag.AssetCode,
        tag.AssetName,
        tag.SiteId,
        tag.SiteName,
        tag.LocationName,
        tag.AssignedAt,
        tag.LastSeenAt);

    public static TagDetailsResponse ToDetails(
        this TrackingTag tag,
        TagValidationResult validation,
        IReadOnlyList<TagEvent> assignmentHistory) => new(
        tag.Id,
        tag.TagId,
        tag.TagNumber,
        tag.Status,
        tag.Technology,
        tag.TagType,
        ValueLabelOf(tag.Technology),
        tag.Value,
        tag.FirmwareVersion,
        tag.CreatedAt,
        tag.CreatedBy,
        validation.Result,
        validation.Message,
        tag.InactiveReason,
        tag.AssetRegistrationId is null
            ? null
            : new LinkedAssetResponse(tag.AssetRegistrationId, tag.AssetCode, tag.AssetName, tag.AssignedAt, tag.AssignedBy),
        new TagLocationResponse(tag.SiteId, tag.SiteName, tag.LocationName, tag.LastSeenAt, tag.LastSeenBy),
        TagTechnology.Powered.Contains(tag.Technology) || tag.BatteryPercent is not null || tag.SignalDbm is not null
            ? new TagConnectivityResponse(tag.BatteryPercent, tag.SignalDbm, SignalQualityOf(tag.SignalDbm))
            : null,
        assignmentHistory.Select(TagEventResponse.FromEntity).ToList());

    /// <summary>How the asset is named in event details, e.g. "Chiller CH-011".</summary>
    public static string AssetDisplay(string? assetName, string? assetCode, string? assetRegistrationId)
    {
        return assetName ?? assetCode ?? assetRegistrationId ?? "asset";
    }

    public static string AssetDisplay(TrackingTag tag) => AssetDisplay(tag.AssetName, tag.AssetCode, tag.AssetRegistrationId);

    public static string AssetDisplay(TagAsset asset) => AssetDisplay(asset.AssetName, asset.AssetCode, asset.AssetRegistrationId);

    private static string? SignalQualityOf(int? signalDbm)
    {
        return signalDbm switch
        {
            null => null,
            >= -55 => "Excellent",
            >= -70 => "Good",
            >= -85 => "Fair",
            _ => "Poor"
        };
    }
}
