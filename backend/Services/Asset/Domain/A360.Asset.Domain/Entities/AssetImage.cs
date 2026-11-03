using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An image filed against one registered asset, for the Asset Images screen.</summary>
/// <remarks>
/// The file itself is uploaded through the Media service; this keeps its link and details.
/// Uploaded by / upload date are BaseEntity.CreatedBy / CreatedAt.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetImage : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Copied from the asset when the image is saved, so the list can be searched and shown without loading assets.
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    // e.g. "Full View - Left Side"; defaults to the file name without its extension.
    [BsonElement("image_name")]
    public string ImageName { get; set; } = string.Empty;

    // e.g. "Asset Overview", "Installation", "Inspection".
    [BsonElement("image_type")]
    public string ImageType { get; set; } = string.Empty;

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    // Where the photo was taken, e.g. "Head Office - Building A - ER-101". Defaults to the asset's location.
    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }

    // Resolution in pixels, as read by the browser before upload.
    [BsonElement("width")]
    public int? Width { get; set; }

    [BsonElement("height")]
    public int? Height { get; set; }
}
