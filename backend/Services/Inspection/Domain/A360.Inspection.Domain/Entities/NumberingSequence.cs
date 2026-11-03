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
}
