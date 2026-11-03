using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetDocumentValidationExtensions
{
    private const int NameMaxLength = 200;
    private const int CodeMaxLength = 100;
    private const int VersionMaxLength = 20;
    private const int DescriptionMaxLength = 1000;
    private const int MaxAssets = 100;
    private const int MaxTags = 20;
    private const int TagMaxLength = 50;

    public static Dictionary<string, string[]> Validate(this CreateAssetDocumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var assetIds = request.AssetRegistrationIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        if (assetIds.Count == 0)
        {
            errors["AssetRegistrationIds"] = ["Select at least one asset"];
        }
        else if (assetIds.Count > MaxAssets)
        {
            errors["AssetRegistrationIds"] = [$"A document can be added to at most {MaxAssets} assets at once"];
        }

        ValidateDetails(errors, request.DocumentName, request.DocumentType, request.LifecycleStage, request.Version, request.Description, request.Tags, request.Status);
        ValidateFile(errors, request.FileName, request.FileUrl, request.FileSize, required: true);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetDocumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId))
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required"];
        }

        ValidateDetails(errors, request.DocumentName, request.DocumentType, request.LifecycleStage, request.Version, request.Description, request.Tags, request.Status);
        ValidateFile(errors, request.FileName, request.FileUrl, request.FileSize, required: false);

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? documentName,
        string? documentType,
        string? lifecycleStage,
        string? version,
        string? description,
        IReadOnlyCollection<string>? tags,
        string? status)
    {
        RequireText(errors, "DocumentName", documentName, NameMaxLength);
        RequireText(errors, "DocumentType", documentType, CodeMaxLength);
        RequireText(errors, "LifecycleStage", lifecycleStage, CodeMaxLength);
        RequireText(errors, "Version", version, VersionMaxLength);

        if (description?.Trim().Length > DescriptionMaxLength)
        {
            errors["Description"] = [$"Description cannot be longer than {DescriptionMaxLength} characters"];
        }

        var cleanTags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList() ?? [];
        if (cleanTags.Count > MaxTags)
        {
            errors["Tags"] = [$"A document can have at most {MaxTags} tags"];
        }
        else if (cleanTags.Any(tag => tag.Trim().Length > TagMaxLength))
        {
            errors["Tags"] = [$"Each tag cannot be longer than {TagMaxLength} characters"];
        }

        if (!string.IsNullOrWhiteSpace(status)
            && !AssetDocumentStatuses.All.Contains(status.Trim(), StringComparer.Ordinal))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AssetDocumentStatuses.All)}"];
        }
    }

    /// <param name="required">False on update, where an empty FileUrl keeps the current file.</param>
    private static void ValidateFile(Dictionary<string, string[]> errors, string? fileName, string? fileUrl, long? fileSize, bool required)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            if (required)
            {
                errors["FileUrl"] = ["Upload a file first"];
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            errors["FileName"] = ["FileName is required"];
        }
        else if (!AssetDocumentFiles.AllowedExtensions.Contains(Path.GetExtension(fileName.Trim()).ToLowerInvariant()))
        {
            errors["FileName"] = ["Only PDF, JPG, PNG, DOC and XLS files are allowed"];
        }

        if (fileSize is < 0 or > AssetDocumentFiles.MaxFileSizeBytes)
        {
            errors["FileSize"] = ["File cannot be larger than 25 MB"];
        }
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
