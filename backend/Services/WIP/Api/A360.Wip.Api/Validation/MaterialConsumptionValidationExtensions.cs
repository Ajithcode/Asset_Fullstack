
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class MaterialConsumptionValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateMaterialConsumptionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.MaterialId))
        {
            errors["MaterialId"] =
                ["MaterialId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ItemName))
        {
            errors["ItemName"] =
                ["ItemName is required"];
        }

        if (request.QuantityPlanned is < 0)
        {
            errors["QuantityPlanned"] =
                ["QuantityPlanned must not be negative"];
        }

        if (request.QuantityUsed is < 0)
        {
            errors["QuantityUsed"] =
                ["QuantityUsed must not be negative"];
        }

        if (request.Cost is < 0)
        {
            errors["Cost"] =
                ["Cost must not be negative"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateMaterialConsumptionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ItemName))
        {
            errors["ItemName"] =
                ["ItemName is required"];
        }

        if (request.QuantityPlanned is < 0)
        {
            errors["QuantityPlanned"] =
                ["QuantityPlanned must not be negative"];
        }

        if (request.QuantityUsed is < 0)
        {
            errors["QuantityUsed"] =
                ["QuantityUsed must not be negative"];
        }

        if (request.Cost is < 0)
        {
            errors["Cost"] =
                ["Cost must not be negative"];
        }

        return errors;
    }
}
