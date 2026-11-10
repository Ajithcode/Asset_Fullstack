namespace A360.MasterDataManagement.Api.Validation;

/// <summary>Map position checks shared by sites, buildings and floors.</summary>
public static class CoordinateValidation
{
    /// <summary>Adds latitude / longitude errors to <paramref name="errors"/> and returns it.</summary>
    public static Dictionary<string, string[]> With(Dictionary<string, string[]> errors, double? latitude, double? longitude)
    {
        if (latitude is not null && (double.IsNaN(latitude.Value) || latitude < -90 || latitude > 90))
        {
            errors["Latitude"] = ["Latitude must be between -90 and 90"];
        }
        else if (latitude is null && longitude is not null)
        {
            errors["Latitude"] = ["Enter the latitude too"];
        }

        if (longitude is not null && (double.IsNaN(longitude.Value) || longitude < -180 || longitude > 180))
        {
            errors["Longitude"] = ["Longitude must be between -180 and 180"];
        }
        else if (longitude is null && latitude is not null)
        {
            errors["Longitude"] = ["Enter the longitude too"];
        }

        return errors;
    }
}
