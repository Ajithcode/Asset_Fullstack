using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetModelValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetModelRequest request)
    {
        var errors = ValidateFields(request.ModelName, request.ModelCode, request.Manufacturer, request.AssetType, request.Category);
        ValidateDetails(errors, request.Specs, request.LifecycleYears, request.WarrantyMonths, request.PurchasePrice, request.SalvagePercent);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetModelRequest request)
    {
        var errors = ValidateFields(request.ModelName, request.ModelCode, request.Manufacturer, request.AssetType, request.Category);
        ValidateDetails(errors, request.Specs, request.LifecycleYears, request.WarrantyMonths, request.PurchasePrice, request.SalvagePercent);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetModelStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? modelName,
        string? modelCode,
        MasterDataReferenceDto? manufacturer,
        MasterDataReferenceDto? assetType,
        MasterDataReferenceDto? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(modelName))
        {
            errors["ModelName"] =
                ["ModelName is required"];
        }

        if (string.IsNullOrWhiteSpace(modelCode))
        {
            errors["ModelCode"] =
                ["ModelCode is required"];
        }

        // Manufacturer, asset type and category are records in this service, so they are picked by id.
        if (manufacturer is not null && string.IsNullOrWhiteSpace(manufacturer.Id))
        {
            errors["Manufacturer"] =
                ["Manufacturer must have a manufacturer id"];
        }

        if (assetType is not null && string.IsNullOrWhiteSpace(assetType.Id))
        {
            errors["AssetType"] =
                ["AssetType must have an asset type id"];
        }

        if (category is not null && string.IsNullOrWhiteSpace(category.Id))
        {
            errors["Category"] =
                ["Category must have a category id"];
        }

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        List<AssetModelSpecDto>? specs,
        int? lifecycleYears,
        int? warrantyMonths,
        decimal? purchasePrice,
        decimal? salvagePercent)
    {
        if (specs is not null && specs.Any(x => x is not null && string.IsNullOrWhiteSpace(x.Label) && !string.IsNullOrWhiteSpace(x.Value)))
        {
            errors["Specs"] =
                ["Every specification value needs a name"];
        }

        if (lifecycleYears is < 1 or > 50)
        {
            errors["LifecycleYears"] =
                ["LifecycleYears must be between 1 and 50"];
        }

        if (warrantyMonths is < 0 or > 240)
        {
            errors["WarrantyMonths"] =
                ["WarrantyMonths must be between 0 and 240"];
        }

        if (purchasePrice < 0)
        {
            errors["PurchasePrice"] =
                ["PurchasePrice cannot be negative"];
        }

        if (salvagePercent is < 0 or > 100)
        {
            errors["SalvagePercent"] =
                ["SalvagePercent must be between 0 and 100"];
        }
    }
}
