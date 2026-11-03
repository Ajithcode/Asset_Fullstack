
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class SparePartValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSparePartRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PartId))
        {
            errors["PartId"] =
                ["PartId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.PartName))
        {
            errors["PartName"] =
                ["PartName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSparePartRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PartId))
        {
            errors["PartId"] =
                ["PartId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.PartName))
        {
            errors["PartName"] =
                ["PartName is required"];
        }

        return errors;
    }
}
