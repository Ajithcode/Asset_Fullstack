using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

public static class LocationValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateLocationRequest request)
    {
        var errors = ValidateFields(request.LocationName, request.Code, request.Type, request.ParentLocationId);

        // The site is a record in this service, so it is picked by id.
        if (request.Site is not null && string.IsNullOrWhiteSpace(request.Site.Id))
        {
            errors["Site"] =
                ["Site must have a site id"];
        }

        if (request.Area < 0)
        {
            errors["Area"] =
                ["Area cannot be negative"];
        }

        if (request.Capacity < 0)
        {
            errors["Capacity"] =
                ["Capacity cannot be negative"];
        }

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateLocationRequest request)
    {
        var errors = ValidateFields(request.LocationName, request.Code, request.Type, request.ParentLocationId);

        // The site is a record in this service, so it is picked by id.
        if (request.Site is not null && string.IsNullOrWhiteSpace(request.Site.Id))
        {
            errors["Site"] =
                ["Site must have a site id"];
        }

        if (request.Area < 0)
        {
            errors["Area"] =
                ["Area cannot be negative"];
        }

        if (request.Capacity < 0)
        {
            errors["Capacity"] =
                ["Capacity cannot be negative"];
        }

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
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
