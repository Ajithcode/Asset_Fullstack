using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class NumberingSequence : BaseEntity
{
    [BsonElement("sequence_code")]
    public string SequenceCode { get; set; } = string.Empty;

    [BsonElement("number_type")]
    public string NumberType { get; set; } = string.Empty;

    [BsonElement("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [BsonElement("suffix")]
    public string Suffix { get; set; } = string.Empty;

    [BsonElement("financial_year")]
    public string FinancialYear { get; set; } = string.Empty;

    [BsonElement("site_code")]
    public string SiteCode { get; set; } = string.Empty;

    [BsonElement("department_code")]
    public string DepartmentCode { get; set; } = string.Empty;

    [BsonElement("running_number")]
    public int RunningNumber { get; set; }

    [BsonElement("reset_frequency")]
    public string ResetFrequency { get; set; } = string.Empty;

    [BsonElement("sample_number_preview")]
    public string SampleNumberPreview { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("sequence_name")]
    public string SequenceName { get; set; } = string.Empty;

    [BsonElement("separator")]
    public string Separator { get; set; } = string.Empty;

    [BsonElement("year_format")]
    public string YearFormat { get; set; } = string.Empty;

    [BsonElement("sequence_digits")]
    public int SequenceDigits { get; set; }

    [BsonElement("starting_number")]
    public int StartingNumber { get; set; }

    [BsonElement("scope")]
    public string Scope { get; set; } = string.Empty;

    [BsonElement("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = string.Empty;

    [BsonElement("generate_on_final_creation")]
    public bool GenerateOnFinalCreation { get; set; }

    [BsonElement("do_not_reuse")]
    public bool DoNotReuse { get; set; }

    [BsonElement("allow_manual_override")]
    public bool AllowManualOverride { get; set; }

    [BsonElement("last_allocated")]
    public string LastAllocated { get; set; } = string.Empty;

    [BsonElement("assigned_this_year")]
    public int AssignedThisYear { get; set; }

    [BsonElement("allocations")]
    public List<SequenceAllocation> Allocations { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class SequenceAllocation
{
    [BsonElement("reference")]
    public string Reference { get; set; } = string.Empty;

    [BsonElement("record")]
    public string Record { get; set; } = string.Empty;

    [BsonElement("allocated_at")]
    public string AllocatedAt { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;
}
