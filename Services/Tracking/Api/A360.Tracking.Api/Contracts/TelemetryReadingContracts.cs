using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Stores one telemetry value from a device. The ReadingId (e.g. TR000001) is generated.</summary>
/// <param name="RecordedAt">When the device took the reading; defaults to now.</param>
public sealed record CreateTelemetryReadingRequest(
    DateTime? RecordedAt,
    string? DeviceId,
    string? SensorId,
    string? AssetRegistrationId,
    string? Parameter,
    decimal? Value,
    string? Unit,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TelemetryReading ToEntity(string readingId, CurrentUser? currentUser)
    {
        return new TelemetryReading
        {
            ReadingId = readingId,
            RecordedAt = RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            DeviceId = DeviceId!.Trim(),
            SensorId = ContractText.TrimOrNull(SensorId),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            Parameter = Parameter!.Trim(),
            Value = Value!.Value,
            Unit = ContractText.TrimOrNull(Unit),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            Status = "Active",
            IsDeleted = false
        };
    }
}

public sealed record TelemetryReadingResponse(
    string Id,
    string ReadingId,
    DateTime RecordedAt,
    string DeviceId,
    string? SensorId,
    string? AssetRegistrationId,
    string Parameter,
    decimal Value,
    string? Unit,
    DateTime? CreatedAt)
{
    public static TelemetryReadingResponse FromEntity(TelemetryReading reading)
    {
        return new TelemetryReadingResponse(
            reading.Id,
            reading.ReadingId,
            reading.RecordedAt,
            reading.DeviceId,
            reading.SensorId,
            reading.AssetRegistrationId,
            reading.Parameter,
            reading.Value,
            reading.Unit,
            reading.CreatedAt);
    }
}

/// <summary>One x-axis point of the IoT Events Timeline.</summary>
/// <param name="Date">UTC day, yyyy-MM-dd.</param>
/// <param name="LocationUpdates">Location events of type Entered Zone / Departed Zone.</param>
/// <param name="GeofenceEvents">Location events of type Geofence Alert / Dwell Exceeded.</param>
public sealed record IoTEventsTimelinePoint(
    string Date,
    int TelemetryData,
    int LocationUpdates,
    int GeofenceEvents,
    int Alerts);

/// <summary>The IoT Events Timeline for the last <see cref="Days"/> days (7 / 30 / 90 on the dashboard); one point per day, zero-filled.</summary>
public sealed record IoTEventsTimelineResponse(
    int Days,
    DateTime From,
    DateTime To,
    IoTEventsTimelinePoint Totals,
    IReadOnlyList<IoTEventsTimelinePoint> Points);
