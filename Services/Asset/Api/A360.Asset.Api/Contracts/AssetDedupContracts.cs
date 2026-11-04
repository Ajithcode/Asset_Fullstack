using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>Field keys used by the Asset Merge / Dedup screen.</summary>
public static class AssetDedupFields
{
    /// <summary>Fields duplicate detection can compare.</summary>
    public static readonly IReadOnlyList<string> Match = ["name", "serialNumber", "model", "manufacturer", "category", "location"];

    /// <summary>Fields whose value is chosen from one of the records when merging.</summary>
    public static readonly IReadOnlyList<string> Merge = ["name", "type", "model", "serialNumber", "category", "location", "status", "purchaseDate", "supplier", "value"];
}

// ===== Rules =====

public sealed record AssetDedupRulesRequest(
    List<string>? Fields,
    int High,
    int Medium,
    int Low,
    bool IncludeInactive);

public sealed record AssetDedupRulesResponse(
    IReadOnlyList<string> Fields,
    int High,
    int Medium,
    int Low,
    bool IncludeInactive,
    DateTime? LastScanAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static AssetDedupRulesResponse FromEntity(AssetDedupRules rules) => new(
        rules.Fields, rules.High, rules.Medium, rules.Low, rules.IncludeInactive, rules.LastScanAt, rules.UpdatedBy ?? rules.CreatedBy, rules.UpdatedAt ?? rules.CreatedAt);
}

// ===== Duplicate groups =====

/// <summary>One asset as compared and merged on the screen.</summary>
public sealed record AssetDedupRecordResponse(
    string Tag,
    string AssetRegistrationId,
    string Name,
    string Type,
    string Manufacturer,
    string Model,
    string SerialNumber,
    string Category,
    string Location,
    string Status,
    string PurchaseDate,
    string Supplier,
    decimal Value,
    string LastModified,
    string ModifiedBy,
    string Source,
    bool InRegister);

public sealed record AssetDedupMemberResponse(int Score, string? Confidence, AssetDedupRecordResponse Record);

/// <summary>A primary asset and the records that may duplicate it. GroupId is the primary asset's tag.</summary>
public sealed record AssetDedupGroupResponse(
    string GroupId,
    int MaxScore,
    string? Confidence,
    AssetDedupRecordResponse Primary,
    IReadOnlyList<AssetDedupMemberResponse> Members);

public sealed record AssetDedupCounts(int High, int Medium, int Low);

public sealed record AssetDedupGroupListResponse(
    IReadOnlyList<AssetDedupGroupResponse> Items,
    int Total,
    int Page,
    int PageSize,
    // Groups per confidence band (of the group's best match), before the filters
    AssetDedupCounts Counts,
    // Duplicate records across all groups, before the filters
    int Duplicates,
    DateTime? LastScanAt);

public sealed record AssetDedupScanResponse(DateTime ScannedAt, int Groups, int Duplicates, int High, int Medium, int Low);

// ===== Dismissals ("not a duplicate") =====

/// <summary>Tags[0] is the primary asset; it is marked as not duplicating each of the other tags.</summary>
public sealed record DismissAssetDuplicatesRequest(List<string>? Tags, string? Reason);

public sealed record AssetDedupDismissalResponse(
    string Id,
    IReadOnlyList<string> Tags,
    string? Reason,
    DateTime? DismissedAt,
    string? DismissedBy)
{
    public static AssetDedupDismissalResponse FromEntity(AssetDedupDismissal dismissal) => new(
        dismissal.Id, dismissal.Tags, dismissal.Reason, dismissal.CreatedAt, dismissal.CreatedBy);
}

// ===== Merges =====

/// <summary>
/// FieldSources: merge field → tag of the record whose value the primary asset keeps
/// (fields left out keep the primary asset's own value). Allowed fields: AssetDedupFields.Merge.
/// </summary>
public sealed record MergeAssetsRequest(
    string? PrimaryTag,
    List<string>? MergedTags,
    Dictionary<string, string>? FieldSources,
    string? Notes);

public sealed record AssetMergeResponse(
    string Id,
    string PrimaryTag,
    IReadOnlyList<string> MergedTags,
    IReadOnlyDictionary<string, string> FieldSources,
    // The values the primary asset kept
    IReadOnlyDictionary<string, string> Fields,
    string? Notes,
    DateTime? MergedAt,
    string? MergedBy)
{
    public static AssetMergeResponse FromEntity(AssetMerge merge) => new(
        merge.Id, merge.PrimaryTag, merge.MergedTags, merge.FieldSources, merge.FinalValues, merge.Notes, merge.CreatedAt, merge.CreatedBy);
}

public sealed record AssetMergeListResponse(IReadOnlyList<AssetMergeResponse> Items, long Total, int Page, int PageSize);
