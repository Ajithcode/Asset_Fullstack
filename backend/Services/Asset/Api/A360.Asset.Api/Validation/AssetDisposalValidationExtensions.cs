using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Validation;

/// <summary>
/// Rules for each step of the New Disposal Request wizard, checked on the values as they will be saved.
/// A draft (Save as Draft) skips the fields marked * on screen, except the request title; values that are sent are
/// always checked. Submit runs every step's rules as if none were a draft. Whether the assets exist and are free
/// is checked by the endpoints.
/// </summary>
public static class AssetDisposalValidationExtensions
{
    public const int MaxAssets = 500;
    public const int MaxApprovers = 10;
    public const int MaxAttachments = 20;
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;
    public const decimal MaxValue = 1_000_000_000_000m;

    private const int TitleMaxLength = 200;
    private const int ShortTextMaxLength = 100;
    private const int PhoneMaxLength = 30;
    private const int RemarksMaxLength = 500;
    private const int JustificationMaxLength = 1000;

    private static readonly string[] AttachmentTypes = ["PDF", "DOC", "DOCX", "XLS", "XLSX", "JPG", "JPEG", "PNG", "ZIP"];

    /// <summary>Step 1: Request Details.</summary>
    public static void ValidateDetails(this AssetDisposalRequest request, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        Required(errors, prefix + "RequestTitle", request.RequestTitle);
        MaxLength(errors, prefix + "RequestTitle", request.RequestTitle, TitleMaxLength);

        if (!saveAsDraft)
        {
            if (request.RequestDate is null)
            {
                errors[prefix + "RequestDate"] = ["RequestDate is required"];
            }

            Required(errors, prefix + "Justification", request.Justification);
        }

        Option(errors, prefix + "Reason", request.Reason, AssetDisposalOptions.Reasons, saveAsDraft);
        Option(errors, prefix + "Priority", request.Priority, AssetDisposalOptions.Priorities, saveAsDraft: true);
        MaxLength(errors, prefix + "RequestedBy.Name", request.RequestedBy?.Name, TitleMaxLength);
        MaxLength(errors, prefix + "DisposalCategory", request.DisposalCategory, ShortTextMaxLength);
        MaxLength(errors, prefix + "Justification", request.Justification, JustificationMaxLength);

        if (request.Currency.Length != 3 || !request.Currency.All(char.IsLetter))
        {
            errors[prefix + "Currency"] = ["Currency must be a 3-letter code such as AED"];
        }
    }

    /// <summary>Step 2: Select Assets.</summary>
    public static void ValidateAssets(this AssetDisposalRequest request, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        if (request.Assets.Count > MaxAssets)
        {
            errors[prefix + "Assets"] = [$"At most {MaxAssets} assets can be in one disposal request"];
            return;
        }

        if (!saveAsDraft && request.Assets.Count == 0)
        {
            errors[prefix + "Assets"] = ["Select at least one asset"];
        }

        var index = 0;
        foreach (var asset in request.Assets)
        {
            if (asset.EstimatedValue is < 0 or > MaxValue)
            {
                errors[$"{prefix}Assets[{index}].EstimatedValue"] = ["EstimatedValue must be 0 or more"];
            }

            index++;
        }
    }

    /// <summary>Step 3: Disposal Method.</summary>
    public static void ValidateMethod(this AssetDisposalRequest request, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        var method = request.Method;
        Option(errors, prefix + "Method.Method", method.Method, AssetDisposalOptions.MethodNames, saveAsDraft);

        if (!saveAsDraft)
        {
            if (method.ExpectedDisposalDate is null)
            {
                errors[prefix + "Method.ExpectedDisposalDate"] = ["ExpectedDisposalDate is required"];
            }

            if (AssetDisposalOptions.VendorRequired(method.Method) && string.IsNullOrWhiteSpace(method.Vendor?.Name))
            {
                errors[prefix + "Method.Vendor"] = [$"Select the vendor for {method.Method}"];
            }
        }

        if (method.ExpectedDisposalDate < request.RequestDate)
        {
            errors[prefix + "Method.ExpectedDisposalDate"] = ["ExpectedDisposalDate cannot be before RequestDate"];
        }

        MaxLength(errors, prefix + "Method.Vendor.Name", method.Vendor?.Name, TitleMaxLength);
        MaxLength(errors, prefix + "Method.Vendor.Address", method.Vendor?.Address, RemarksMaxLength);
        MaxLength(errors, prefix + "Method.Vendor.TradeLicense", method.Vendor?.TradeLicense, ShortTextMaxLength);
        MaxLength(errors, prefix + "Method.ContactPerson", method.ContactPerson, ShortTextMaxLength);
        MaxLength(errors, prefix + "Method.ContactNumber", method.ContactNumber, PhoneMaxLength);
        MaxLength(errors, prefix + "Method.Instructions", method.Instructions, RemarksMaxLength);
    }

