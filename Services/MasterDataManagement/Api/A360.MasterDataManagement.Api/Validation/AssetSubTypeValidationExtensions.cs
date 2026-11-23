using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetSubTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateAssetSubTypeRequest request) =>
        ValidateFields(request.SubTypeName, request.Code, request.AssetType);

    public static Dictionary<string, string[]> Validate(this UpdateAssetSubTypeRequest request) =>
        ValidateFields(request.SubTypeName, request.Code, request.AssetType);

    public static Dictionary<string, string[]> Validate(this UpdateAssetSubTypeStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] = ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? subTypeName,
        string? code,
        MasterDataReferenceDto? assetType)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(subTypeName))
        {
            errors["SubTypeName"] = ["SubTypeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] = ["Code is required"];
        }

        if (assetType is null || string.IsNullOrWhiteSpace(assetType.Id))
        {
            errors["AssetType"] = ["AssetType is required"];
        }

        return errors;
    }
}
