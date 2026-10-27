using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>Adds a row to Firmware Versions by Device Model: Device Model, Current Version, Latest Version, Devices, Update Status, Release Date.</summary>
/// <param name="Status">Update status, one of <see cref="FirmwareUpdateStatus.All"/>; worked out from the two versions when omitted.</param>
/// <param name="ReleaseDate">Release date of the latest version; only the date is kept.</param>
public sealed record CreateFirmwareVersionRequest(
    string? DeviceModel,
    string? CurrentVersion,
    string? LatestVersion,
    int? DeviceCount,
    string? Status,
    DateTime? ReleaseDate,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public FirmwareVersion ToEntity(CurrentUser? currentUser)
    {
        var currentVersion = CurrentVersion!.Trim();
        var latestVersion = LatestVersion!.Trim();

        return new FirmwareVersion
        {
            DeviceModel = DeviceModel!.Trim(),
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            DeviceCount = DeviceCount ?? 0,
            Status = ContractText.Canonical(Status, FirmwareUpdateStatus.All)
                ?? (string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase)
                    ? FirmwareUpdateStatus.UpToDate
                    : FirmwareUpdateStatus.UpdateAvailable),
            // Kept as UTC midnight so a date sent without a time does not shift a day.
            ReleaseDate = ReleaseDate is { } date ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc) : null,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }
}

public sealed record FirmwareVersionResponse(
    string Id,
    string DeviceModel,
    string CurrentVersion,
    string LatestVersion,
    int DeviceCount,
    string? Status,
    DateTime? ReleaseDate,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? ClientId,
    string? TenantId)
{
    public static FirmwareVersionResponse FromEntity(FirmwareVersion firmware)
    {
        return new FirmwareVersionResponse(
            firmware.Id,
            firmware.DeviceModel,
            firmware.CurrentVersion,
            firmware.LatestVersion,
            firmware.DeviceCount,
            firmware.Status,
            firmware.ReleaseDate,
            firmware.CreatedBy,
            firmware.CreatedAt,
            firmware.ClientId,
            firmware.TenantId);
    }
}
