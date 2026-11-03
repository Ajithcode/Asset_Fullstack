using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to IoT Device Management: Device Name / ID, Device Type, Model, Connectivity,
/// Linked Asset, Gateway, Battery, Last Seen, Health. Also the body of the "Add Device" form.
/// </summary>
/// <param name="Model">Model number from GET /api/iot-devices/models.</param>
/// <param name="SerialNumber">Required, unique.</param>
/// <param name="Connectivity">e.g. BLE 5.0 or LoRaWAN, from GET /api/iot-devices/connectivities.</param>
/// <param name="SiteId">Site id from GET /api/master-data/sites; SiteName is stored alongside for display.</param>
/// <param name="Gateway">Gateway device id from GET /api/iot-devices/gateways, e.g. GW-DXB-01.</param>
/// <param name="AssetCode">"Link to asset (optional)", e.g. FL-008.</param>
/// <param name="PowerSource">One of <see cref="DevicePowerSource.All"/>; when left out it is taken from the model.</param>
/// <param name="BatteryLevel">Percent, 0–100; only kept for battery-powered devices.</param>
/// <param name="Health">One of <see cref="IoTDeviceHealth.All"/>; defaults to Good.</param>
public sealed record CreateIoTDeviceRequest(
    string? DeviceId,
    string? DeviceName,
    string? DeviceType,
    string? Model,
    string? SerialNumber,
    string? Connectivity,
    string? SiteId,
    string? SiteName,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? Gateway,
    string? PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? FirmwareVersion,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    /// <param name="modelPowerSource">The model's power source, used when PowerSource is left out.</param>
    public IoTDevice ToEntity(CurrentUser? currentUser, string? modelPowerSource = null)
    {
        var powerSource = ContractText.Canonical(PowerSource, DevicePowerSource.All)
            ?? IoTDevicePower.FromModel(modelPowerSource);

        return new IoTDevice
        {
            DeviceId = DeviceId!.Trim(),
            DeviceName = DeviceName!.Trim(),
            DeviceType = DeviceType!.Trim(),
            Model = Model!.Trim(),
            SerialNumber = ContractText.TrimOrNull(SerialNumber),
            Connectivity = Connectivity!.Trim(),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetCode = ContractText.TrimOrNull(AssetCode),
            AssetName = ContractText.TrimOrNull(AssetName),
            Gateway = ContractText.TrimOrNull(Gateway),
            PowerSource = powerSource,
            BatteryLevel = powerSource == DevicePowerSource.Battery ? BatteryLevel : null,
            LastSeenAt = LastSeenAt?.ToUniversalTime(),
            Status = ContractText.Canonical(Health, IoTDeviceHealth.All) ?? IoTDeviceHealth.Good,
            FirmwareVersion = ContractText.TrimOrNull(FirmwareVersion),
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
    string? SerialNumber,
    string Connectivity,
    string? SiteId,
    string? SiteName,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? Gateway,
    string PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? FirmwareVersion,
    string? PendingFirmwareVersion,
    DateTime? FirmwareUpdateScheduledAt,
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
            device.SerialNumber,
            device.Connectivity,
            device.SiteId,
            device.SiteName,
            device.AssetRegistrationId,
            device.AssetCode,
            device.AssetName,
            device.Gateway,
            device.PowerSource,
            device.BatteryLevel,
            device.LastSeenAt,
            device.Status,
            device.FirmwareVersion,
            device.PendingFirmwareVersion,
            device.FirmwareUpdateScheduledAt,
            device.CreatedBy,
            device.CreatedAt,
            device.UpdatedBy,
            device.UpdatedAt,
            device.ClientId,
            device.TenantId);
    }
}

/// <summary>
/// An option of the Model dropdown, e.g. "Minew E8". Send ModelNumber as the device's Model;
/// DefaultConnectivity can pre-fill the Connectivity dropdown.
/// </summary>
public sealed record DeviceModelOptionResponse(
    string ModelNumber,
    string Manufacturer,
    string DisplayName,
    string DeviceType,
    string Connectivity,
    string? Protocol,
    string DefaultConnectivity,
    string PowerSource)
{
    public static DeviceModelOptionResponse FromEntity(DeviceModel model)
    {
        return new DeviceModelOptionResponse(
            model.ModelNumber,
            model.Manufacturer,
            string.IsNullOrWhiteSpace(model.Manufacturer) ? model.ModelNumber : $"{model.Manufacturer} {model.ModelNumber}",
            model.DeviceType,
            model.Connectivity,
            model.Protocol,
            string.IsNullOrWhiteSpace(model.Protocol) ? model.Connectivity : model.Protocol,
            IoTDevicePower.FromModel(model.PowerSource));
    }
}

/// <summary>An option of the Gateway dropdown, e.g. GW-DXB-01.</summary>
public sealed record GatewayOptionResponse(string DeviceId, string DeviceName, string? SiteId, string? SiteName)
{
    public static GatewayOptionResponse FromEntity(IoTDevice gateway)
    {
        return new GatewayOptionResponse(gateway.DeviceId, gateway.DeviceName, gateway.SiteId, gateway.SiteName);
    }
}

internal static class IoTDevicePower
{
    /// <summary>Maps a model power option (e.g. "Battery (replaceable)") onto a device power source; External power when unknown.</summary>
    public static string FromModel(string? modelPowerSource)
    {
        if (ContractText.Canonical(modelPowerSource, DevicePowerSource.All) is { } exact)
        {
            return exact;
        }

        return modelPowerSource?.StartsWith(DevicePowerSource.Battery, StringComparison.OrdinalIgnoreCase) == true
            ? DevicePowerSource.Battery
            : DevicePowerSource.ExternalPower;
    }
}
