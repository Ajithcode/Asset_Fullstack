using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>An identifier attached to an asset: a barcode or QR label, an RFID tag or a BLE beacon (e.g. RFID-009183).</summary>
/// <remarks>The assignment status is kept in BaseEntity.Status and is one of <see cref="TagStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class TrackingTag : BaseEntity
{
    [BsonElement("tag_id")]
    public string TagId { get; set; } = string.Empty;

    /// <summary>Physical form, e.g. Label, Hard Tag, Beacon.</summary>
    [BsonElement("tag_type")]
    public string TagType { get; set; } = string.Empty;

    /// <summary>One of <see cref="TagTechnology.All"/>.</summary>
    [BsonElement("technology")]
    public string Technology { get; set; } = string.Empty;

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("location_name")]
    public string? LocationName { get; set; }

    [BsonElement("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }
}

public static class TagStatus
{
    public const string Available = "Available";
    public const string Assigned = "Assigned";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Available, Assigned, Inactive];
}

public static class TagTechnology
{
    public const string Barcode = "Barcode";
    public const string QrCode = "QR Code";
    public const string PassiveRfid = "Passive RFID";
    public const string ActiveRfid = "Active RFID";
    public const string Ble = "BLE";
    public const string Nfc = "NFC";

    public static readonly IReadOnlyList<string> All = [Barcode, QrCode, PassiveRfid, ActiveRfid, Ble, Nfc];
}
