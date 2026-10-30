using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class FloorValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateFloorRequest request)
    {
        var errors = ValidateFields(
            request.Name, request.Code, request.Site, request.BuildingId, request.Capacity, request.Area);

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateFloorRequest request)
    {
        var errors = ValidateFields(
            request.Name, request.Code, request.Site, request.BuildingId, request.Capacity, request.Area);

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateFloorStatusRequest request)
    {
        return SpaceValidation.ValidateStatus(request.Status);
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? name,
        string? code,
        MasterDataReferenceDto? site,
        string? buildingId,
        int? capacity,
        double? area)
    {
        var errors = SpaceValidation.ValidateCommonFields(name, code, site, capacity, area);

        if (string.IsNullOrWhiteSpace(buildingId))
        {
            errors["BuildingId"] =
                ["BuildingId is required"];
        }

        return errors;
    }
}
