
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class StatusMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateStatusMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.StatusId))
        {
            errors["StatusId"] =
                ["StatusId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.StatusName))
        {
            errors["StatusName"] =
                ["StatusName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.StatusCode))
        {
            errors["StatusCode"] =
                ["StatusCode is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStatusMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.StatusName))
        {
            errors["StatusName"] =
                ["StatusName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.StatusCode))
        {
            errors["StatusCode"] =
                ["StatusCode is required"];
        }

        return errors;
    }
}
