using A360.Asset.Api.Contracts;

namespace A360.Asset.Api.Validation;

public static class AssetImageValidationExtensions
{
    private const int NameMaxLength = 200;
    private const int CodeMaxLength = 100;
    private const int LocationMaxLength = 300;
    private const int RemarksMaxLength = 500;
    private const int DescriptionMaxLength = 1000;
    private const int MaxAssets = 100;
    private const int MaxTags = 20;
    private const int TagMaxLength = 50;

    public static Dictionary<string, string[]> Validate(this CreateAssetImagesRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var assetIds = request.AssetRegistrationIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        if (assetIds.Count == 0)
        {
            errors["AssetRegistrationIds"] = ["Select at least one asset"];
        }
        else if (assetIds.Count > MaxAssets)
        {
            errors["AssetRegistrationIds"] = [$"Images can be added to at most {MaxAssets} assets at once"];
        }

        var images = request.Images ?? [];
        if (images.Count == 0)
        {
            errors["Images"] = ["Upload at least one image"];
        }
        else if (images.Count > AssetImageFiles.MaxImagesPerUpload)
        {
            errors["Images"] = [$"At most {AssetImageFiles.MaxImagesPerUpload} images can be uploaded at once"];
        }
        else
        {
            var index = 0;
            foreach (var image in images)
            {
                ValidateFile(errors, $"Images[{index}]", image);
                if (image.ImageName?.Trim().Length > NameMaxLength)
                {
                    errors[$"Images[{index}].ImageName"] = [$"ImageName cannot be longer than {NameMaxLength} characters"];
                }

                index++;
            }
        }

        ValidateDetails(errors, request.ImageType, request.Tags, request.Location, request.Remarks, request.Description);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateAssetImageRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId))
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required"];
        }

        RequireText(errors, "ImageName", request.ImageName, NameMaxLength);

        if (request.File is not null && !string.IsNullOrWhiteSpace(request.File.FileUrl))
        {
            ValidateFile(errors, "File", request.File);
        }

        ValidateDetails(errors, request.ImageType, request.Tags, request.Location, request.Remarks, request.Description);

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? imageType,
        IReadOnlyCollection<string>? tags,
        string? location,
        string? remarks,
        string? description)
    {
        RequireText(errors, "ImageType", imageType, CodeMaxLength);
        LimitText(errors, "Location", location, LocationMaxLength);
        LimitText(errors, "Remarks", remarks, RemarksMaxLength);
        LimitText(errors, "Description", description, DescriptionMaxLength);

        var cleanTags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList() ?? [];
        if (cleanTags.Count > MaxTags)
        {
            errors["Tags"] = [$"An image can have at most {MaxTags} tags"];
        }
        else if (cleanTags.Any(tag => tag.Trim().Length > TagMaxLength))
        {
            errors["Tags"] = [$"Each tag cannot be longer than {TagMaxLength} characters"];
        }
    }

    private static void ValidateFile(Dictionary<string, string[]> errors, string prefix, AssetImageFileRequest file)
    {
        if (string.IsNullOrWhiteSpace(file.FileUrl))
        {
            errors[$"{prefix}.FileUrl"] = ["Upload the image first"];
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            errors[$"{prefix}.FileName"] = ["FileName is required"];
        }
        else if (!AssetImageFiles.AllowedExtensions.Contains(Path.GetExtension(file.FileName.Trim()).ToLowerInvariant()))
        {
            errors[$"{prefix}.FileName"] = ["Only JPG, PNG, WEBP, GIF and BMP images are allowed"];
        }

        if (file.FileSize is < 0 or > AssetImageFiles.MaxFileSizeBytes)
        {
            errors[$"{prefix}.FileSize"] = ["Image cannot be larger than 20 MB"];
        }

        if (file.Width is < 1 || file.Height is < 1)
        {
            errors[$"{prefix}.Resolution"] = ["Width and Height must be greater than 0"];
        }
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else
        {
            LimitText(errors, field, value, maxLength);
        }
    }

    private static void LimitText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
