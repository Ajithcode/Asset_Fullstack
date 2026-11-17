using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// A tag identifier registered in Tag Management: a barcode or QR label, an RFID tag, a BLE beacon or an NFC tag
/// (e.g. BLE-002951). Its tag id is the technology prefix plus the tag number.
/// </summary>
/// <remarks>The assignment status is kept in BaseEntity.Status and is one of <see cref="TagStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class TrackingTag : BaseEntity
{
    /// <summary>Prefix + tag number, e.g. BLE-002952.</summary>
    [BsonElement("tag_id")]
    public string TagId { get; set; } = string.Empty;

    /// <summary>The number typed in the Register form, e.g. 002952.</summary>
    [BsonElement("tag_number")]
    public string TagNumber { get; set; } = string.Empty;

    /// <summary>One of <see cref="TagTechnology.All"/>.</summary>
    [BsonElement("technology")]
    public string Technology { get; set; } = string.Empty;

    /// <summary>Physical form shown as Format, e.g. Beacon, Barcode Label, QR Label, Hard Tag.</summary>
    [BsonElement("tag_type")]
    public string TagType { get; set; } = string.Empty;

    /// <summary>What the tag carries: a MAC address (BLE), an EPC (RFID), an encoded value (barcode / QR) or a UID (NFC).</summary>
    [BsonElement("value")]
    public string? Value { get; set; }

    [BsonElement("firmware_version")]
    public string? FirmwareVersion { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("location_name")]
    public string? LocationName { get; set; }

    /// <summary>Registration id of the linked asset (e.g. AR-000123); null while the tag is in stock.</summary>
    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    /// <summary>The asset's own code / asset tag, e.g. FL-008.</summary>
    [BsonElement("asset_code")]
    public string? AssetCode { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [BsonElement("assigned_by")]
    public string? AssignedBy { get; set; }

    /// <summary>Why the tag was deactivated, while it is Inactive.</summary>
    [BsonElement("inactive_reason")]
    public string? InactiveReason { get; set; }

    [BsonElement("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    /// <summary>The reader, gateway or scanner that last read the tag, e.g. Gateway GW-DXB-04.</summary>
    [BsonElement("last_seen_by")]
    public string? LastSeenBy { get; set; }

    [BsonElement("battery_percent")]
    public int? BatteryPercent { get; set; }

    [BsonElement("signal_dbm")]
    public int? SignalDbm { get; set; }
}

public static class TagStatus
{
    public const string Available = "Available";
    public const string Assigned = "Assigned";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Available, Assigned, Inactive];
}

public static class TagValidation
{
    public const string Valid = "Valid";
    public const string Invalid = "Invalid";

    public static readonly IReadOnlyList<string> All = [Valid, Invalid];
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

    /// <summary>Prefix of the tag id shown in the Register Tag ID form, e.g. BLE + 002952 → BLE-002952.</summary>
    public static readonly IReadOnlyDictionary<string, string> IdPrefixes = new Dictionary<string, string>
    {
        [Barcode] = "BC",
        [QrCode] = "QR",
        [PassiveRfid] = "RFID",
        [ActiveRfid] = "RFID",
        [Ble] = "BLE",
        [Nfc] = "NFC"
    };

    /// <summary>The tag types (physical forms) offered for each technology in the Tag type dropdown.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> TagTypes = new Dictionary<string, IReadOnlyList<string>>
    {
        [Barcode] = ["Barcode Label"],
        [QrCode] = ["QR Label"],
        [PassiveRfid] = ["Label", "Hard Tag"],
        [ActiveRfid] = ["Hard Tag"],
        [Ble] = ["Beacon"],
        [Nfc] = ["Label", "Hard Tag"]
    };

    /// <summary>The label of the value field and column, e.g. "MAC Address" for BLE.</summary>
    public static readonly IReadOnlyDictionary<string, string> ValueLabels = new Dictionary<string, string>
    {
        [Barcode] = "Encoded Value",
        [QrCode] = "Encoded Value",
        [PassiveRfid] = "EPC",
        [ActiveRfid] = "EPC",
        [Ble] = "MAC Address",
        [Nfc] = "UID"
    };

    /// <summary>Technologies whose tags report battery, signal and firmware.</summary>
    public static readonly IReadOnlySet<string> Powered = new HashSet<string> { ActiveRfid, Ble };
}
