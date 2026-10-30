using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class BuildingValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateBuildingRequest request)
    {
        var errors = SpaceValidation.ValidateCommonFields(
            request.Name, request.Code, request.Site, request.Capacity, request.Area);

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBuildingRequest request)
    {
        var errors = SpaceValidation.ValidateCommonFields(
            request.Name, request.Code, request.Site, request.Capacity, request.Area);

        StructureValidation.ValidateCoordinates(errors, request.Latitude, request.Longitude);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateBuildingStatusRequest request)
    {
        return SpaceValidation.ValidateStatus(request.Status);
    }
}
