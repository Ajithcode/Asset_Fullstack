using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class DataImportJob : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("job_id")]
    public string JobId { get; set; } = string.Empty;

    [BsonElement("job_name")]
    public string JobName { get; set; } = string.Empty;

    // Target data object, e.g. Asset Master.
    [BsonElement("data_object")]
    public string DataObject { get; set; } = string.Empty;

    // Insert, Update or Upsert.
    [BsonElement("operation")]
    public string Operation { get; set; } = string.Empty;

    // URL of the uploaded file, typically returned by the Media service upload.
    [BsonElement("source_file")]
    public string SourceFile { get; set; } = string.Empty;

    // XLSX or CSV.
    [BsonElement("file_format")]
    public string? FileFormat { get; set; }

    // e.g. Update existing.
    [BsonElement("duplicate_handling")]
    public string? DuplicateHandling { get; set; }

    [BsonElement("date_format")]
    public string? DateFormat { get; set; }

    // e.g. Comma.
    [BsonElement("delimiter")]
    public string? Delimiter { get; set; }

    // e.g. UTF-8.
    [BsonElement("encoding")]
    public string? Encoding { get; set; }

    [BsonElement("batch_size")]
    public int BatchSize { get; set; }

    // e.g. None (Run manually).
    [BsonElement("schedule")]
    public string? Schedule { get; set; }

    [BsonElement("notification_recipients")]
    public List<string> NotificationRecipients { get; set; } = [];
}
