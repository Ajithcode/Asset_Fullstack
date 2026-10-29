using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

public static class LocationValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateLocationRequest request)
    {
        return ValidateFields(request.LocationName, request.Code, request.Type, request.ParentLocationId);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateLocationRequest request)
    {
        return ValidateFields(request.LocationName, request.Code, request.Type, request.ParentLocationId);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateLocationStatusRequest request)
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
        string? locationName,
        string? code,
        string? type,
        string? parentLocationId)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(locationName))
        {
            errors["LocationName"] =
                ["LocationName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            errors["Type"] =
                ["Type is required"];
        }

        if (!string.IsNullOrWhiteSpace(parentLocationId) && !MongoObjectId.IsValid(parentLocationId))
        {
            errors["ParentLocationId"] =
                ["ParentLocationId is not a valid id"];
        }

        return errors;
    }
}
