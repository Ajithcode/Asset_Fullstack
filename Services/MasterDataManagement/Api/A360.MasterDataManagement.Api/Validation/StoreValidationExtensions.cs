using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class StoreValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateStoreRequest request)
    {
        return ValidateFields(request.StoreName, request.Code, request.Warehouse);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStoreRequest request)
    {
        return ValidateFields(request.StoreName, request.Code, request.Warehouse);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStoreStatusRequest request)
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
        string? storeName,
        string? code,
        MasterDataReferenceDto? warehouse)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(storeName))
        {
            errors["StoreName"] =
                ["StoreName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(warehouse?.Id))
        {
            errors["Warehouse"] =
                ["Warehouse is required: pick a warehouse by id"];
        }

        return errors;
    }
}
