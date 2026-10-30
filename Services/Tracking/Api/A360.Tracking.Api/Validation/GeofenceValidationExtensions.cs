using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class GeofenceValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int MaxAssetGroups = 30;
    private const int MaxDwellAlertMinutes = 24 * 60;

    public static Dictionary<string, string[]> Validate(this CreateGeofenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "Name", request.Name, NameMaxLength);
        RequireText(errors, "SiteName", request.SiteName, NameMaxLength);

        if (string.IsNullOrWhiteSpace(request.GeofenceType))
        {
            errors["GeofenceType"] = ["GeofenceType is required"];
        }
        else if (ContractText.Canonical(request.GeofenceType, GeofenceType.All) is null)
        {
            errors["GeofenceType"] = [$"GeofenceType must be one of: {string.Join(", ", GeofenceType.All)}"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && ContractText.Canonical(request.Status, GeofenceStatus.All) is null)
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", GeofenceStatus.All)}"];
        }

        if (request.MonitoredAssetCount < 0)
        {
            errors["MonitoredAssetCount"] = ["MonitoredAssetCount cannot be negative"];
        }

        if (request.RuleCount < 0)
        {
            errors["RuleCount"] = ["RuleCount cannot be negative"];
        }

        // Allows a little clock drift, but not events from the future.
        if (request.LastEventAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["LastEventAt"] = ["LastEventAt cannot be in the future"];
        }

        if (!string.IsNullOrWhiteSpace(request.MapColour) && ContractText.Canonical(request.MapColour, GeofenceMapColour.All) is null)
        {
            errors["MapColour"] = [$"MapColour must be one of: {string.Join(", ", GeofenceMapColour.All)}"];
        }

        if (!string.IsNullOrWhiteSpace(request.Schedule) && ContractText.Canonical(request.Schedule, GeofenceSchedule.All) is null)
        {
            errors["Schedule"] = [$"Schedule must be one of: {string.Join(", ", GeofenceSchedule.All)}"];
        }

        var assetGroups = request.AssetGroups?.Where(group => !string.IsNullOrWhiteSpace(group)).ToList() ?? [];
        if (assetGroups.Count == 0)
        {
            errors["AssetGroups"] = ["Select at least one asset group"];
        }
        else if (assetGroups.Count > MaxAssetGroups)
        {
            errors["AssetGroups"] = [$"At most {MaxAssetGroups} asset groups are allowed"];
        }
        else if (assetGroups.Any(group => group.Trim().Length > NameMaxLength))
        {
            errors["AssetGroups"] = [$"Each asset group cannot be longer than {NameMaxLength} characters"];
        }

        if (request.DwellAlertMinutes is < 1 or > MaxDwellAlertMinutes)
        {
            errors["DwellAlertMinutes"] = [$"DwellAlertMinutes must be between 1 and {MaxDwellAlertMinutes}"];
        }

        ValidateShape(errors, request);

        return errors;
    }

    private static void ValidateShape(Dictionary<string, string[]> errors, CreateGeofenceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Shape) && ContractText.Canonical(request.Shape, GeofenceShape.All) is null)
        {
            errors["Shape"] = [$"Shape must be one of: {string.Join(", ", GeofenceShape.All)}"];
            return;
        }

        var shape = ContractText.Canonical(request.Shape, GeofenceShape.All) ?? GeofenceShape.Polygon;
        var coordinates = request.Coordinates ?? [];

        if (coordinates.Any(point => point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180))
        {
            errors["Coordinates"] = ["Latitude must be between -90 and 90 and longitude between -180 and 180"];
            return;
        }

        switch (shape)
        {
            case GeofenceShape.Polygon:
                if (request.PolygonPoints is < GeofenceShape.MinPolygonPoints or > GeofenceShape.MaxPolygonPoints)
                {
                    errors["PolygonPoints"] = [$"PolygonPoints must be between {GeofenceShape.MinPolygonPoints} and {GeofenceShape.MaxPolygonPoints}"];
                }
                else if (coordinates.Count > 0 && coordinates.Count is < GeofenceShape.MinPolygonPoints or > GeofenceShape.MaxPolygonPoints)
                {
                    errors["Coordinates"] = [$"A polygon needs between {GeofenceShape.MinPolygonPoints} and {GeofenceShape.MaxPolygonPoints} points"];
                }
                else if (coordinates.Count > 0 && request.PolygonPoints is not null && request.PolygonPoints != coordinates.Count)
                {
                    errors["PolygonPoints"] = [$"PolygonPoints is {request.PolygonPoints} but {coordinates.Count} coordinates were drawn"];
                }
                break;

            case GeofenceShape.Rectangle:
                if (coordinates.Count is not (0 or 4))
                {
                    errors["Coordinates"] = ["A rectangle needs exactly 4 corner points"];
                }
                break;

            case GeofenceShape.Circle:
                if (coordinates.Count > 1)
                {
                    errors["Coordinates"] = ["A circle takes only its centre point"];
                }

                if (request.RadiusMeters is <= 0)
                {
                    errors["RadiusMeters"] = ["RadiusMeters must be greater than 0"];
                }
                else if (coordinates.Count == 1 && request.RadiusMeters is null)
                {
                    errors["RadiusMeters"] = ["RadiusMeters is required when the circle centre is set"];
                }
                break;
        }
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
