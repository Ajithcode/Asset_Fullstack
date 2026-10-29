using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class NumberingSequence : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("sequence_id")]
    public string SequenceId { get; set; } = string.Empty;

    [BsonElement("sequence_name")]
    public string SequenceName { get; set; } = string.Empty;

    // Unique, upper-case code such as ASSET-001.
    [BsonElement("sequence_code")]
    public string SequenceCode { get; set; } = string.Empty;

    // Module the numbers are generated for, e.g. Assets, Maintenance.
    [BsonElement("module")]
    public string Module { get; set; } = string.Empty;

    // e.g. Physical Asset.
    [BsonElement("record_type")]
    public string RecordType { get; set; } = string.Empty;

    // Static text at the start of every number, e.g. AST.
    [BsonElement("prefix")]
    public string Prefix { get; set; } = string.Empty;

    // Character placed between the parts of the number, e.g. "-".
    [BsonElement("separator")]
    public string Separator { get; set; } = string.Empty;

    // Zero-padded width of the counter, e.g. 6 -> 000001.
    [BsonElement("number_length")]
    public int NumberLength { get; set; }

    [BsonElement("start_number")]
    public long StartNumber { get; set; }

    [BsonElement("increment_by")]
    public int IncrementBy { get; set; }

    // Never, Yearly or Monthly.
    [BsonElement("reset_frequency")]
    public string ResetFrequency { get; set; } = string.Empty;

    // Organization, Location or Department.
    [BsonElement("scope")]
    public string Scope { get; set; } = string.Empty;
}
