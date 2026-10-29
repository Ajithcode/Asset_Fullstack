using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class WarehouseValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateWarehouseRequest request)
    {
        return ValidateFields(request.WarehouseName, request.Code, request.Manager);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWarehouseRequest request)
    {
        return ValidateFields(request.WarehouseName, request.Code, request.Manager);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateWarehouseStatusRequest request)
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
        string? warehouseName,
        string? code,
        MasterDataReferenceDto? manager)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(warehouseName))
        {
            errors["WarehouseName"] =
                ["WarehouseName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (manager is not null && string.IsNullOrWhiteSpace(manager.Id))
        {
            errors["Manager"] =
                ["Manager must have an employee id"];
        }

        return errors;
    }
}
