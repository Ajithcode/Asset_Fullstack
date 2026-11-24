using A360.Security;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Contracts;

/// <summary>The Add Device form. Battery, signal, last seen and online / offline come from NetworkTimeSeries, not from here.</summary>
/// <param name="Model">Model number from GET /api/iot-devices/models.</param>
/// <param name="DeviceId">The id the device reports with, e.g. BL-29901 or its hardware id c3000071fe4c. Unique.</param>
/// <param name="SerialNumber">Required, unique.</param>
/// <param name="Connectivity">e.g. BLE 5.0 or LoRaWAN, from GET /api/iot-devices/connectivities.</param>
/// <param name="SiteId">Site id from GET /api/master-data/sites; SiteName is stored alongside for display.</param>
/// <param name="Gateway">Gateway device id from GET /api/iot-devices/gateways, e.g. GW-DXB-01.</param>
/// <param name="AssetCode">"Link to asset (optional)": asset code (FL-008) or registration id (AR000001).</param>
public sealed record AddIoTDeviceRequest(
    string? DeviceType,
    string? Model,
    string? DeviceName,
    string? DeviceId,
    string? SerialNumber,
    string? Connectivity,
    string? SiteId,
    string? SiteName,
    string? Gateway,
    string? AssetCode,
    string? FirmwareVersion)
{
    public IoTDevice ToEntity(CurrentUser? currentUser, DeviceModel model, TagAsset? asset)
    {
        var powerSource = IoTDevicePower.FromModel(model.PowerSource);

        return new IoTDevice
        {
            DeviceId = DeviceId!.Trim(),
            DeviceName = DeviceName!.Trim(),
            DeviceType = DeviceType!.Trim(),
            Model = model.ModelNumber,
            SerialNumber = SerialNumber!.Trim(),
            Connectivity = Connectivity!.Trim(),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            Gateway = ContractText.TrimOrNull(Gateway),
            AssetRegistrationId = asset?.AssetRegistrationId,
            AssetCode = asset?.AssetCode,
            AssetName = asset?.AssetName,
            PowerSource = powerSource,
            Status = IoTDeviceHealth.Good,
            FirmwareVersion = ContractText.TrimOrNull(FirmwareVersion),
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            IsDeleted = false
        };
    }
}

/// <summary>Query string of the device table. With only search and limit it is the Device dropdown search.</summary>
public sealed class IoTDeviceListQuery
{
    /// <summary>Matches device name or id, serial number, asset code or name, gateway.</summary>
    public string? Search { get; set; }
    public string? DeviceType { get; set; }
    public string? Connectivity { get; set; }
    public string? SiteId { get; set; }

    /// <summary>Online or Offline (live, from NetworkTimeSeries).</summary>
    public string? Status { get; set; }

    /// <summary>Good, Warning or Critical.</summary>
    public string? Health { get; set; }

    /// <summary>true: only devices with battery under 50%.</summary>
    public bool? Attention { get; set; }

    /// <summary>1-based; default 1.</summary>
    public int? Page { get; set; }

    /// <summary>1-200; default 10.</summary>
    public int? PageSize { get; set; }

    /// <summary>Dropdown search: return at most this many rows as a plain list (no paging).</summary>
    public int? Limit { get; set; }
}

public static class DeviceConnection
{
    public const string Online = "Online";
    public const string Offline = "Offline";

    public static readonly IReadOnlyList<string> All = [Online, Offline];

    /// <summary>Battery below this percent needs attention.</summary>
    public const int LowBatteryPercent = 50;

    /// <summary>Battery below this percent makes the device Critical.</summary>
    public const int CriticalBatteryPercent = 10;
}

/// <summary>A device with its latest NetworkTimeSeries reading applied.</summary>
public sealed record LiveIoTDevice(IoTDevice Device, NetworkReading? Reading, DeviceModel? Model)
{
    /// <summary>Online when the latest reading says online; Offline when it says otherwise or the device never reported.</summary>
    public string Connection => string.Equals(Reading?.Status, "online", StringComparison.OrdinalIgnoreCase)
        ? DeviceConnection.Online
        : DeviceConnection.Offline;

    public bool NeverReported => Reading is null;

    /// <summary>Battery from the latest reading only; null when the device never reported one.</summary>
    public int? BatteryPercent => Reading?.Battery;

    public bool LowBattery => BatteryPercent < DeviceConnection.LowBatteryPercent;

    public DateTime? LastSeenAt => Reading?.Timestamp;

    /// <summary>The gateway that relayed the latest reading, else the one set on the device.</summary>
    public string? Gateway => Reading?.GatewayId ?? Device.Gateway;

    /// <summary>Critical under 10% battery; Warning when offline or under 50%; otherwise Good.</summary>
    public string Health => BatteryPercent < DeviceConnection.CriticalBatteryPercent
        ? IoTDeviceHealth.Critical
        : Connection == DeviceConnection.Offline || LowBattery
            ? IoTDeviceHealth.Warning
            : IoTDeviceHealth.Good;

    public string ModelDisplayName => Model is null || string.IsNullOrWhiteSpace(Model.Manufacturer)
        ? Device.Model
        : $"{Model.Manufacturer} {Model.ModelNumber}";

    public string? Protocol => Model?.Protocol ?? Device.Connectivity;
}

