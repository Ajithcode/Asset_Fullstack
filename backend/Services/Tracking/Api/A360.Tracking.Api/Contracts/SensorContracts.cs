using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Sensor Management: Sensor ID / Name, Sensor Type, Measured Parameter, Linked Device,
/// Linked Asset / Point, Live Reading, Calibration Due, Status.
/// </summary>
/// <param name="LiveReading">Latest value in Unit; leave empty for "No data".</param>
/// <param name="CalibrationDueDate">Only the date is kept.</param>
/// <param name="Status">One of <see cref="SensorStatus.All"/>; defaults to Active.</param>
public sealed record CreateSensorRequest(
    string? SensorId,
    string? Name,
    string? SensorType,
    string? MeasuredParameter,
    string? Unit,
    string? DeviceId,
    string? DeviceModel,
    string? AssetRegistrationId,
    string? AssetName,
    string? MeasurementPoint,
    decimal? LiveReading,
    DateTime? CalibrationDueDate,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public Sensor ToEntity(CurrentUser? currentUser)
    {
        return new Sensor
        {
            SensorId = SensorId!.Trim(),
            Name = Name!.Trim(),
            SensorType = SensorType!.Trim(),
            MeasuredParameter = MeasuredParameter!.Trim(),
            Unit = ContractText.TrimOrNull(Unit),
            DeviceId = ContractText.TrimOrNull(DeviceId),
            DeviceModel = ContractText.TrimOrNull(DeviceModel),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
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
    string Name,
    string SensorType,
    string MeasuredParameter,
    string? Unit,
    string? DeviceId,
    string? DeviceModel,
    string? AssetRegistrationId,
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
            sensor.Name,
            sensor.SensorType,
            sensor.MeasuredParameter,
            sensor.Unit,
            sensor.DeviceId,
            sensor.DeviceModel,
            sensor.AssetRegistrationId,
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
