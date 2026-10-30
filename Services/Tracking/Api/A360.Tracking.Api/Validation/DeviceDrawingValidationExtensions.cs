using A360.Tracking.Api.Contracts;

namespace A360.Tracking.Api.Validation;

public static class DeviceDrawingValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateDeviceDrawingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ZoneDrawingValidationExtensions.RequireObjectId(errors, "ProjectId", request.ProjectId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "CountryId", request.CountryId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "AreaId", request.AreaId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "BuildingId", request.BuildingId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "FloorId", request.FloorId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "ZoneId", request.ZoneId);
        ZoneDrawingValidationExtensions.RequireObjectId(errors, "DeviceReferenceId", request.DeviceReferenceId);

        if (string.IsNullOrWhiteSpace(request.DeviceUniqueId))
        {
            errors["DeviceUniqueId"] = ["DeviceUniqueId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            errors["DeviceName"] = ["DeviceName is required"];
        }

        ZoneDrawingValidationExtensions.ValidateGeoJson(errors, "DeviceGeoJsonData", request.DeviceGeoJsonData);

        return errors;
    }
}
