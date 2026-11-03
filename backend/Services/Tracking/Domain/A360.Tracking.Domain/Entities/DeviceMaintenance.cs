using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Device Maintenance: one planned or open service job on a connected device or gateway (e.g. DM000001, preventive maintenance of GW-DXB-02).</summary>
/// <remarks>
/// A device can have many jobs over time; each gets its own MaintenanceId.
/// Status is kept in BaseEntity.Status and is one of <see cref="DeviceMaintenanceStatus"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class DeviceMaintenance : BaseEntity
{
    [BsonElement("maintenance_id")]
    public string MaintenanceId { get; set; } = string.Empty;

    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>What the device is, e.g. LoRaWAN Gateway, Vibration Sensor V2.</summary>
    [BsonElement("device_name")]
    public string? DeviceName { get; set; }

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string SiteName { get; set; } = string.Empty;

    /// <summary>One of <see cref="DeviceMaintenanceType.All"/>.</summary>
    [BsonElement("maintenance_type")]
    public string MaintenanceType { get; set; } = string.Empty;

    [BsonElement("due_date")]
    public DateTime DueDate { get; set; }

    /// <summary>Technician or vendor doing the work, e.g. "A. Rahman", "Field Services LLC"; null while unassigned.</summary>
    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }
}

public static class DeviceMaintenanceType
{
    public const string Preventive = "Preventive";
    public const string CorrectiveRepair = "Corrective repair";
    public const string Calibration = "Calibration";
    public const string BatteryReplacement = "Battery replacement";
    public const string FirmwareUpdate = "Firmware update";
    public const string Inspection = "Inspection";

    public static readonly IReadOnlyList<string> All = [Preventive, CorrectiveRepair, Calibration, BatteryReplacement, FirmwareUpdate, Inspection];
}

public static class DeviceMaintenanceStatus
{
    public const string Scheduled = "Scheduled";
    public const string InProgress = "In Progress";
    public const string DueSoon = "Due Soon";
    public const string Overdue = "Overdue";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = [Scheduled, InProgress, DueSoon, Overdue, Completed, Cancelled];
}