/// <summary>A card: the count and its share of all devices in percent (1 decimal).</summary>
public sealed record DeviceCountResponse(int Count, double Percent)
{
    public static DeviceCountResponse Of(int count, int total) =>
        new(count, total == 0 ? 0 : Math.Round(count * 100.0 / total, 1));
}

/// <summary>
/// The four cards: Total Devices, Online, Offline (includes devices that never reported) and Attention Required
/// (battery under 50%), with online / offline / battery from the latest NetworkTimeSeries reading of each device.
/// </summary>
public sealed record IoTDeviceSummaryResponse(
    int Total,
    DeviceCountResponse Online,
    DeviceCountResponse Offline,
    DeviceCountResponse AttentionRequired,
    int NeverReported,
    IReadOnlyDictionary<string, int> ByDeviceType);

/// <summary>
/// A row of the device table. Id, serial number, firmware and audit dates are included so pages that list devices
/// (firmware inventory, replacement queue) do not need a details call per row.
/// </summary>
public sealed record IoTDeviceListItemResponse(
    string DeviceId,
    string DeviceName,
    string DeviceType,
    string Model,
    string ModelDisplayName,
    string Connectivity,
    string Status,
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? Gateway,
    string? SiteId,
    string? SiteName,
    string PowerSource,
    int? BatteryPercent,
    bool LowBattery,
    int? SignalDbm,
    DateTime? LastSeenAt,
    string Health,
    string Id,
    string? SerialNumber,
    string? FirmwareVersion,
    string? PendingFirmwareVersion,
    DateTime? FirmwareUpdateScheduledAt,
    DateTime? CreatedAt,
    DateTime? UpdatedAt)
{
    public static IoTDeviceListItemResponse From(LiveIoTDevice live) => new(
        live.Device.DeviceId,
        live.Device.DeviceName,
        live.Device.DeviceType,
        live.Device.Model,
        live.ModelDisplayName,
        live.Device.Connectivity,
        live.Connection,
        live.Device.AssetRegistrationId,
        live.Device.AssetCode,
        live.Device.AssetName,
        live.Gateway,
        live.Device.SiteId,
        live.Device.SiteName,
        live.Device.PowerSource,
        live.BatteryPercent,
        live.LowBattery,
        live.Reading?.Rssi,
        live.LastSeenAt,
        live.Health,
        live.Device.Id,
        live.Device.SerialNumber,
        live.Device.FirmwareVersion,
        live.Device.PendingFirmwareVersion,
        live.Device.FirmwareUpdateScheduledAt,
        live.Device.CreatedAt,
        live.Device.UpdatedAt);
}

public sealed record DeviceIdentityResponse(
    string DeviceName,
    string DeviceId,
    string? SerialNumber,
    string DeviceType,
    string Model,
    string ModelDisplayName,
    string? Protocol,
    string Connectivity,
    string? FirmwareVersion,
    string? PendingFirmwareVersion,
    DateTime? FirmwareUpdateScheduledAt = null);

public sealed record DeviceAssociationResponse(
    string? AssetRegistrationId,
    string? AssetCode,
    string? AssetName,
    string? Gateway,
    string? SiteId,
    string? SiteName,
    DateTime? LastSeenAt);

/// <summary>Status block; BatteryLabel is "Low" under 50%, SignalQuality Excellent / Good / Fair / Poor.</summary>
public sealed record DeviceStatusResponse(
    string Connection,
    bool NeverReported,
    string PowerSource,
    int? BatteryPercent,
    string? BatteryLabel,
    int? SignalDbm,
    string? SignalQuality,
    string Health,
    double? Latitude,
    double? Longitude);

/// <summary>The device side panel (Overview tab).</summary>
public sealed record IoTDeviceDetailsResponse(
    string Id,
    DeviceIdentityResponse Identity,
    DeviceAssociationResponse Association,
    DeviceStatusResponse Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static IoTDeviceDetailsResponse From(LiveIoTDevice live)
    {
        var device = live.Device;
        return new IoTDeviceDetailsResponse(
            device.Id,
            new DeviceIdentityResponse(
                device.DeviceName, device.DeviceId, device.SerialNumber, device.DeviceType, device.Model,
                live.ModelDisplayName, live.Protocol, device.Connectivity, device.FirmwareVersion, device.PendingFirmwareVersion,
                device.FirmwareUpdateScheduledAt),
            new DeviceAssociationResponse(
                device.AssetRegistrationId, device.AssetCode, device.AssetName, live.Gateway, device.SiteId, device.SiteName, live.LastSeenAt),
            new DeviceStatusResponse(
                live.Connection,
                live.NeverReported,
                device.PowerSource,
                live.BatteryPercent,
                live.LowBattery ? "Low" : live.BatteryPercent is null ? null : "OK",
                live.Reading?.Rssi,
                SignalQualityOf(live.Reading?.Rssi),
                live.Health,
                live.Reading?.Latitude,
                live.Reading?.Longitude),
            device.CreatedBy,
            device.CreatedAt,
            device.UpdatedBy,
            device.UpdatedAt);
    }

    private static string? SignalQualityOf(int? rssi) => rssi switch
    {
        null => null,
        >= -55 => "Excellent",
        >= -70 => "Good",
        >= -85 => "Fair",
        _ => "Poor"
    };
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
