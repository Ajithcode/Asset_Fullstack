using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class MasterDataItemValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateMasterDataItemRequest request)
    {
        return ValidateRequired(request.MasterType, request.Code, request.Name);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateMasterDataItemRequest request)
    {
        return ValidateRequired(request.MasterType, request.Code, request.Name);
    }

    private static Dictionary<string, string[]> ValidateRequired(
        string? masterType,
        string? code,
        string? name)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(masterType))
        {
            errors["MasterType"] =
                ["MasterType is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["Name"] =
                ["Name is required"];
        }

        return errors;
    }
}
