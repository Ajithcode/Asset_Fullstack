using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>Asset Criticality configuration masters (MDM > Asset Criticality Configuration). Levels live in the MDM Criticality master.</summary>

/// <summary>An assessment criterion, e.g. "Safety &amp; Human Impact".</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityCriterionMaster : BaseEntity
{
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("sort_order")]
    public int SortOrder { get; set; }
}

/// <summary>A criterion with its weight in a template; name and code are copied so old versions stay readable.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityTemplateCriterion
{
    [BsonElement("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Percent of the total score; all weights of a template add up to exactly 100.
    [BsonElement("weight")]
    public double Weight { get; set; }
}

/// <summary>
/// A scoring template. Every edit saves a new version (same code, version + 1); older versions are kept so existing
/// assessments still point at the version they were scored with. IsLatest marks the version in use.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityTemplate : BaseEntity
{
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("industry")]
    public string? Industry { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("is_default")]
    public bool IsDefault { get; set; }

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("is_latest")]
    public bool IsLatest { get; set; } = true;

    [BsonElement("criteria")]
    public List<AssetCriticalityTemplateCriterion> Criteria { get; set; } = [];
}

/// <summary>Who approves an assessment: one row per level of the proposed criticality, plus "level reduced" and "review, no change". Enforced in a later phase.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityApprovalRule : BaseEntity
{
    // Level:Critical | Level:High | Level:Medium | Level:Low | LevelReduced | ReviewNoChange
    [BsonElement("scenario")]
    public string Scenario { get; set; } = string.Empty;

    [BsonElement("label")]
    public string Label { get; set; } = string.Empty;

    // assess = the assessor's own permission is enough; approve = needs Assets Approve.
    [BsonElement("approver_permission")]
    public string ApproverPermission { get; set; } = AssetCriticalityApproverPermissions.Approve;

    // Role names that approve (all of them when approval_type = All).
    [BsonElement("approver_roles")]
    public List<string> ApproverRoles { get; set; } = [];

    [BsonElement("approval_type")]
    public string ApprovalType { get; set; } = "Any";

    [BsonElement("requires_justification")]
    public bool RequiresJustification { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("sort_order")]
    public int SortOrder { get; set; }
}

public static class AssetCriticalityApproverPermissions
{
    public const string Assess = "assess";
    public const string Approve = "approve";

    public static readonly IReadOnlyList<string> All = [Assess, Approve];
    public static readonly IReadOnlyList<string> ApprovalTypes = ["Any", "All"];
}

/// <summary>Default review interval for assets of a criticality level (date-based reassessment).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityReassessmentRule : BaseEntity
{
    [BsonElement("level_name")]
    public string LevelName { get; set; } = string.Empty;

    [BsonElement("review_interval_months")]
    public int ReviewIntervalMonths { get; set; }
}
