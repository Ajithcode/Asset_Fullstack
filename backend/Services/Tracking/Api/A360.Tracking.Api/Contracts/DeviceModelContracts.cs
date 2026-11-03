using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Device Type & Model: Model / Manufacturer, Device Type, Connectivity,
/// Supported Measurements, Power, Protocol, Status.
/// </summary>
/// <param name="Connectivity">One of <see cref="DeviceModelConnectivity.All"/>.</param>
/// <param name="PowerSource">One of <see cref="DevicePowerSource.All"/>.</param>
/// <param name="Status">One of <see cref="DeviceModelStatus.All"/>; defaults to Draft.</param>
public sealed record CreateDeviceModelRequest(
    string? ModelNumber,
    string? Manufacturer,
    string? DeviceType,
    string? Connectivity,
    IReadOnlyList<string>? SupportedMeasurements,
    string? PowerSource,
    string? Protocol,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DeviceModel ToEntity(CurrentUser? currentUser)
    {
        return new DeviceModel
        {
            ModelNumber = ModelNumber!.Trim(),
            Manufacturer = Manufacturer!.Trim(),
            DeviceType = DeviceType!.Trim(),
            Connectivity = ContractText.Canonical(Connectivity, DeviceModelConnectivity.All)!,
            SupportedMeasurements = (SupportedMeasurements ?? [])
                .Where(measurement => !string.IsNullOrWhiteSpace(measurement))
                .Select(measurement => measurement.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PowerSource = ContractText.Canonical(PowerSource, DevicePowerSource.All)!,
            Protocol = ContractText.TrimOrNull(Protocol),
            Status = ContractText.Canonical(Status, DeviceModelStatus.All) ?? DeviceModelStatus.Draft,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record DeviceModelResponse(
    string Id,
    string ModelNumber,
    string Manufacturer,
    string DeviceType,
    string Connectivity,
    IReadOnlyList<string> SupportedMeasurements,
    string PowerSource,
    string? Protocol,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static DeviceModelResponse FromEntity(DeviceModel model)
    {
        return new DeviceModelResponse(
            model.Id,
            model.ModelNumber,
            model.Manufacturer,
            model.DeviceType,
            model.Connectivity,
            model.SupportedMeasurements,
            model.PowerSource,
            model.Protocol,
            model.Status,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt,
            model.ClientId,
            model.TenantId);
    }
}