    /// <summary>Step 4: Approval Workflow, Attachments and Additional Remarks.</summary>
    public static void ValidateApproval(this AssetDisposalRequest request, Dictionary<string, string[]> errors, bool saveAsDraft, string prefix = "")
    {
        if (request.Approvers.Count > MaxApprovers)
        {
            errors[prefix + "Approvers"] = [$"At most {MaxApprovers} approval levels can be added"];
        }
        else if (!saveAsDraft && request.Approvers.Count == 0)
        {
            errors[prefix + "Approvers"] = ["Add at least one approver"];
        }
        else if (!saveAsDraft && request.Approvers.All(approver => approver.ApprovalType != AssetDisposalOptions.Mandatory))
        {
            errors[prefix + "Approvers"] = ["At least one approval level must be Mandatory"];
        }

        var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var approver in request.Approvers)
        {
            var key = $"{prefix}Approvers[{index}]";
            Required(errors, key + ".ApprovalLevel", approver.ApprovalLevel);
            MaxLength(errors, key + ".ApprovalLevel", approver.ApprovalLevel, ShortTextMaxLength);
            Required(errors, key + ".Approver", approver.Approver.Name);
            Option(errors, key + ".ApprovalType", approver.ApprovalType, AssetDisposalOptions.ApprovalTypes, saveAsDraft: false);

            var person = approver.Approver.UserId ?? approver.Approver.Name;
            if (!string.IsNullOrWhiteSpace(person) && !people.Add(person))
            {
                errors[key + ".Approver"] = [$"{approver.Approver.Name} is already an approver at another level"];
            }

            index++;
        }

        ValidateAttachments(errors, request.Attachments, prefix + "Attachments");
        MaxLength(errors, prefix + "Remarks", request.Remarks, RemarksMaxLength);
    }

    /// <summary>Submit: every step, as if none were a draft. Keys start with the step, e.g. "Approval.Approvers".</summary>
    public static Dictionary<string, string[]> ValidateForSubmit(this AssetDisposalRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        request.ValidateDetails(errors, saveAsDraft: false, "Details.");
        request.ValidateAssets(errors, saveAsDraft: false, "Assets.");
        request.ValidateMethod(errors, saveAsDraft: false, "Method.");
        request.ValidateApproval(errors, saveAsDraft: false, "Approval.");
        return errors;
    }

    /// <summary>Completing: the actual date, recovered value and certificate.</summary>
    public static Dictionary<string, string[]> Validate(this CompleteAssetDisposalRequest request, DateTime? requestDate)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ActualDisposalDate is null)
        {
            errors["ActualDisposalDate"] = ["ActualDisposalDate is required"];
        }
        else if (request.ActualDisposalDate.Value.Date < requestDate)
        {
            errors["ActualDisposalDate"] = ["ActualDisposalDate cannot be before the request date"];
        }

        if (request.RecoveredValue is < 0 or > MaxValue)
        {
            errors["RecoveredValue"] = ["RecoveredValue must be 0 or more"];
        }

        MaxLength(errors, "CertificateNumber", request.CertificateNumber, ShortTextMaxLength);
        MaxLength(errors, "Remarks", request.Remarks, RemarksMaxLength);

        if (request.Certificate is not null)
        {
            ValidateAttachments(errors, [request.Certificate.ToEntity(null)], "Certificate");
        }

        return errors;
    }

    private static void ValidateAttachments(Dictionary<string, string[]> errors, IReadOnlyCollection<AssetDisposalAttachment> attachments, string field)
    {
        if (attachments.Count > MaxAttachments)
        {
            errors[field] = [$"At most {MaxAttachments} attachments can be added"];
            return;
        }

        var index = 0;
        foreach (var attachment in attachments)
        {
            var key = $"{field}[{index}]";
            Required(errors, key + ".FileName", attachment.FileName);
            Required(errors, key + ".FileUrl", attachment.FileUrl);
            MaxLength(errors, key + ".Description", attachment.Description, TitleMaxLength);

            if (attachment.FileType is not null && !AttachmentTypes.Contains(attachment.FileType))
            {
                errors[key + ".FileType"] = [$"Only {string.Join(", ", AttachmentTypes)} files can be attached"];
            }

            if (attachment.FileSize > MaxAttachmentBytes)
            {
                errors[key + ".FileSize"] = ["Each file can be at most 10 MB"];
            }

            index++;
        }
    }

    private static void Option(Dictionary<string, string[]> errors, string field, string? value, OptionList options, bool saveAsDraft)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (!saveAsDraft)
            {
                errors[field] = [$"{field.Split('.').Last()} is required"];
            }
        }
        else if (!options.IsValid(value))
        {
            errors[field] = [$"{field.Split('.').Last()} must be one of: {string.Join(", ", options.All)}"];
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
