using A360.Tracking.Domain.Entities;
using A360.Security;

namespace A360.Tracking.Api.Contracts;

/// <summary>
/// Adds a row to Device Maintenance: Device / Gateway, Model, Linked Asset, Site, Maintenance Type,
/// Due Date, Assigned Technician / Vendor, Status. The MaintenanceId (e.g. DM000001) is generated.
/// </summary>
/// <param name="MaintenanceType">One of <see cref="DeviceMaintenanceType.All"/>.</param>
/// <param name="DueDate">Only the date is kept.</param>
/// <param name="Model">Taken from the IoT device when left empty.</param>
/// <param name="SiteName">Taken from the device's previous job when left empty.</param>
/// <param name="AssignedTo">Technician or vendor name; leave empty (or "Unassigned") for Unassigned.</param>
/// <param name="AssignedToId">Id from GET /api/master-data/technicians or /api/master-data/amc-vendors.</param>
/// <param name="AssignedToType">One of <see cref="DeviceMaintenanceAssigneeType.All"/>.</param>
/// <param name="Priority">One of <see cref="DeviceMaintenancePriority.All"/>; defaults to Medium.</param>
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
    string? AssignedToId,
    string? AssignedToType,
    string? Priority,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public DeviceMaintenance ToEntity(string maintenanceId, CurrentUser? currentUser)
    {
        var assignedTo = AssignedToName;

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
            AssignedTo = assignedTo,
            AssignedToId = assignedTo is null ? null : ContractText.TrimOrNull(AssignedToId),
            AssignedToType = assignedTo is null ? null : ContractText.Canonical(AssignedToType, DeviceMaintenanceAssigneeType.All),
            Priority = ContractText.Canonical(Priority, DeviceMaintenancePriority.All) ?? DeviceMaintenancePriority.Medium,
            Description = ContractText.TrimOrNull(Description),
            Status = ContractText.Canonical(Status, DeviceMaintenanceStatus.All) ?? DeviceMaintenanceStatus.Scheduled,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };
    }

    /// <summary>The dropdown's "Unassigned" option means nobody.</summary>
    public string? AssignedToName =>
        string.Equals(AssignedTo?.Trim(), Unassigned, StringComparison.OrdinalIgnoreCase) ? null : ContractText.TrimOrNull(AssignedTo);

    public const string Unassigned = "Unassigned";

    /// <summary>Fills fields the form doesn't ask for from the selected IoT device and its previous job.</summary>
    public CreateDeviceMaintenanceRequest WithDeviceDefaults(IoTDevice? device, DeviceMaintenance? previousJob)
    {
        return this with
        {
            DeviceId = device?.DeviceId ?? DeviceId,
            DeviceName = ContractText.FirstNonEmpty(DeviceName, ContractText.FirstNonEmpty(device?.DeviceName, device?.DeviceType)),
            Model = ContractText.FirstNonEmpty(Model, device?.Model),
            AssetRegistrationId = ContractText.FirstNonEmpty(AssetRegistrationId, device?.AssetRegistrationId),
            AssetName = ContractText.FirstNonEmpty(AssetName, device?.AssetName),
            SiteId = ContractText.FirstNonEmpty(SiteId, string.IsNullOrWhiteSpace(SiteName) ? previousJob?.SiteId : null),
            SiteName = ContractText.FirstNonEmpty(SiteName, previousJob?.SiteName)
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
    string? AssignedToId,
    string? AssignedToType,
    string Priority,
    string? Description,
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
            job.AssignedToId,
            job.AssignedToType,
            job.Priority,
            job.Description,
            job.Status,
            job.CreatedBy,
            job.CreatedAt,
            job.UpdatedBy,
            job.UpdatedAt,
            job.ClientId,
            job.TenantId);
    }
}

/// <summary>Create Service Request → Device dropdown, e.g. "GW-DXB-02 – LoRaWAN Gateway (Dubai Plant)".</summary>
/// <param name="SiteName">From the device's latest job; null when the device has never been serviced.</param>
public sealed record MaintenanceDeviceOptionResponse(
    string DeviceId,
    string DeviceName,
    string DeviceType,
    string Model,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteId,
    string? SiteName,
    string Label)
{
    public static MaintenanceDeviceOptionResponse FromEntity(IoTDevice device, DeviceMaintenance? latestJob)
    {
        var description = ContractText.FirstNonEmpty(device.DeviceName, device.DeviceType);
        var label = description is null ? device.DeviceId : $"{device.DeviceId} – {description}";
        if (!string.IsNullOrWhiteSpace(latestJob?.SiteName))
        {
            label += $" ({latestJob.SiteName})";
        }

        return new MaintenanceDeviceOptionResponse(
            device.DeviceId,
            device.DeviceName,
            device.DeviceType,
            device.Model,
            device.AssetRegistrationId,
            device.AssetName,
            latestJob?.SiteId,
            latestJob?.SiteName,
            label);
    }
}

/// <summary>Create Service Request: the fixed dropdowns in one call.</summary>
public sealed record DeviceMaintenanceOptionsResponse(
    IReadOnlyList<string> MaintenanceTypes,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<string> AssigneeTypes,
    IReadOnlyList<string> Statuses,
    string DefaultMaintenanceType,
    string DefaultPriority)
{
    public static readonly DeviceMaintenanceOptionsResponse Instance = new(
        DeviceMaintenanceType.All,
        DeviceMaintenancePriority.All,
        DeviceMaintenanceAssigneeType.All,
        DeviceMaintenanceStatus.All,
        DeviceMaintenanceType.CorrectiveRepair,
        DeviceMaintenancePriority.Medium);
}
