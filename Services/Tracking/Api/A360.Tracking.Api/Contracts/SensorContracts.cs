using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Sensor Management: Sensor ID / Name, Sensor Type, Measured Parameter, Linked Device,
/// Linked Asset / Point, Live Reading, Calibration Due, Status. Also the body of the "Add Sensor" form.
/// </summary>
/// <param name="SensorId">Optional when SensorNumber is given: built as the type's prefix + number, e.g. SEN-T-80900.</param>
/// <param name="SensorNumber">The "Sensor number" field, e.g. 80900.</param>
/// <param name="Name">Defaults to the sensor type.</param>
/// <param name="MeasuredParameter">Defaults from the sensor type (GET /api/sensors/types), as does Unit.</param>
/// <param name="DeviceId">"Linked device", an IoT device id, e.g. TS-80214. The device model and site are taken from it when left out.</param>
/// <param name="Channel">Device channel, 1 or more; unique per device.</param>
/// <param name="SamplingIntervalSeconds">One of GET /api/sensors/sampling-intervals.</param>
/// <param name="SiteId">Site id from GET /api/master-data/sites.</param>
/// <param name="LiveReading">Latest value in Unit; leave empty for "No data".</param>
/// <param name="CalibrationDueDate">Only the date is kept.</param>
/// <param name="Status">One of <see cref="SensorStatus.All"/>; defaults to Active.</param>
public sealed record CreateSensorRequest(
    string? SensorId,
    string? SensorNumber,
    string? SerialNumber,
    string? Name,
    string? SensorType,
    string? MeasuredParameter,
    string? Unit,
    string? DeviceId,
    string? DeviceModel,
    int? Channel,
    int? SamplingIntervalSeconds,
    decimal? LowAlarm,
    decimal? HighAlarm,
    string? SiteId,
    string? SiteName,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? MeasurementPoint,
    decimal? LiveReading,
    DateTime? CalibrationDueDate,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    /// <summary>The sensor id as sent, or built from the type prefix and SensorNumber (e.g. SEN-T-80900).</summary>
    public string? ResolvedSensorId()
    {
        if (!string.IsNullOrWhiteSpace(SensorId))
        {
            return SensorId.Trim();
        }

        if (string.IsNullOrWhiteSpace(SensorNumber))
        {
            return null;
        }

        var prefix = SensorTypeOption.Find(SensorType)?.IdPrefix ?? "SEN";
        return $"{prefix}-{SensorNumber.Trim()}";
    }

    public Sensor ToEntity(CurrentUser? currentUser)
    {
        var type = SensorTypeOption.Find(SensorType);

        return new Sensor
        {
            SensorId = ResolvedSensorId()!,
            SensorNumber = ContractText.TrimOrNull(SensorNumber),
            SerialNumber = ContractText.TrimOrNull(SerialNumber),
            Name = ContractText.TrimOrNull(Name) ?? SensorType!.Trim(),
            SensorType = type?.SensorType ?? SensorType!.Trim(),
            MeasuredParameter = ContractText.TrimOrNull(MeasuredParameter) ?? type?.MeasuredParameter ?? SensorType!.Trim(),
            Unit = ContractText.TrimOrNull(Unit) ?? type?.Unit,
            DeviceId = ContractText.TrimOrNull(DeviceId),
            DeviceModel = ContractText.TrimOrNull(DeviceModel),
            Channel = Channel,
            SamplingIntervalSeconds = SamplingIntervalSeconds,
            LowAlarm = LowAlarm,
            HighAlarm = HighAlarm,
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetCode = ContractText.TrimOrNull(AssetCode),
            AssetName = ContractText.TrimOrNull(AssetName),
            MeasurementPoint = ContractText.TrimOrNull(MeasurementPoint),
            LiveReading = LiveReading,
            // Kept as UTC midnight so a date sent without a time does not shift a day.
            CalibrationDueDate = CalibrationDueDate is { } date ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc) : null,
            Status = ContractText.Canonical(Status, SensorStatus.All) ?? SensorStatus.Active,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record SensorResponse(
    string Id,
    string SensorId,
    string? SensorNumber,
    string? SerialNumber,
    string Name,
    string SensorType,
    string MeasuredParameter,
    string? Unit,
    string? DeviceId,
    string? DeviceModel,
    int? Channel,
    int? SamplingIntervalSeconds,
    decimal? LowAlarm,
    decimal? HighAlarm,
    string? SiteId,
    string? SiteName,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? MeasurementPoint,
    decimal? LiveReading,
    DateTime? CalibrationDueDate,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static SensorResponse FromEntity(Sensor sensor)
    {
        return new SensorResponse(
            sensor.Id,
            sensor.SensorId,
            sensor.SensorNumber,
            sensor.SerialNumber,
            sensor.Name,
            sensor.SensorType,
            sensor.MeasuredParameter,
            sensor.Unit,
            sensor.DeviceId,
            sensor.DeviceModel,
            sensor.Channel,
            sensor.SamplingIntervalSeconds,
            sensor.LowAlarm,
            sensor.HighAlarm,
            sensor.SiteId,
            sensor.SiteName,
            sensor.AssetRegistrationId,
            sensor.AssetCode,
            sensor.AssetName,
            sensor.MeasurementPoint,
            sensor.LiveReading,
            sensor.CalibrationDueDate,
            sensor.Status,
            sensor.CreatedBy,
            sensor.CreatedAt,
            sensor.UpdatedBy,
            sensor.UpdatedAt,
            sensor.ClientId,
            sensor.TenantId);
    }
}

/// <summary>An option of the Linked device dropdown.</summary>
public sealed record SensorDeviceOptionResponse(
    string DeviceId,
    string DeviceName,
    string DeviceType,
    string Model,
    string? SiteId,
    string? SiteName,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    IReadOnlyList<int> UsedChannels,
    int NextChannel);

/// <summary>Channels already taken on a device and the first free one.</summary>
public sealed record SensorChannelsResponse(string DeviceId, IReadOnlyList<int> UsedChannels, int NextChannel);

/// <summary>A Sampling interval option, e.g. 60 → "60 seconds".</summary>
public sealed record SamplingIntervalOptionResponse(int Seconds, string Label);

/// <summary>Every dropdown of the Add Sensor form in one response.</summary>
public sealed record SensorFormOptionsResponse(
    IReadOnlyList<SensorTypeOption> SensorTypes,
    IReadOnlyList<SamplingIntervalOptionResponse> SamplingIntervals,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MeasurementPointsBySensorType,
    IReadOnlyList<string> Statuses);
