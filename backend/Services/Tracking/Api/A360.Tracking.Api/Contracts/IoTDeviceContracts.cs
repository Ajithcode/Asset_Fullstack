using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to IoT Device Management: Device Name / ID, Device Type, Model, Connectivity,
/// Linked Asset, Gateway, Battery, Last Seen, Health.
/// </summary>
/// <param name="Connectivity">One of <see cref="DeviceConnectivity.All"/>.</param>
/// <param name="PowerSource">One of <see cref="DevicePowerSource.All"/>.</param>
/// <param name="BatteryLevel">Percent, 0–100; required when PowerSource is Battery.</param>
/// <param name="Health">One of <see cref="IoTDeviceHealth.All"/>; defaults to Good.</param>
public sealed record CreateIoTDeviceRequest(
    string? DeviceId,
    string? DeviceName,
    string? DeviceType,
    string? Model,
    string? Connectivity,
    string? AssetRegistrationId,
    string? AssetName,
    string? Gateway,
    string? PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public IoTDevice ToEntity(CurrentUser? currentUser)
    {
        var powerSource = ContractText.Canonical(PowerSource, DevicePowerSource.All)!;

        return new IoTDevice
        {
            DeviceId = DeviceId!.Trim(),
            DeviceName = DeviceName!.Trim(),
            DeviceType = DeviceType!.Trim(),
            Model = Model!.Trim(),
            Connectivity = ContractText.Canonical(Connectivity, DeviceConnectivity.All)!,
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetName = ContractText.TrimOrNull(AssetName),
            Gateway = ContractText.TrimOrNull(Gateway),
            PowerSource = powerSource,
            BatteryLevel = powerSource == DevicePowerSource.Battery ? BatteryLevel : null,
            LastSeenAt = LastSeenAt?.ToUniversalTime(),
            Status = ContractText.Canonical(Health, IoTDeviceHealth.All) ?? IoTDeviceHealth.Good,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record IoTDeviceResponse(
    string Id,
    string DeviceId,
    string DeviceName,
    string DeviceType,
    string Model,
    string Connectivity,
    string? AssetRegistrationId,
    string? AssetName,
    string? Gateway,
    string PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static IoTDeviceResponse FromEntity(IoTDevice device)
    {
        return new IoTDeviceResponse(
            device.Id,
            device.DeviceId,
            device.DeviceName,
            device.DeviceType,
            device.Model,
            device.Connectivity,
            device.AssetRegistrationId,
            device.AssetName,
            device.Gateway,
            device.PowerSource,
            device.BatteryLevel,
            device.LastSeenAt,
            device.Status,
            device.CreatedBy,
            device.CreatedAt,
            device.UpdatedBy,
            device.UpdatedAt,
            device.ClientId,
            device.TenantId);
    }
}
