using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A document filed against one registered asset, for the Asset Documents screen.</summary>
/// <remarks>
/// The file itself is uploaded through the Media service; this keeps its link and details.
/// Uploaded by / upload date are BaseEntity.CreatedBy / CreatedAt, and Status (Active/Inactive) is BaseEntity.Status.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetDocumentRecord : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Copied from the asset when the document is saved, so the list can be searched and shown without loading assets.
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("document_name")]
    public string DocumentName { get; set; } = string.Empty;

    // e.g. "User Manual", "Invoice", "Purchase Order".
    [BsonElement("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    // e.g. "Procurement", "In Operation", "Maintenance".
    [BsonElement("lifecycle_stage")]
    public string LifecycleStage { get; set; } = string.Empty;

    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    [BsonElement("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }
}
