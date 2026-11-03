using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class SignatureUsageLogEntry
{
    [BsonElement("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    [BsonElement("device_information")]
    public string DeviceInformation { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("signature_hash")]
    public string SignatureHash { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class SignatureAndStamp : BaseEntity
{
    [BsonElement("signature_code")]
    public string SignatureCode { get; set; } = string.Empty;

    [BsonElement("user")]
    public string User { get; set; } = string.Empty;

    [BsonElement("signature_name")]
    public string SignatureName { get; set; } = string.Empty;

    [BsonElement("signature_image")]
    public string SignatureImage { get; set; } = string.Empty;

    [BsonElement("stamp_image")]
    public string StampImage { get; set; } = string.Empty;

    [BsonElement("digital_signature_certificate")]
    public string DigitalSignatureCertificate { get; set; } = string.Empty;

    [BsonElement("effective_date")]
    public DateTime? EffectiveDate { get; set; }

    [BsonElement("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [BsonElement("is_default_signature")]
    public bool IsDefaultSignature { get; set; }

    [BsonElement("is_default_stamp")]
    public bool IsDefaultStamp { get; set; }

    [BsonElement("allow_manual_signature")]
    public bool AllowManualSignature { get; set; }

    [BsonElement("allow_uploaded_signature")]
    public bool AllowUploadedSignature { get; set; }

    [BsonElement("allow_both")]
    public bool AllowBoth { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("require_password_confirmation_before_signing")]
    public bool RequirePasswordConfirmationBeforeSigning { get; set; }

    [BsonElement("require_otp_confirmation")]
    public bool RequireOtpConfirmation { get; set; }

    [BsonElement("signature_usage_log")]
    public List<SignatureUsageLogEntry> SignatureUsageLog { get; set; } = [];
}
