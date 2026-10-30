using System.Net.Mail;
using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetAssignmentValidationExtensions
{
    private const int MaxAssets = 100;
    private const int MaxAttachments = 20;
    private const int TextMaxLength = 200;
    private const int RemarksMaxLength = 500;

    /// <summary>
    /// A draft only needs at least one asset; the fields marked * on screen are required when confirming.
    /// Values that are sent are always checked (allowed options, lengths, date order).
    /// </summary>
    public static Dictionary<string, string[]> Validate(this CreateAssetAssignmentRequest request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        // Step 1: Asset Selection.
        var assetIds = ContractText.CleanList(request.AssetRegistrationIds, StringComparer.Ordinal);
        if (assetIds.Count == 0)
        {
            errors["AssetRegistrationIds"] = ["Select at least one asset"];
        }
        else if (assetIds.Count > MaxAssets)
        {
            errors["AssetRegistrationIds"] = [$"At most {MaxAssets} assets can be assigned at once"];
        }

        if (!string.IsNullOrWhiteSpace(request.SelectionMode) && !AssetAssignmentSelectionModes.IsValid(request.SelectionMode))
        {
            errors["SelectionMode"] = [$"SelectionMode must be one of: {string.Join(", ", AssetAssignmentSelectionModes.All)}"];
        }

        // Step 2: Assignment Details.
        var assignTo = AssetAssignmentTargets.Normalize(request.AssignTo);
        if (string.IsNullOrWhiteSpace(request.AssignTo))
        {
            if (!saveAsDraft)
            {
                errors["AssignTo"] = ["AssignTo is required"];
            }
        }
        else if (assignTo is null)
        {
            errors["AssignTo"] = [$"AssignTo must be one of: {string.Join(", ", AssetAssignmentTargets.All)}"];
        }

        if (!saveAsDraft)
        {
            Required(errors, "AssignmentType", request.AssignmentType);

            if (request.FromDate is null)
            {
                errors["FromDate"] = ["FromDate is required"];
            }

            if (!request.IsOpenEnded && request.ToDate is null)
            {
                errors["ToDate"] = ["Enter ToDate or tick Open Ended"];
            }
        }

        if (request.IsOpenEnded && request.ToDate is not null)
        {
            errors["ToDate"] = ["Leave ToDate empty when Open Ended is ticked"];
        }
        else if (request.FromDate is not null && request.ToDate is not null && request.ToDate.Value.Date < request.FromDate.Value.Date)
        {
            errors["ToDate"] = ["ToDate cannot be before FromDate"];
        }

        switch (assignTo)
        {
            case AssetAssignmentTargets.Employee:
                if (!saveAsDraft)
                {
                    Required(errors, "Employee.EmployeeId", request.Employee?.EmployeeId);
                }

                break;

            case AssetAssignmentTargets.Department:
                if (!saveAsDraft)
                {
                    Required(errors, "Department.DepartmentId", request.Department?.DepartmentId);
                }

                break;

            case AssetAssignmentTargets.Location:
                if (!saveAsDraft && string.IsNullOrWhiteSpace(request.Location?.SiteId))
                {
                    errors["Location.SiteId"] = ["Select a location / site"];
                }

                break;

            case AssetAssignmentTargets.OtherEntity:
                ValidateOtherEntity(errors, request.OtherEntity, request.FromDate, saveAsDraft);
                break;
        }

        MaxLength(errors, "AssignmentType", request.AssignmentType, TextMaxLength);
        MaxLength(errors, "CostCenter", request.CostCenter, TextMaxLength);
        MaxLength(errors, "Purpose", request.Purpose, RemarksMaxLength);
        MaxLength(errors, "ApprovalReference", request.ApprovalReference, TextMaxLength);
        MaxLength(errors, "Remarks", request.Remarks, RemarksMaxLength);

        // Step 3: Additional Information.
        MaxLength(errors, "Notes", request.Notes, RemarksMaxLength);
        ValidateAttachments(errors, request.Attachments);
        MaxLength(errors, "Notification.Message", request.Notification?.Message, RemarksMaxLength);

        if (request.Reminder?.IsEnabled == true)
        {
            if (!saveAsDraft && request.Reminder.ReminderDate is null)
            {
                errors["Reminder.ReminderDate"] = ["ReminderDate is required when a reminder is set"];
            }

            MaxLength(errors, "Reminder.Message", request.Reminder.Message, RemarksMaxLength);
        }

        MaxLength(errors, "CustomFields.AssignmentReference", request.CustomFields?.AssignmentReference, TextMaxLength);
        MaxLength(errors, "CustomFields.Priority", request.CustomFields?.Priority, TextMaxLength);
        MaxLength(errors, "CustomFields.AssignmentReason", request.CustomFields?.AssignmentReason, TextMaxLength);

        return errors;
    }

    private static void ValidateOtherEntity(
        Dictionary<string, string[]> errors,
        AssetAssignmentOtherEntityRequest? entity,
        DateTime? fromDate,
        bool saveAsDraft)
    {
        if (entity is null)
        {
            if (!saveAsDraft)
            {
                errors["OtherEntity"] = ["Enter the other entity details"];
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(entity.EntityType) && !AssetAssignmentOtherEntityTypes.IsValid(entity.EntityType))
        {
            errors["OtherEntity.EntityType"] = [$"EntityType must be one of: {string.Join(", ", AssetAssignmentOtherEntityTypes.All)}"];
        }

        if (!saveAsDraft)
        {
            Required(errors, "OtherEntity.EntityType", entity.EntityType);
            Required(errors, "OtherEntity.EntityName", entity.EntityName);

            if (entity.ResponsiblePerson is null
                || (string.IsNullOrWhiteSpace(entity.ResponsiblePerson.Id) && string.IsNullOrWhiteSpace(entity.ResponsiblePerson.Name)))
            {
                errors["OtherEntity.ResponsiblePerson"] = ["Select the responsible person"];
            }
        }

        if (!string.IsNullOrWhiteSpace(entity.ContactEmail) && !MailAddress.TryCreate(entity.ContactEmail.Trim(), out _))
        {
            errors["OtherEntity.ContactEmail"] = ["ContactEmail is not a valid email address"];
        }

        if (fromDate is not null && entity.ExpectedReturnDate is not null && entity.ExpectedReturnDate.Value.Date < fromDate.Value.Date)
        {
            errors["OtherEntity.ExpectedReturnDate"] = ["ExpectedReturnDate cannot be before FromDate"];
        }

        MaxLength(errors, "OtherEntity.EntityName", entity.EntityName, TextMaxLength);
        MaxLength(errors, "OtherEntity.ContactPerson", entity.ContactPerson, TextMaxLength);
        MaxLength(errors, "OtherEntity.ContactPhone", entity.ContactPhone, 30);
        MaxLength(errors, "OtherEntity.AgreementReference", entity.AgreementReference, TextMaxLength);
        MaxLength(errors, "OtherEntity.ServiceRequestNumber", entity.ServiceRequestNumber, TextMaxLength);
        MaxLength(errors, "OtherEntity.ServiceLocation", entity.ServiceLocation, TextMaxLength);
    }

    private static void ValidateAttachments(Dictionary<string, string[]> errors, IReadOnlyCollection<AssetAssignmentAttachmentRequest>? attachments)
    {
        var items = attachments ?? [];
        if (items.Count > MaxAttachments)
        {
            errors["Attachments"] = [$"At most {MaxAttachments} attachments can be added"];
            return;
        }

        var index = 0;
        foreach (var attachment in items)
        {
            Required(errors, $"Attachments[{index}].FileName", attachment.FileName);
            Required(errors, $"Attachments[{index}].FileUrl", attachment.FileUrl);
            index++;
        }
    }

    private static void Required(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field.Split('.').Last()} is required"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field.Split('.').Last()} cannot be longer than {maxLength} characters"];
        }
    }
}
