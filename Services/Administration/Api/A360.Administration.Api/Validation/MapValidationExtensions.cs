using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class MapValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateFloorPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.LocationId), request.LocationId);
        Require(errors, nameof(request.LocationType), request.LocationType);
        Require(errors, nameof(request.Name), request.Name);
        Require(errors, nameof(request.Type), request.Type);
        Require(errors, nameof(request.Format), request.Format);
        Require(errors, nameof(request.FileName), request.FileName);

        AllowOnly(errors, nameof(request.LocationType), request.LocationType, MapOptions.LocationTypes);
        AllowOnly(errors, nameof(request.Type), request.Type, MapOptions.FloorPlanTypes);
        AllowOnly(errors, nameof(request.Format), request.Format, MapOptions.FileFormats);
        AllowOnly(errors, nameof(request.Status), request.Status, MapOptions.Statuses);

        AtLeast(errors, nameof(request.SizeBytes), request.SizeBytes, 0);
        AtLeast(errors, nameof(request.WidthMeters), request.WidthMeters, 0);
        AtLeast(errors, nameof(request.HeightMeters), request.HeightMeters, 0);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateFloorPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.Name), request.Name);
        Require(errors, nameof(request.Type), request.Type);

        AllowOnly(errors, nameof(request.LocationType), request.LocationType, MapOptions.LocationTypes);
        AllowOnly(errors, nameof(request.Type), request.Type, MapOptions.FloorPlanTypes);

        AtLeast(errors, nameof(request.WidthMeters), request.WidthMeters, 0);
        AtLeast(errors, nameof(request.HeightMeters), request.HeightMeters, 0);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateVersionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.FileName), request.FileName);
        Require(errors, nameof(request.Format), request.Format);

        AllowOnly(errors, nameof(request.Format), request.Format, MapOptions.FileFormats);
        AllowOnly(errors, nameof(request.Bump), request.Bump, MapOptions.VersionBumps);

        AtLeast(errors, nameof(request.SizeBytes), request.SizeBytes, 0);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SaveZoneRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.Code), request.Code);
        Require(errors, nameof(request.Name), request.Name);
        Require(errors, nameof(request.Type), request.Type);
        Require(errors, nameof(request.Shape), request.Shape);

        AllowOnly(errors, nameof(request.Type), request.Type, MapOptions.ZoneTypes);
        AllowOnly(errors, nameof(request.Shape), request.Shape, MapOptions.ZoneShapes);
        AllowOnly(errors, nameof(request.AccessRule), request.AccessRule, MapOptions.AccessRules);

        // Bounding box is always sent, as percent of the plan
        Percent(errors, nameof(request.X), request.X, required: true);
        Percent(errors, nameof(request.Y), request.Y, required: true);
        Percent(errors, nameof(request.W), request.W, required: true);
        Percent(errors, nameof(request.H), request.H, required: true);
        Percent(errors, nameof(request.Radius), request.Radius, required: false);

        if (request.Points is not null
            && request.Points.Any(point => point is null || point.X is < 0 or > 100 || point.Y is < 0 or > 100))
        {
            errors[nameof(request.Points)] = ["Points must have x and y between 0 and 100"];
        }

        AtLeast(errors, nameof(request.AreaM2), request.AreaM2, 0);
        AtLeast(errors, nameof(request.DwellMinutes), request.DwellMinutes, 0);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SavePlotRequest request, string? kind)
    {
        var errors = ValidateKind(kind, required: true);

        Percent(errors, nameof(request.X), request.X, required: true);
        Percent(errors, nameof(request.Y), request.Y, required: true);

        return errors;
    }

    /// <summary>Plot kind from the route or query string: asset or device.</summary>
    public static Dictionary<string, string[]> ValidateKind(string? kind, bool required)
    {
        var errors = new Dictionary<string, string[]>();

        if (required)
        {
            Require(errors, "Kind", kind);
        }

        AllowOnly(errors, "Kind", kind, MapOptions.PlotKinds);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SaveLayerRequest request, string? layerKey)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "LayerKey", layerKey);
        AllowOnly(errors, "LayerKey", layerKey, MapOptions.Layers.Select(layer => layer.Key).ToArray());
        Percent(errors, nameof(request.Opacity), request.Opacity, required: false);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateLayerSetRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.Name), request.Name);

        var layerKeys = MapOptions.Layers.Select(layer => layer.Key).ToArray();
        var unknown = (request.Layers ?? [])
            .Keys
            .Where(key => !layerKeys.Contains(key?.Trim(), StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (unknown.Count > 0)
        {
            errors[nameof(request.Layers)] = [$"Unknown layer key(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", layerKeys)}"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this SaveMapSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Between(errors, nameof(request.DefaultZoom), request.DefaultZoom, 1, 1000);
        AtLeast(errors, nameof(request.MaxFileMb), request.MaxFileMb, 1);
        AtLeast(errors, nameof(request.KeepVersions), request.KeepVersions, 1);
        if (request.GridSize is <= 0)
        {
            errors[nameof(request.GridSize)] = ["GridSize must be above zero"];
        }
        AtLeast(errors, nameof(request.DwellMinutes), request.DwellMinutes, 0);
        AtLeast(errors, nameof(request.ArchiveAfter), request.ArchiveAfter, 0);
        AtLeast(errors, nameof(request.CacheHours), request.CacheHours, 0);
        AtLeast(errors, nameof(request.CalibrationPoints), request.CalibrationPoints, 0);
        AtLeast(errors, nameof(request.CalibrationTolerance), request.CalibrationTolerance, 0);

        AllowOnly(errors, nameof(request.ZoneType), request.ZoneType, MapOptions.ZoneTypes);

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }

    private static void AllowOnly(Dictionary<string, string[]> errors, string field, string? value, string[] allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    /// <summary>Position or size as percent of the plan: 0-100.</summary>
    private static void Percent(Dictionary<string, string[]> errors, string field, double? value, bool required)
    {
        if (value is null)
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
            return;
        }

        Between(errors, field, value, 0, 100);
    }

    private static void Between(Dictionary<string, string[]> errors, string field, double? value, double min, double max)
    {
        if (value is not null && (double.IsNaN(value.Value) || value < min || value > max))
        {
            errors[field] = [$"{field} must be between {min} and {max}"];
        }
    }

    private static void AtLeast(Dictionary<string, string[]> errors, string field, double? value, double min)
    {
        if (value is not null && (double.IsNaN(value.Value) || value < min))
        {
            errors[field] = [$"{field} must be {min} or more"];
        }
    }
}
