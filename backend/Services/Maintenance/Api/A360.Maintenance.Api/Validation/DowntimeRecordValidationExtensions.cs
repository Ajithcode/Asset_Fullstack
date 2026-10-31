
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class DowntimeRecordValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateDowntimeRecordRequest request)
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
        this UpdateDowntimeRecordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        return errors;
    }
}
