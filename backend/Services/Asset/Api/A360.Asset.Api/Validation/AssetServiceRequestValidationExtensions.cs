using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Validation;

/// <summary>
/// Rules for the New Service Request form and the tabs, checked on the values as they will be saved.
/// A draft skips the fields marked * on screen, except the asset and title; values that are sent are always checked.
/// Whether the asset, work order and related request exist is checked by the endpoints.
/// </summary>
public static class AssetServiceRequestValidationExtensions
{
    public const int MaxAttachments = 50;
    public const int MaxUpdateAttachments = 10;
    public const long MaxAttachmentBytes = 50 * 1024 * 1024;
    public const int MaxTags = 20;
    public const int MaxLinks = 100;

    private const int TitleMaxLength = 200;
    private const int TagMaxLength = 50;
    private const int ShortTextMaxLength = 100;
    private const int DescriptionMaxLength = 1000;
    private const int MessageMaxLength = 2000;
    private const decimal MaxDowntimeHours = 10_000m;

    public static Dictionary<string, string[]> Validate(this AssetServiceRequest request, bool saveAsDraft)
    {
        var errors = new Dictionary<string, string[]>();

        Required(errors, "AssetRegistrationId", request.Asset.AssetRegistrationId);
        Required(errors, "Title", request.Title);
        MaxLength(errors, "Title", request.Title, TitleMaxLength);

        Option(errors, "RequestType", request.RequestType, AssetServiceRequestOptions.RequestTypes, saveAsDraft);
        Option(errors, "Priority", request.Priority, AssetServiceRequestOptions.Priorities, saveAsDraft);

        if (!saveAsDraft)
        {
            Required(errors, "Description", request.Description);
            if (request.RequestedBy is null)
            {
                errors["RequestedBy"] = ["RequestedBy is required"];
            }
        }

        MaxLength(errors, "Description", request.Description, DescriptionMaxLength);

        if (request.TargetDate?.Date < request.RequestedDate?.Date)
        {
            errors["TargetDate"] = ["TargetDate cannot be before RequestedDate"];
        }

        if (request.EstimatedDowntimeHours is < 0 or > MaxDowntimeHours)
        {
            errors["EstimatedDowntimeHours"] = [$"EstimatedDowntimeHours must be between 0 and {MaxDowntimeHours}"];
        }

        if (request.PlannedEnd <= request.PlannedStart)
        {
            errors["PlannedEnd"] = ["Required End must be after Required Start"];
        }

        if (request.ActualEnd < request.ActualStart)
        {
            errors["ActualEnd"] = ["ActualEnd cannot be before ActualStart"];
        }

        MaxLength(errors, "Location.Name", request.Location?.Name, TitleMaxLength);
        MaxLength(errors, "Department.Name", request.Department?.Name, TitleMaxLength);
        MaxLength(errors, "RequestedBy.Name", request.RequestedBy?.Name, TitleMaxLength);
        MaxLength(errors, "AssignedTo.Name", request.AssignedTo?.Name, TitleMaxLength);
        MaxLength(errors, "ReferenceNumber", request.ReferenceNumber, ShortTextMaxLength);

        if (request.Tags.Count > MaxTags)
        {
            errors["Tags"] = [$"At most {MaxTags} tags can be added"];
        }
        else if (request.Tags.FirstOrDefault(tag => tag.Length > TagMaxLength) is { } longTag)
        {
            errors["Tags"] = [$"Tag '{longTag}' cannot be longer than {TagMaxLength} characters"];
        }

        ValidateAttachments(errors, "Attachments", request.Attachments, MaxAttachments);
        return errors;
    }

    /// <summary>An update: a message or a new status (or both), and its attachments.</summary>
    public static Dictionary<string, string[]> Validate(this AddAssetServiceRequestUpdateRequest update, IReadOnlyCollection<AssetServiceRequestAttachment> attachments)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(update.Message) && string.IsNullOrWhiteSpace(update.Status))
        {
            errors["Message"] = ["Enter a comment or choose a new status"];
        }

        if (!string.IsNullOrWhiteSpace(update.Status) && !AssetServiceRequestOptions.UpdateStatuses.IsValid(update.Status))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AssetServiceRequestOptions.UpdateStatuses.All)}"];
        }

        MaxLength(errors, "Message", update.Message, MessageMaxLength);
        MaxLength(errors, "RecommendedAction", update.RecommendedAction, DescriptionMaxLength);
        ValidateAttachments(errors, "Attachments", attachments, MaxUpdateAttachments);
        return errors;
    }

    public static void ValidateAttachments(Dictionary<string, string[]> errors, string field, IReadOnlyCollection<AssetServiceRequestAttachment> attachments, int maxCount)
    {
        if (attachments.Count > maxCount)
        {
            errors[field] = [$"At most {maxCount} attachments can be added"];
            return;
        }

        var index = 0;
        foreach (var attachment in attachments)
        {
            var key = $"{field}[{index}]";
            Required(errors, key + ".FileName", attachment.FileName);
            Required(errors, key + ".FileUrl", attachment.FileUrl);
            MaxLength(errors, key + ".Description", attachment.Description, TitleMaxLength);

            if (!string.IsNullOrWhiteSpace(attachment.FileName) && attachment.FileType is null)
            {
                errors[key + ".FileName"] = [$"Only {string.Join(", ", AssetServiceRequestOptions.FileExtensions)} files can be attached"];
            }

            if (attachment.FileSize > MaxAttachmentBytes)
            {
                errors[key + ".FileSize"] = ["Each file can be at most 50 MB"];
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
                errors[field] = [$"{field} is required"];
            }
        }
        else if (!options.IsValid(value))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", options.All)}"];
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
