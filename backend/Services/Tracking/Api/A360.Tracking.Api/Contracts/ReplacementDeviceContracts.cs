using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Adds a row to Replacement Device: Device ID, Model, Availability, Compatibility, Protocol, Battery, Location.</summary>
/// <param name="Availability">One of <see cref="ReplacementAvailability.All"/>; defaults to Available.</param>
/// <param name="Compatibility">One of <see cref="ReplacementCompatibility.All"/>.</param>
/// <param name="BatteryLevel">Percent, 0–100; leave empty for mains-powered devices.</param>
public sealed record CreateReplacementDeviceRequest(
    string? DeviceId,
    string? Model,
    string? Availability,
    string? Compatibility,
    string? Protocol,
    int? BatteryLevel,
    string? LocationName,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ReplacementDevice ToEntity(CurrentUser? currentUser)
    {
        return new ReplacementDevice
        {
            DeviceId = DeviceId!.Trim(),
            Model = Model!.Trim(),
            Status = ContractText.Canonical(Availability, ReplacementAvailability.All) ?? ReplacementAvailability.Available,
            Compatibility = ContractText.Canonical(Compatibility, ReplacementCompatibility.All)!,
            Protocol = ContractText.TrimOrNull(Protocol),
            BatteryLevel = BatteryLevel,
            LocationName = LocationName!.Trim(),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record ReplacementDeviceResponse(
    string Id,
    string DeviceId,
    string Model,
    string? Availability,
    string Compatibility,
    string? Protocol,
    int? BatteryLevel,
    string LocationName,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static ReplacementDeviceResponse FromEntity(ReplacementDevice device)
    {
        return new ReplacementDeviceResponse(
            device.Id,
            device.DeviceId,
            device.Model,
            device.Status,
            device.Compatibility,
            device.Protocol,
            device.BatteryLevel,
            device.LocationName,
            device.CreatedBy,
            device.CreatedAt,
            device.UpdatedBy,
            device.UpdatedAt,
            device.ClientId,
            device.TenantId);
    }
}
