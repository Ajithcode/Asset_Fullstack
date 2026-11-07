using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class BuildingValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateBuildingRequest request)
    {
        return CoordinateValidation.With(
            SpaceValidation.ValidateCommonFields(request.Name, request.Code, request.Site, request.Capacity, request.Area),
            request.Latitude, request.Longitude);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBuildingRequest request)
    {
        return CoordinateValidation.With(
            SpaceValidation.ValidateCommonFields(request.Name, request.Code, request.Site, request.Capacity, request.Area),
            request.Latitude, request.Longitude);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBuildingStatusRequest request)
    {
        return SpaceValidation.ValidateStatus(request.Status);
    }
}
