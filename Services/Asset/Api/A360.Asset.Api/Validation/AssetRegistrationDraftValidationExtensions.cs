using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Validation;

/// <summary>
/// Required fields are enforced only when a step is completed with Next;
/// Save as Draft keeps whatever has been filled in, but values given must still be valid.
/// </summary>
public static class AssetRegistrationDraftValidationExtensions
{
    // Options shown as radio buttons on the Classification step.
    private static readonly OptionList TaggingMethods = AssetRegistrationOptions.TaggingMethods;

    private const int RemarksMaxLength = 500;

    /// <summary>Returns the tagging method spelled as listed (e.g. "qr code" → "QR Code"), or the value unchanged if it is not listed.</summary>
    public static string? NormalizeTaggingMethod(string? taggingMethod)
    {
        return TaggingMethods.Normalize(taggingMethod) ?? taggingMethod;
    }

    public static Dictionary<string, string[]> Validate(this AssetBasicInformationDto? request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        if (saveAsDraft)
        {
            return errors;
        }

        Require(errors, "AssetName", request?.AssetName);
        Require(errors, "AssetTag", request?.AssetTag);
        Require(errors, "Status", request?.Status);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this AssetClassificationDto? request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        if (!saveAsDraft)
        {
            Require(errors, "Category", request?.Category);
            Require(errors, "SubCategory", request?.SubCategory);
            Require(errors, "AssetType", request?.AssetType);
            Require(errors, "Criticality", request?.Criticality);
            Require(errors, "Department", request?.Department);
            Require(errors, "TaggingMethod", request?.TaggingMethod);
            Require(errors, "LifecycleStage", request?.LifecycleStage);
        }

        if (!string.IsNullOrWhiteSpace(request?.TaggingMethod) && !TaggingMethods.IsValid(request.TaggingMethod))
        {
            errors["TaggingMethod"] = [$"TaggingMethod must be one of: {string.Join(", ", TaggingMethods.All)}"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this AssetLocationAssignmentDto? request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        if (!saveAsDraft)
        {
            // Location levels are picked from the hierarchy, so their ids are what must be present.
            Require(errors, "Location.CountryId", request?.Location?.CountryId);
            Require(errors, "Location.SiteId", request?.Location?.SiteId);
            Require(errors, "Location.BuildingId", request?.Location?.BuildingId);
            Require(errors, "Location.FloorId", request?.Location?.FloorId);
            Require(errors, "Assignment.CustodianDepartment", request?.Assignment?.CustodianDepartment);
        }

        if (request?.Assignment?.AssignmentStartDate is { } startDate
            && request.Assignment.AssignmentEndDate is { } endDate
            && endDate.Date < startDate.Date)
        {
            errors["Assignment.AssignmentEndDate"] = ["AssignmentEndDate cannot be before AssignmentStartDate"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this AssetPurchaseWarrantyDto? request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();
        var purchase = request?.Purchase;
        var warranty = request?.Warranty;

        if (!saveAsDraft)
        {
            Require(errors, "Purchase.PurchaseOrderNumber", purchase?.PurchaseOrderNumber);
            Require(errors, "Purchase.SupplierVendor", purchase?.SupplierVendor);
            Require(errors, "Purchase.Manufacturer", purchase?.Manufacturer);

            if (purchase?.PurchaseDate is null)
            {
                errors["Purchase.PurchaseDate"] = ["PurchaseDate is required"];
            }

            if (purchase?.PurchaseCost is null)
            {
                errors["Purchase.PurchaseCost"] = ["PurchaseCost is required"];
            }

            if (purchase?.Quantity is null)
            {
                errors["Purchase.Quantity"] = ["Quantity is required"];
            }
        }

        if (purchase is not null)
        {
            if (purchase.PurchaseCost < 0)
            {
                errors["Purchase.PurchaseCost"] = ["PurchaseCost cannot be negative"];
            }

            if (purchase.Quantity <= 0)
            {
                errors["Purchase.Quantity"] = ["Quantity must be greater than 0"];
            }

            if (purchase.TaxPercent is < 0 or > 100)
            {
                errors["Purchase.TaxPercent"] = ["TaxPercent must be between 0 and 100"];
            }

            if (purchase.PurchaseDate is { } purchaseDate && purchase.DeliveryDate is { } deliveryDate && deliveryDate.Date < purchaseDate.Date)
            {
                errors["Purchase.DeliveryDate"] = ["DeliveryDate cannot be before PurchaseDate"];
            }

            if (purchase.Remarks?.Trim().Length > RemarksMaxLength)
            {
                errors["Purchase.Remarks"] = [$"Remarks cannot be longer than {RemarksMaxLength} characters"];
            }
        }

        ValidateDocuments(errors, "Documents", request?.Documents);

        if (warranty is not null)
        {
            if (warranty.WarrantyPeriod <= 0)
            {
                errors["Warranty.WarrantyPeriod"] = ["WarrantyPeriod must be greater than 0"];
            }

            if (!string.IsNullOrWhiteSpace(warranty.WarrantyPeriodUnit) && !WarrantyPeriodUnits.IsValid(warranty.WarrantyPeriodUnit))
            {
                errors["Warranty.WarrantyPeriodUnit"] = [$"WarrantyPeriodUnit must be one of: {string.Join(", ", WarrantyPeriodUnits.All)}"];
            }
            else if (warranty.WarrantyPeriod is not null && string.IsNullOrWhiteSpace(warranty.WarrantyPeriodUnit))
            {
                errors["Warranty.WarrantyPeriodUnit"] = ["WarrantyPeriodUnit is required when WarrantyPeriod is given"];
            }

            // When a period is given the end date is calculated, so only a manually entered one needs checking.
            if (warranty.WarrantyPeriod is null
                && warranty.WarrantyStartDate is { } warrantyStartDate
                && warranty.WarrantyEndDate is { } warrantyEndDate
                && warrantyEndDate.Date < warrantyStartDate.Date)
            {
                errors["Warranty.WarrantyEndDate"] = ["WarrantyEndDate cannot be before WarrantyStartDate"];
            }
        }

        return errors;
    }

    // Nothing on the Additional Information step is required; only the values given are checked.
    public static Dictionary<string, string[]> Validate(this AssetAdditionalInformationDto? request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        if (request is null)
        {
            return errors;
        }

        if (request.Technical is { } technical)
        {
            if (technical.DimensionLength <= 0 || technical.DimensionWidth <= 0 || technical.DimensionHeight <= 0)
            {
                errors["Technical.Dimensions"] = ["Dimensions must be greater than 0"];
            }

            if (technical.Weight <= 0)
            {
                errors["Technical.Weight"] = ["Weight must be greater than 0"];
            }

            if (technical.ExpectedLifespan <= 0)
            {
                errors["Technical.ExpectedLifespan"] = ["ExpectedLifespan must be greater than 0"];
            }

            if (technical.TechnicalRemarks?.Trim().Length > RemarksMaxLength)
            {
                errors["Technical.TechnicalRemarks"] = [$"TechnicalRemarks cannot be longer than {RemarksMaxLength} characters"];
            }
        }

        if (request.Operational is { } operational)
        {
            if (operational.OperatingHoursPerDay is < 0 or > 24)
            {
                errors["Operational.OperatingHoursPerDay"] = ["OperatingHoursPerDay must be between 0 and 24"];
            }

            if (operational.MaintenanceFrequency <= 0)
            {
                errors["Operational.MaintenanceFrequency"] = ["MaintenanceFrequency must be greater than 0"];
            }

            if (operational.LastMaintenanceDate is { } lastMaintenanceDate
                && operational.NextMaintenanceDate is { } nextMaintenanceDate
                && nextMaintenanceDate.Date < lastMaintenanceDate.Date)
            {
                errors["Operational.NextMaintenanceDate"] = ["NextMaintenanceDate cannot be before LastMaintenanceDate"];
            }
        }

        var duplicateFieldNames = (request.CustomFields ?? [])
            .Where(field => !string.IsNullOrWhiteSpace(field?.FieldName))
            .GroupBy(field => field.FieldName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateFieldNames.Count > 0)
        {
            errors["CustomFields"] = [$"Custom field names must be unique: {string.Join(", ", duplicateFieldNames)}"];
        }

        ValidateDocuments(errors, "Attachments", request.Attachments);

        return errors;
    }

    /// <summary>
    /// Re-checks every step as if each had been completed with Next. Errors are keyed by step,
    /// e.g. "Classification.Department", so the wizard can jump to the step that needs fixing.
    /// </summary>
    public static Dictionary<string, string[]> ValidateForSubmit(this AssetRegistrationDraft draft)
    {
        var errors = new Dictionary<string, string[]>();

        AddStepErrors(errors, "BasicInformation", AssetBasicInformationDto.FromSection(draft.BasicInformation).Validate(saveAsDraft: false));
        AddStepErrors(errors, "Classification", AssetClassificationDto.FromSection(draft.Classification).Validate(saveAsDraft: false));
        AddStepErrors(errors, "LocationAssignment", AssetLocationAssignmentDto.FromSection(draft.LocationAssignment).Validate(saveAsDraft: false));
        AddStepErrors(errors, "PurchaseWarranty", AssetPurchaseWarrantyDto.FromSection(draft.PurchaseWarranty).Validate(saveAsDraft: false));
        AddStepErrors(errors, "AdditionalInformation", AssetAdditionalInformationDto.FromSection(draft.AdditionalInformation).Validate(saveAsDraft: false));

        return errors;
    }

    private static void AddStepErrors(Dictionary<string, string[]> errors, string step, Dictionary<string, string[]> stepErrors)
    {
        foreach (var (field, messages) in stepErrors)
        {
            errors[$"{step}.{field}"] = messages;
        }
    }

    private static void ValidateDocuments(Dictionary<string, string[]> errors, string field, IEnumerable<AssetDocumentDto?>? documents)
    {
        if (documents?.Any(document => document is null || string.IsNullOrWhiteSpace(document.Url) || string.IsNullOrWhiteSpace(document.FileName)) == true)
        {
            errors[field] = ["Each document needs a FileName and the Url returned by the media upload"];
        }
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }
}
