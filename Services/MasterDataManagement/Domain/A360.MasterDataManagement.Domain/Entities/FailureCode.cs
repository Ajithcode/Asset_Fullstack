using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class FailureCode : BaseEntity
{
    [BsonElement("failure_code_name")]
    public string FailureCodeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Electrical" or "Mechanical".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    // One of FailureCodeSeverities: Critical, High, Medium or Low.
    [BsonElement("severity")]
    public string Severity { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }
}
