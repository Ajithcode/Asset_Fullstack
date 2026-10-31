using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class CalibrationPoint
{
    [BsonElement("reference")]
    public double Reference { get; set; }

    [BsonElement("as_found")]
    public double AsFound { get; set; }

    [BsonElement("as_left")]
    public double AsLeft { get; set; }

    [BsonElement("result")]
    public string Result { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class CalibrationHistoryEntry
{
    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;

    [BsonElement("result")]
    public string Result { get; set; } = string.Empty;

    [BsonElement("certificate")]
    public string Certificate { get; set; } = string.Empty;

    [BsonElement("performed_by")]
    public string PerformedBy { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class CalibrationInstrument : BaseEntity
{
    [BsonElement("instrument_code")]
    public string InstrumentCode { get; set; } = string.Empty;

    [BsonElement("instrument_name")]
    public string InstrumentName { get; set; } = string.Empty;

    [BsonElement("serial_number")]
    public string SerialNumber { get; set; } = string.Empty;

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("range")]
    public string Range { get; set; } = string.Empty;

    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    [BsonElement("tolerance")]
    public string Tolerance { get; set; } = string.Empty;

    [BsonElement("interval_months")]
    public int IntervalMonths { get; set; }

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("provider_type")]
    public string ProviderType { get; set; } = string.Empty;

    [BsonElement("assigned_to")]
    public string AssignedTo { get; set; } = string.Empty;

    [BsonElement("last_calibration")]
    public string? LastCalibration { get; set; }

    [BsonElement("next_due")]
    public string? NextDue { get; set; }

    [BsonElement("certificate")]
    public string Certificate { get; set; } = string.Empty;

    [BsonElement("last_result")]
    public string LastResult { get; set; } = string.Empty;

    [BsonElement("results")]
    public List<CalibrationPoint> Results { get; set; } = [];

    [BsonElement("reference_standard")]
    public string ReferenceStandard { get; set; } = string.Empty;

    [BsonElement("reference_certificate")]
    public string ReferenceCertificate { get; set; } = string.Empty;

    [BsonElement("valid_at_calibration")]
    public bool ValidAtCalibration { get; set; }

    [BsonElement("history")]
    public List<CalibrationHistoryEntry> History { get; set; } = [];
}
