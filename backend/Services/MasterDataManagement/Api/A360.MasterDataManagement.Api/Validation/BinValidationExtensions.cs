using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class BinValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateBinRequest request)
    {
        return ValidateFields(request.BinName, request.Code, request.Store, request.Capacity);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBinRequest request)
    {
        return ValidateFields(request.BinName, request.Code, request.Store, request.Capacity);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBinStatusRequest request)
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
        string? binName,
        string? code,
        MasterDataReferenceDto? store,
        double? capacity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(binName))
        {
            errors["BinName"] =
                ["BinName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(store?.Id))
        {
            errors["Store"] =
                ["Store is required: pick a store by id"];
        }

        if (capacity <= 0)
        {
            errors["Capacity"] =
                ["Capacity must be greater than 0"];
        }

        return errors;
    }
}
