using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Condition : BaseEntity
{
    [BsonElement("condition_name")]
    public string ConditionName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("type")]
    public string? Type { get; set; }

    // Hex colour used to show the condition, e.g. "#2E7D32".
    [BsonElement("color")]
    public string? Color { get; set; }

    // At most one condition per client is the default.
    [BsonElement("default_condition")]
    public bool DefaultCondition { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // 0 (worst) to 100 (best).
    [BsonElement("health_score")]
    public int? HealthScore { get; set; }

    // Built-in icon key, used when no image is uploaded.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    // Uploaded picture as a data URL.
    [BsonElement("image")]
    public string? Image { get; set; }

    [BsonElement("display_order")]
    public int? DisplayOrder { get; set; }

    [BsonElement("show_in_mobile")]
    public bool ShowInMobile { get; set; } = true;

    [BsonElement("show_in_reports")]
    public bool ShowInReports { get; set; } = true;

    [BsonElement("characteristics")]
    public ConditionCharacteristics Characteristics { get; set; } = new();

    // Allowed moves, as codes of other conditions of the same client.
    [BsonElement("can_move_from")]
    public List<string> CanMoveFrom { get; set; } = [];

    [BsonElement("can_move_to")]
    public List<string> CanMoveTo { get; set; } = [];

    [BsonElement("trigger_criteria")]
    public string? TriggerCriteria { get; set; }

    [BsonElement("auto_update")]
    public bool AutoUpdate { get; set; }

    [BsonElement("photo_required")]
    public bool PhotoRequired { get; set; }

    [BsonElement("comment_required")]
    public bool CommentRequired { get; set; }

    [BsonElement("notify_owner")]
    public bool NotifyOwner { get; set; }

    [BsonElement("blocks_assignment")]
    public bool BlocksAssignment { get; set; }
}

// How an asset in this condition typically looks and behaves, as free text.
public class ConditionCharacteristics
{
    [BsonElement("physical_state")]
    public string? PhysicalState { get; set; }

    [BsonElement("functionality")]
    public string? Functionality { get; set; }

    [BsonElement("appearance")]
    public string? Appearance { get; set; }

    [BsonElement("maintenance_need")]
    public string? MaintenanceNeed { get; set; }

    [BsonElement("expected_remaining_life")]
    public string? ExpectedRemainingLife { get; set; }
}
