using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// Confirm Assignment (or Save as Draft) on step 4 of the Assign Asset wizard: everything entered in steps 1 to 3.
/// Only the section matching AssignTo is read: Employee, Department, Location or OtherEntity.
/// </summary>
/// <param name="SelectionMode">"Single", "Multiple" or "Scan".</param>
/// <param name="AssetRegistrationIds">The selected assets; their names, tags and locations are loaded on the server.</param>
/// <param name="AssignTo">One of AssetAssignmentTargets.All.</param>
/// <param name="ToDate">Leave empty when IsOpenEnded.</param>
/// <param name="Location">
/// For "Location": the chosen site. For Employee / Department: the Location / Site, Building, Floor and Room fields.
/// </param>
/// <param name="Remarks">Assignment Notes on step 2.</param>
/// <param name="Notes">Notes / Remarks on step 3.</param>
public sealed record CreateAssetAssignmentRequest(
    string? SelectionMode,
    IReadOnlyCollection<string>? AssetRegistrationIds,
    string? AssignTo,
    string? AssignmentType,
    DateTime? FromDate,
    DateTime? ToDate,
    bool IsOpenEnded,
    AssetAssignmentEmployeeRequest? Employee,
    AssetAssignmentDepartmentRequest? Department,
    AssetAssignmentOtherEntityRequest? OtherEntity,
    AssetAssignmentLocationRequest? Location,
    string? CostCenter,
    string? Purpose,
    string? ApprovalReference,
    string? Remarks,
    string? Notes,
    IReadOnlyCollection<AssetAssignmentAttachmentRequest>? Attachments,
    AssetAssignmentNotificationRequest? Notification,
    AssetAssignmentReminderRequest? Reminder,
    AssetAssignmentCustomFieldsRequest? CustomFields,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetAssignmentEmployeeRequest(
    string? EmployeeId,
    string? EmployeeCode,
    string? EmployeeName,
    string? Designation,
    string? DepartmentId,
    string? DepartmentName,
    AssetAssignmentPersonRequest? Manager)
{
    public AssetAssignmentEmployee ToEntity()
    {
        return new AssetAssignmentEmployee
        {
            EmployeeId = EmployeeId?.Trim() ?? string.Empty,
            EmployeeCode = ContractText.TrimOrNull(EmployeeCode),
            EmployeeName = ContractText.TrimOrNull(EmployeeName),
            Designation = ContractText.TrimOrNull(Designation),
            DepartmentId = ContractText.TrimOrNull(DepartmentId),
            DepartmentName = ContractText.TrimOrNull(DepartmentName),
            Manager = Manager?.ToEntity()
        };
    }
}

public sealed record AssetAssignmentDepartmentRequest(
    string? DepartmentId,
    string? DepartmentName,
    AssetAssignmentPersonRequest? Manager)
{
    public AssetAssignmentDepartment ToEntity()
    {
        return new AssetAssignmentDepartment
        {
            DepartmentId = DepartmentId?.Trim() ?? string.Empty,
            DepartmentName = ContractText.TrimOrNull(DepartmentName),
            Manager = Manager?.ToEntity()
        };
    }
}

public sealed record AssetAssignmentPersonRequest(
    string? Id,
    string? Code,
    string? Name,
    string? Designation)
{
    /// <summary>Null when nothing was picked, so an empty manager is not stored.</summary>
    public AssetAssignmentPerson? ToEntity()
    {
        if (string.IsNullOrWhiteSpace(Id) && string.IsNullOrWhiteSpace(Name))
        {
            return null;
        }

        return new AssetAssignmentPerson
        {
            Id = ContractText.TrimOrNull(Id),
            Code = ContractText.TrimOrNull(Code),
            Name = ContractText.TrimOrNull(Name),
            Designation = ContractText.TrimOrNull(Designation)
        };
    }
}

/// <param name="EntityType">One of AssetAssignmentOtherEntityTypes.All (the tabs).</param>
/// <param name="AgreementReference">Agreement / PO.</param>
/// <param name="Reason">Reason for Assignment.</param>
/// <param name="ServiceRequestNumber">Service Request / Work Order.</param>
public sealed record AssetAssignmentOtherEntityRequest(
    string? EntityType,
    string? EntityId,
    string? EntityName,
    string? ContactPerson,
    string? ContactPhone,
    string? ContactEmail,
    string? AgreementReference,
    string? Reason,
    string? ServiceType,
    string? ServiceRequestNumber,
    string? ServiceLocation,
    DateTime? ExpectedReturnDate,
    string? ReturnCondition,
    AssetAssignmentPersonRequest? ResponsiblePerson)
{
    public AssetAssignmentOtherEntity ToEntity()
    {
        return new AssetAssignmentOtherEntity
        {
            EntityType = AssetAssignmentOtherEntityTypes.Normalize(EntityType),
            EntityId = ContractText.TrimOrNull(EntityId),
            EntityName = EntityName?.Trim() ?? string.Empty,
            ContactPerson = ContractText.TrimOrNull(ContactPerson),
            ContactPhone = ContractText.TrimOrNull(ContactPhone),
            ContactEmail = ContractText.TrimOrNull(ContactEmail),
            AgreementReference = ContractText.TrimOrNull(AgreementReference),
            Reason = ContractText.TrimOrNull(Reason),
            ServiceType = ContractText.TrimOrNull(ServiceType),
            ServiceRequestNumber = ContractText.TrimOrNull(ServiceRequestNumber),
            ServiceLocation = ContractText.TrimOrNull(ServiceLocation),
            ExpectedReturnDate = ContractText.DateOnly(ExpectedReturnDate),
            ReturnCondition = ContractText.TrimOrNull(ReturnCondition),
            ResponsiblePerson = ResponsiblePerson?.ToEntity()
        };
    }
}

public sealed record AssetAssignmentLocationRequest(
    string? SiteId,
    string? SiteCode,
    string? SiteName,
    string? BuildingId,
    string? BuildingName,
    string? FloorId,
    string? FloorName,
    string? RoomId,
    string? RoomName)
{
    /// <summary>Null when no level was picked.</summary>
    public AssetAssignmentLocation? ToEntity()
    {
        var levels = new[] { SiteName, BuildingName, FloorName, RoomName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToList();

        if (levels.Count == 0 && string.IsNullOrWhiteSpace(SiteId))
        {
            return null;
        }

        return new AssetAssignmentLocation
        {
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteCode = ContractText.TrimOrNull(SiteCode),
            SiteName = ContractText.TrimOrNull(SiteName),
            BuildingId = ContractText.TrimOrNull(BuildingId),
            BuildingName = ContractText.TrimOrNull(BuildingName),
            FloorId = ContractText.TrimOrNull(FloorId),
            FloorName = ContractText.TrimOrNull(FloorName),
            RoomId = ContractText.TrimOrNull(RoomId),
            RoomName = ContractText.TrimOrNull(RoomName),
            LocationPath = levels.Count == 0 ? null : string.Join(" > ", levels)
        };
    }
}

/// <summary>A file already uploaded through the Media service.</summary>
public sealed record AssetAssignmentAttachmentRequest(
    string? FileName,
    string? FileUrl,
    string? ContentType,
    long? FileSize)
{
    public AssetAssignmentAttachment ToEntity()
    {
        return new AssetAssignmentAttachment
        {
            FileName = FileName?.Trim() ?? string.Empty,
            FileUrl = FileUrl?.Trim() ?? string.Empty,
            ContentType = ContractText.TrimOrNull(ContentType),
            FileSize = FileSize
        };
    }
}

public sealed record AssetAssignmentNotificationRequest(
    bool NotifyAssignees,
    string? Message,
    bool NotifyManager,
    bool NotifyDepartment)
{
    public AssetAssignmentNotification ToEntity()
    {
        return new AssetAssignmentNotification
        {
            NotifyAssignees = NotifyAssignees,
            Message = ContractText.TrimOrNull(Message),
            NotifyManager = NotifyManager,
            NotifyDepartment = NotifyDepartment
        };
    }
}

/// <param name="IsEnabled">"Set a reminder" checkbox.</param>
public sealed record AssetAssignmentReminderRequest(
    bool IsEnabled,
    DateTime? ReminderDate,
    string? Message)
{
    public AssetAssignmentReminder ToEntity()
    {
        return new AssetAssignmentReminder
        {
            IsEnabled = IsEnabled,
            ReminderDate = IsEnabled ? ContractText.DateOnly(ReminderDate) : null,
            Message = IsEnabled ? ContractText.TrimOrNull(Message) : null
        };
    }
}

public sealed record AssetAssignmentCustomFieldsRequest(
    string? AssignmentReference,
    string? RequestorId,
    string? RequestorName,
    string? Priority,
    string? AssignmentReason)
{
    public AssetAssignmentCustomFields ToEntity()
    {
        return new AssetAssignmentCustomFields
        {
            AssignmentReference = ContractText.TrimOrNull(AssignmentReference),
            RequestorId = ContractText.TrimOrNull(RequestorId),
            RequestorName = ContractText.TrimOrNull(RequestorName),
            Priority = ContractText.TrimOrNull(Priority),
            AssignmentReason = ContractText.TrimOrNull(AssignmentReason)
        };
    }
}

public sealed record DeleteAssetAssignmentsRequest(IReadOnlyCollection<string>? Ids);

/// <param name="StatusCounts">Counts per status (Draft, Active) for the same filters except Status.</param>
public sealed record AssetAssignmentListResponse(
    IReadOnlyCollection<AssetAssignmentResponse> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts,
    int Page,
    int PageSize);

/// <summary>Fixed options for the wizard's radio buttons and tabs.</summary>
public sealed record AssetAssignmentOptionsResponse(
    IEnumerable<string> AssignTo,
    IEnumerable<string> OtherEntityTypes,
    IEnumerable<string> SelectionModes);

public static class AssetAssignmentTargets
{
    public const string Employee = "Employee";
    public const string Department = "Department";
    public const string Location = "Location";
    public const string OtherEntity = "Other Entity";

    public static IEnumerable<string> All => [Employee, Department, Location, OtherEntity];

    public static bool IsValid(string? target) => Normalize(target) is not null;

    /// <summary>Standard spelling, e.g. "other entity" as "Other Entity"; null when not recognised.</summary>
    public static string? Normalize(string? target) =>
        All.FirstOrDefault(value => string.Equals(value, target?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class AssetAssignmentOtherEntityTypes
{
    public static IEnumerable<string> All =>
        ["Vendor / Supplier", "Contractor", "Customer / Client", "Partner", "Project", "Cost Center", "Other"];

    public static bool IsValid(string? type) =>
        All.Any(value => string.Equals(value, type?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? type) =>
        All.FirstOrDefault(value => string.Equals(value, type?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Other";
}

public static class AssetAssignmentSelectionModes
{
    // Set on assignments made by the Bulk Assignment wizard; not offered in the Assign Asset wizard.
    public const string BulkUpload = "Bulk Upload";

    public static IEnumerable<string> All => ["Single", "Multiple", "Scan"];

    public static bool IsValid(string? mode) =>
        All.Any(value => string.Equals(value, mode?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? mode) =>
        All.FirstOrDefault(value => string.Equals(value, mode?.Trim(), StringComparison.OrdinalIgnoreCase));
}

internal static class AssetAssignmentAssets
{
    public static AssetAssignmentAsset FromAsset(AssetRegistration asset)
    {
        return new AssetAssignmentAsset
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetName = asset.BasicInformation.AssetName,
            AssetTag = asset.BasicInformation.AssetTag,
            SerialNumber = asset.BasicInformation.SerialNumber,
            Model = asset.BasicInformation.Model,
            Manufacturer = asset.BasicInformation.Manufacturer,
            Category = asset.Classification.Category,
            AssetType = asset.Classification.AssetType,
            CurrentLocation = asset.LocationAssignment.Location.LocationPath,
            PreviousStatus = asset.BasicInformation.Status
        };
    }

    /// <summary>The employee, department, site or entity name the assets went to.</summary>
    public static string? AssignedToName(AssetAssignmentRecord assignment) => assignment.AssignTo switch
    {
        AssetAssignmentTargets.Employee => assignment.Employee?.EmployeeName ?? assignment.Employee?.EmployeeId,
        AssetAssignmentTargets.Department => assignment.Department?.DepartmentName ?? assignment.Department?.DepartmentId,
        AssetAssignmentTargets.Location => assignment.Location?.LocationPath ?? assignment.Location?.SiteId,
        AssetAssignmentTargets.OtherEntity => assignment.OtherEntity?.EntityName,
        _ => null
    };
}

/// <param name="AssignedToName">The employee, department, site or entity name, for the summary panel.</param>
public sealed record AssetAssignmentResponse(
    string Id,
    string AssignmentNumber,
    string? Status,
    string? SelectionMode,
    string? BulkAssignmentId,
    int AssetCount,
    IReadOnlyCollection<AssetAssignmentAsset> Assets,
    string AssignTo,
    string? AssignedToName,
    string? AssignmentType,
    DateTime? FromDate,
    DateTime? ToDate,
    bool IsOpenEnded,
    AssetAssignmentEmployee? Employee,
    AssetAssignmentDepartment? Department,
    AssetAssignmentOtherEntity? OtherEntity,
    AssetAssignmentLocation? Location,
    string? CostCenter,
    string? Purpose,
    string? ApprovalReference,
    string? Remarks,
    string? Notes,
    IReadOnlyCollection<AssetAssignmentAttachment> Attachments,
    AssetAssignmentNotification Notification,
    AssetAssignmentReminder Reminder,
    AssetAssignmentCustomFields CustomFields,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetAssignmentResponse FromEntity(AssetAssignmentRecord assignment)
    {
        return new AssetAssignmentResponse(
            assignment.Id,
            assignment.AssignmentNumber,
            assignment.Status,
            assignment.SelectionMode,
            assignment.BulkAssignmentId,
            assignment.Assets.Count,
            assignment.Assets,
            assignment.AssignTo,
            AssetAssignmentAssets.AssignedToName(assignment),
            assignment.AssignmentType,
            assignment.FromDate,
            assignment.ToDate,
            assignment.IsOpenEnded,
            assignment.Employee,
            assignment.Department,
            assignment.OtherEntity,
            assignment.Location,
            assignment.CostCenter,
            assignment.Purpose,
            assignment.ApprovalReference,
            assignment.Remarks,
            assignment.Notes,
            assignment.Attachments,
            assignment.Notification,
            assignment.Reminder,
            assignment.CustomFields,
            assignment.CreatedBy,
            assignment.CreatedAt,
            assignment.UpdatedBy,
            assignment.UpdatedAt,
            assignment.ClientId,
            assignment.TenantId);
    }
}
