using System.Text.RegularExpressions;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static partial class TrackingTagValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this CreateTrackingTagRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "TagId", request.TagId, CodeMaxLength);
        if (!errors.ContainsKey("TagId") && !TagIdPattern().IsMatch(request.TagId!.Trim()))
        {
            errors["TagId"] = ["TagId can only contain letters, digits, '-' and '_'"];
        }

        RequireText(errors, "TagType", request.TagType, CodeMaxLength);
        RequireOneOf(errors, "Technology", request.Technology, TagTechnology.All, required: true);
        RequireOneOf(errors, "Status", request.Status, TagStatus.All, required: false);

        MaxLength(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        MaxLength(errors, "AssetName", request.AssetName, NameMaxLength);
        MaxLength(errors, "SiteName", request.SiteName, NameMaxLength);
        MaxLength(errors, "LocationName", request.LocationName, NameMaxLength);

        var hasAsset = !string.IsNullOrWhiteSpace(request.AssetRegistrationId);
        if (string.Equals(request.Status?.Trim(), TagStatus.Assigned, StringComparison.OrdinalIgnoreCase) && !hasAsset)
        {
            errors["AssetRegistrationId"] = ["AssetRegistrationId is required when Status is Assigned"];
        }
        else if (hasAsset && !string.IsNullOrWhiteSpace(request.Status)
            && !string.Equals(request.Status.Trim(), TagStatus.Assigned, StringComparison.OrdinalIgnoreCase))
        {
            errors["Status"] = ["Status must be Assigned when a linked asset is given"];
        }

        // Allows a little reader clock drift, but not readings from the future.
        if (request.LastSeenAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["LastSeenAt"] = ["LastSeenAt cannot be in the future"];
        }

        return errors;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else
        {
            MaxLength(errors, field, value, maxLength);
        }
    }

    private static void RequireOneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
        }
        else if (ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex TagIdPattern();
}
