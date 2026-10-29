using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ZoneValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateZoneRequest request)
    {
        return ValidateFields(
            request.Name, request.Code, request.Site, request.BuildingId, request.FloorId, request.Capacity, request.Area);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateZoneRequest request)
    {
        return ValidateFields(
            request.Name, request.Code, request.Site, request.BuildingId, request.FloorId, request.Capacity, request.Area);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateZoneStatusRequest request)
    {
        return SpaceValidation.ValidateStatus(request.Status);
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? name,
        string? code,
        MasterDataReferenceDto? site,
        string? buildingId,
        string? floorId,
        int? capacity,
        double? area)
    {
        var errors = SpaceValidation.ValidateCommonFields(name, code, site, capacity, area);

        // The building can be worked out from the floor, so one of the two is enough.
        if (string.IsNullOrWhiteSpace(buildingId) && string.IsNullOrWhiteSpace(floorId))
        {
            errors["BuildingId"] =
                ["BuildingId or FloorId is required"];
        }

        return errors;
    }
}
