
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class AssetLinkingValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetLinkingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetLinkingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CurrentStatus))
        {
            errors["CurrentStatus"] =
                ["CurrentStatus is required"];
        }

        return errors;
    }
}
