using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Device Maintenance: Device / Gateway, Model, Linked Asset, Site, Maintenance Type,
/// Due Date, Assigned Technician / Vendor, Status. The MaintenanceId (e.g. DM000001) is generated.
/// </summary>
/// <param name="MaintenanceType">One of <see cref="DeviceMaintenanceType.All"/>.</param>
/// <param name="DueDate">Only the date is kept.</param>
/// <param name="AssignedTo">Technician or vendor; leave empty for Unassigned.</param>
/// <param name="Status">One of <see cref="DeviceMaintenanceStatus.All"/>; defaults to Scheduled.</param>
public sealed record CreateDeviceMaintenanceRequest(
    string? DeviceId,
    string? DeviceName,
    string? Model,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string? MaintenanceType,
    DateTime? DueDate,
    string? AssignedTo,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DeviceMaintenance ToEntity(string maintenanceId, CurrentUser? currentUser)
    {
        return new DeviceMaintenance
        {
            MaintenanceId = maintenanceId,
            DeviceId = DeviceId!.Trim(),
            DeviceName = ContractText.TrimOrNull(DeviceName),
            Model = Model!.Trim(),
            AssetRegistrationId = ContractText.TrimOrNull(AssetRegistrationId),
            AssetName = ContractText.TrimOrNull(AssetName),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = SiteName!.Trim(),
            MaintenanceType = ContractText.Canonical(MaintenanceType, DeviceMaintenanceType.All)!,
            DueDate = DueDateOnly!.Value,
            AssignedTo = ContractText.TrimOrNull(AssignedTo),
            Status = ContractText.Canonical(Status, DeviceMaintenanceStatus.All) ?? DeviceMaintenanceStatus.Scheduled,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }

    /// <summary>Kept as UTC midnight so a date sent without a time does not shift a day.</summary>
    public DateTime? DueDateOnly => DueDate is { } date ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc) : null;
}

public sealed record DeviceMaintenanceResponse(
    string Id,
    string MaintenanceId,
    string DeviceId,
    string? DeviceName,
    string Model,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string SiteName,
    string MaintenanceType,
    DateTime DueDate,
    string? AssignedTo,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static DeviceMaintenanceResponse FromEntity(DeviceMaintenance job)
    {
        return new DeviceMaintenanceResponse(
            job.Id,
            job.MaintenanceId,
            job.DeviceId,
            job.DeviceName,
            job.Model,
            job.AssetRegistrationId,
            job.AssetName,
            job.SiteId,
            job.SiteName,
            job.MaintenanceType,
            job.DueDate,
            job.AssignedTo,
            job.Status,
            job.CreatedBy,
            job.CreatedAt,
            job.UpdatedBy,
            job.UpdatedAt,
            job.ClientId,
            job.TenantId);
    }
}
