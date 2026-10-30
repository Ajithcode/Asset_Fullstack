using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Device Health & Status: Device ID / Model, Linked Asset, Site / Gateway,
/// Connectivity, Signal, Battery / Power, Last Seen, Health.
/// </summary>
/// <param name="SignalQuality">One of <see cref="Domain.Entities.SignalQuality.All"/>; worked out from SignalStrengthDbm when omitted.</param>
/// <param name="BatteryLevel">Percent, 0–100; required when PowerSource is Battery.</param>
/// <param name="Health">One of <see cref="DeviceHealthStatus.All"/>.</param>
public sealed record CreateDeviceHealthRequest(
    string? DeviceId,
    string? DeviceModel,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? Gateway,
    string? Connectivity,
    int? SignalStrengthDbm,
    double? SnrDb,
    string? SignalQuality,
    string? PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DeviceHealth ToEntity(CurrentUser? currentUser)
    {
        var powerSource = ContractText.Canonical(PowerSource, DevicePowerSource.All)!;

        return new DeviceHealth
        {
            DeviceId = DeviceId!.Trim(),
            DeviceModel = DeviceModel!.Trim(),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetName = ContractText.TrimOrNull(AssetName),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            Gateway = ContractText.TrimOrNull(Gateway),
            Connectivity = ContractText.Canonical(Connectivity, DeviceConnectivity.All)!,
            SignalStrengthDbm = SignalStrengthDbm,
            SnrDb = SnrDb,
            SignalQuality = ContractText.Canonical(SignalQuality, Domain.Entities.SignalQuality.All)
                ?? (SignalStrengthDbm is { } dbm ? Domain.Entities.SignalQuality.FromDbm(dbm) : null),
            PowerSource = powerSource,
            BatteryLevel = powerSource == DevicePowerSource.Battery ? BatteryLevel : null,
            LastSeenAt = LastSeenAt?.ToUniversalTime(),
            Status = ContractText.Canonical(Health, DeviceHealthStatus.All),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record DeviceHealthResponse(
    string Id,
    string DeviceId,
    string DeviceModel,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? Gateway,
    string Connectivity,
    int? SignalStrengthDbm,
    double? SnrDb,
    string? SignalQuality,
    string PowerSource,
    int? BatteryLevel,
    DateTime? LastSeenAt,
    string? Health,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static DeviceHealthResponse FromEntity(DeviceHealth device)
    {
        return new DeviceHealthResponse(
            device.Id,
            device.DeviceId,
            device.DeviceModel,
            device.AssetRegistrationId,
            device.AssetName,
            device.SiteId,
            device.SiteName,
            device.Gateway,
            device.Connectivity,
            device.SignalStrengthDbm,
            device.SnrDb,
            device.SignalQuality,
            device.PowerSource,
            device.BatteryLevel,
            device.LastSeenAt,
            device.Status,
            device.CreatedBy,
            device.CreatedAt,
            device.ClientId,
            device.TenantId);
    }
}
