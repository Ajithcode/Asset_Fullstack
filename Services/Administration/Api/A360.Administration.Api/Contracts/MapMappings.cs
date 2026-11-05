using System.Globalization;
using System.Text.Json;
using MongoDB.Bson;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class MapMappings
{
    // ---- Floor plans ------------------------------------------------------------------------------------------------

    /// <summary>New floor plan with its first file as version v1.0 (current). A plan created as Published publishes v1.0.</summary>
    public static FloorPlan ToEntity(this CreateFloorPlanRequest request, CurrentUser? currentUser)
    {
        var userName = FirstNonEmpty(request.CreatedBy, currentUser?.UserName);
        var now = DateTime.UtcNow;
        var status = Canonical(request.Status, MapOptions.Statuses) ?? MapOptions.Draft;

        var version = new FloorPlanVersion
        {
            Version = FormatVersion(1, 0),
            Status = status == MapOptions.Published ? MapOptions.Published : MapOptions.Draft,
            FileUrl = Optional(request.FileUrl),
            FileName = request.FileName!.Trim(),
            Format = Canonical(request.Format, MapOptions.FileFormats)!,
            SizeBytes = request.SizeBytes ?? 0,
            Description = Optional(request.VersionNote) ?? "Initial upload",
            Changes = [],
            UploadedBy = userName,
            UploadedAt = now
        };

        var plan = new FloorPlan
        {
            // Generated on the server so every floor plan gets a stable id independent of the Mongo _id.
            FloorPlanId = Guid.NewGuid().ToString(),
            LocationId = request.LocationId!.Trim(),
            LocationType = Canonical(request.LocationType, MapOptions.LocationTypes)!,
            LocationPath = Optional(request.LocationPath),
            Name = request.Name!.Trim(),
            Description = Optional(request.Description),
            PlanType = Canonical(request.Type, MapOptions.FloorPlanTypes)!,
            Format = version.Format,
            FileUrl = version.FileUrl,
            FileName = version.FileName,
            SizeBytes = version.SizeBytes,
            WidthMeters = request.WidthMeters,
            HeightMeters = request.HeightMeters,
            Scale = Optional(request.Scale),
            CurrentVersion = version.Version,
            Status = status,
            Versions = [version],
            CreatedBy = userName,
            CreatedAt = now,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };

        if (status == MapOptions.Published)
        {
            version.PublishedAt = plan.PublishedAt = now;
            version.PublishedBy = plan.PublishedBy = userName;
        }

        return plan;
    }

    /// <summary>Location fields left out keep their value; the others are replaced as sent.</summary>
    public static void ApplyTo(this UpdateFloorPlanRequest request, FloorPlan plan, CurrentUser? currentUser)
    {
        plan.Name = request.Name!.Trim();
        plan.Description = Optional(request.Description);
        plan.PlanType = Canonical(request.Type, MapOptions.FloorPlanTypes)!;
        plan.WidthMeters = request.WidthMeters;
        plan.HeightMeters = request.HeightMeters;
        plan.Scale = Optional(request.Scale);
        if (!string.IsNullOrWhiteSpace(request.LocationId))
        {
            plan.LocationId = request.LocationId.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.LocationType))
        {
            plan.LocationType = Canonical(request.LocationType, MapOptions.LocationTypes)!;
        }
        if (request.LocationPath is not null)
        {
            plan.LocationPath = Optional(request.LocationPath);
        }
        plan.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        plan.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Makes the version current and Published; the previously Published version is Archived and the plan's file,
    /// format, size, current version and status follow the published one.
    /// </summary>
    public static void Publish(this FloorPlan plan, FloorPlanVersion version, string? userName)
    {
        var now = DateTime.UtcNow;

        foreach (var other in plan.Versions.Where(other => !ReferenceEquals(other, version) && other.Status == MapOptions.Published))
        {
            other.Status = MapOptions.Archived;
        }

        version.Status = MapOptions.Published;
        version.PublishedAt = now;
        version.PublishedBy = userName;

        plan.CurrentVersion = version.Version;
        plan.FileUrl = version.FileUrl;
        plan.FileName = version.FileName;
        plan.Format = version.Format;
        plan.SizeBytes = version.SizeBytes;
        plan.Status = MapOptions.Published;
        plan.PublishedAt = now;
        plan.PublishedBy = userName;
        plan.UpdatedBy = userName;
        plan.UpdatedAt = now;
    }

    // ---- Versions ---------------------------------------------------------------------------------------------------

    public static FloorPlanVersion ToVersion(this CreateVersionRequest request, FloorPlan plan, CurrentUser? currentUser)
    {
        return new FloorPlanVersion
        {
            Version = NextVersion(plan, request.Bump),
            Status = MapOptions.Draft,
            FileUrl = Optional(request.FileUrl),
            FileName = request.FileName!.Trim(),
            Format = Canonical(request.Format, MapOptions.FileFormats)!,
            SizeBytes = request.SizeBytes ?? 0,
            Description = Optional(request.Description),
            Changes = CleanList(request.Changes),
            UploadedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            UploadedAt = DateTime.UtcNow
        };
    }

    /// <summary>A new Draft copy of an earlier version, numbered as a minor bump.</summary>
    public static FloorPlanVersion RestoreCopy(this FloorPlanVersion source, FloorPlan plan, string? userName)
    {
        return new FloorPlanVersion
        {
            Version = NextVersion(plan, "minor"),
            Status = MapOptions.Draft,
            FileUrl = source.FileUrl,
            FileName = source.FileName,
            Format = source.Format,
            SizeBytes = source.SizeBytes,
            Description = $"Restored from {source.Version}",
            Changes = [.. source.Changes],
            UploadedBy = userName,
            UploadedAt = DateTime.UtcNow
        };
    }

    /// <summary>Finds a version by its number; "2.1" and "V2.1" both match v2.1.</summary>
    public static FloorPlanVersion? FindVersion(this FloorPlan plan, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var wanted = version.Trim().TrimStart('v', 'V');
        return plan.Versions.FirstOrDefault(v => string.Equals(v.Version.TrimStart('v', 'V'), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Versions newest first.</summary>
    public static IEnumerable<FloorPlanVersion> NewestFirst(this FloorPlan plan)
    {
        return plan.Versions
            .OrderByDescending(v => ParseVersion(v.Version).Major)
            .ThenByDescending(v => ParseVersion(v.Version).Minor)
            .ThenByDescending(v => v.UploadedAt);
    }

    /// <summary>Semantic vMAJOR.MINOR after the highest existing version: minor v2.0 -> v2.1, major v2.1 -> v3.0.</summary>
    private static string NextVersion(FloorPlan plan, string? bump)
    {
        var highest = plan.Versions
            .Select(v => ParseVersion(v.Version))
            .DefaultIfEmpty((Major: 0, Minor: 0))
            .Max();

        return string.Equals(bump?.Trim(), "major", StringComparison.OrdinalIgnoreCase)
            ? FormatVersion(highest.Major + 1, 0)
            : FormatVersion(Math.Max(highest.Major, 1), highest.Major == 0 ? 0 : highest.Minor + 1);
    }

    private static (int Major, int Minor) ParseVersion(string? version)
    {
        var parts = (version ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var mj) ? mj : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mn) ? mn : 0;
        return (major, minor);
    }

    private static string FormatVersion(int major, int minor)
    {
        return string.Create(CultureInfo.InvariantCulture, $"v{major}.{minor}");
    }

    // ---- Zones ------------------------------------------------------------------------------------------------------

    public static MapZone ToZone(this SaveZoneRequest request, FloorPlan plan, CurrentUser? currentUser)
    {
        var zone = new MapZone
        {
            // Generated on the server so every zone gets a stable id independent of the Mongo _id.
            ZoneId = Guid.NewGuid().ToString(),
            FloorPlanId = plan.FloorPlanId,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            // A zone belongs to the floor plan's tenant unless the request says otherwise
            TenantId = FirstNonEmpty(request.TenantId, plan.TenantId ?? currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, plan.ClientId ?? currentUser?.ClientId),
            IsDeleted = false
        };
        request.CopyTo(zone);
        return zone;
    }

    public static void ApplyTo(this SaveZoneRequest request, MapZone zone, CurrentUser? currentUser)
    {
        request.CopyTo(zone);
        zone.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        zone.UpdatedAt = DateTime.UtcNow;
    }

    private static void CopyTo(this SaveZoneRequest request, MapZone zone)
    {
        zone.Code = request.Code!.Trim();
        zone.Name = request.Name!.Trim();
        zone.ZoneType = Canonical(request.Type, MapOptions.ZoneTypes)!;
        zone.Color = Optional(request.Color) ?? MapOptions.ZoneColors[0];
        zone.Shape = Canonical(request.Shape, MapOptions.ZoneShapes)!;
        zone.X = request.X!.Value;
        zone.Y = request.Y!.Value;
        zone.W = request.W!.Value;
        zone.H = request.H!.Value;
        zone.Points = (request.Points ?? []).Where(point => point is not null).Select(point => new MapPoint { X = point.X, Y = point.Y }).ToList();
        zone.Radius = request.Radius;
        zone.AreaM2 = request.AreaM2;
        zone.Description = Optional(request.Description);
        zone.AccessRule = Canonical(request.AccessRule, MapOptions.AccessRules);
        zone.Active = request.Active ?? true;
        zone.Status = zone.Active ? "Active" : "Inactive";
        zone.Geofence = request.Geofence ?? false;
        zone.EntryAlert = request.EntryAlert ?? false;
        zone.ExitAlert = request.ExitAlert ?? false;
        zone.DwellAlert = request.DwellAlert ?? false;
        zone.DwellMinutes = request.DwellMinutes ?? 0;
    }

    // ---- Plots ------------------------------------------------------------------------------------------------------

    /// <summary>Places or moves an item: every field is taken from the request.</summary>
    public static void ApplyTo(this SavePlotRequest request, MapPlot plot, FloorPlan plan, CurrentUser? currentUser)
    {
        plot.Code = Optional(request.Code);
        plot.Name = Optional(request.Name);
        plot.ItemType = Optional(request.Type);
        plot.AssetType = Optional(request.AssetType);
        plot.Model = Optional(request.Model);
        plot.Serial = Optional(request.Serial);
        plot.Status = Optional(request.Status);
        plot.X = request.X!.Value;
        plot.Y = request.Y!.Value;
        plot.Orientation = request.Orientation ?? 0;
        plot.ZoneId = Optional(request.ZoneId);
        plot.TenantId = FirstNonEmpty(request.TenantId, plot.TenantId ?? plan.TenantId ?? currentUser?.TenantId);
        plot.ClientId = FirstNonEmpty(request.ClientId, plot.ClientId ?? plan.ClientId ?? currentUser?.ClientId);
        plot.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        plot.UpdatedAt = DateTime.UtcNow;
        plot.IsDeleted = false;
    }

    // ---- Layers -----------------------------------------------------------------------------------------------------

    /// <summary>A layer row holding the built-in defaults.</summary>
    public static MapLayer DefaultLayer(LayerDefinition definition, FloorPlan plan)
    {
        return new MapLayer
        {
            FloorPlanId = plan.FloorPlanId,
            LayerKey = definition.Key,
            Enabled = definition.Enabled,
            Visible = true,
            Locked = definition.Locked,
            ShowLabel = true,
            ShowId = true,
            ShowStatusIcon = false,
            UseCategoryColor = true,
            LabelPosition = "Bottom Center",
            Categories = [],
            StatusFilter = "All Statuses",
            Opacity = 80,
            Color = "#5B2AA8",
            TenantId = plan.TenantId,
            ClientId = plan.ClientId,
            IsDeleted = false
        };
    }

    /// <summary>Fields left out keep their stored (or default) value.</summary>
    public static void ApplyTo(this SaveLayerRequest request, MapLayer layer, CurrentUser? currentUser)
    {
        layer.Enabled = request.Enabled ?? layer.Enabled;
        layer.Visible = request.Visible ?? layer.Visible;
        layer.Locked = request.Locked ?? layer.Locked;
        layer.ShowLabel = request.ShowLabel ?? layer.ShowLabel;
        layer.ShowId = request.ShowId ?? layer.ShowId;
        layer.ShowStatusIcon = request.ShowStatusIcon ?? layer.ShowStatusIcon;
        layer.UseCategoryColor = request.UseCategoryColor ?? layer.UseCategoryColor;
        layer.LabelPosition = request.LabelPosition is null ? layer.LabelPosition : Optional(request.LabelPosition);
        layer.Categories = request.Categories is null ? layer.Categories : CleanList(request.Categories);
        layer.StatusFilter = request.StatusFilter is null ? layer.StatusFilter : Optional(request.StatusFilter);
        layer.Opacity = request.Opacity is null ? layer.Opacity : (int)Math.Round(request.Opacity.Value);
        layer.Color = request.Color is null ? layer.Color : Optional(request.Color);
        layer.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        layer.UpdatedAt = DateTime.UtcNow;
    }

    public static MapLayerSet ToEntity(this CreateLayerSetRequest request, CurrentUser? currentUser)
    {
        var layerKeys = MapOptions.Layers.Select(definition => definition.Key).ToArray();
        var layers = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (key, on) in request.Layers ?? [])
        {
            var canonical = Canonical(key, layerKeys);
            if (canonical is not null)
            {
                layers[canonical] = on;
            }
        }

        return new MapLayerSet
        {
            // Generated on the server so every layer set gets a stable id independent of the Mongo _id.
            LayerSetId = Guid.NewGuid().ToString(),
            Name = request.Name!.Trim(),
            Layers = layers,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    // ---- Settings ---------------------------------------------------------------------------------------------------

    /// <summary>Fields left out keep their saved value. Unknown keys (in "extra" or at the top level) are stored in Extra.</summary>
    public static void ApplyTo(this SaveMapSettingsRequest request, MapSettings settings)
    {
        settings.DefaultView = Optional(request.DefaultView) ?? settings.DefaultView;
        settings.DefaultZoom = request.DefaultZoom ?? settings.DefaultZoom;
        settings.Unit = Optional(request.Unit) ?? settings.Unit;
        settings.FileTypes = CleanFlags(request.FileTypes) ?? settings.FileTypes;
        settings.MaxFileMb = request.MaxFileMb ?? settings.MaxFileMb;
        settings.CoordinateSystem = Optional(request.CoordinateSystem) ?? settings.CoordinateSystem;
        settings.Plotted = CleanFlags(request.Plotted) ?? settings.Plotted;
        settings.AssetLabel = Optional(request.AssetLabel) ?? settings.AssetLabel;
        settings.LabelPosition = Optional(request.LabelPosition) ?? settings.LabelPosition;
        settings.StatusIcon = request.StatusIcon ?? settings.StatusIcon;
        settings.Snap = request.Snap ?? settings.Snap;
        settings.GridSize = request.GridSize ?? settings.GridSize;
        settings.ZoneType = Canonical(request.ZoneType, MapOptions.ZoneTypes) ?? settings.ZoneType;
        settings.DwellMinutes = request.DwellMinutes ?? settings.DwellMinutes;
        settings.ZoneColor = Optional(request.ZoneColor) ?? settings.ZoneColor;
        settings.ZoneOverlap = request.ZoneOverlap ?? settings.ZoneOverlap;
        settings.GeofenceLogging = request.GeofenceLogging ?? settings.GeofenceLogging;
        settings.Versioning = Optional(request.Versioning) ?? settings.Versioning;
        settings.Approval = request.Approval ?? settings.Approval;
        settings.KeepVersions = request.KeepVersions ?? settings.KeepVersions;
        settings.AutoArchive = request.AutoArchive ?? settings.AutoArchive;
        settings.ArchiveAfter = request.ArchiveAfter ?? settings.ArchiveAfter;
        settings.MapAccess = Optional(request.MapAccess) ?? settings.MapAccess;
        settings.AllowDownload = request.AllowDownload ?? settings.AllowDownload;
        settings.Watermark = request.Watermark ?? settings.Watermark;
        settings.NotifyPublish = request.NotifyPublish ?? settings.NotifyPublish;
        settings.NotifyGeofence = request.NotifyGeofence ?? settings.NotifyGeofence;
        settings.NotifyVersion = request.NotifyVersion ?? settings.NotifyVersion;
        settings.CalibrationPoints = request.CalibrationPoints ?? settings.CalibrationPoints;
        settings.CalibrationTolerance = request.CalibrationTolerance ?? settings.CalibrationTolerance;
        settings.OriginX = request.OriginX ?? settings.OriginX;
        settings.OriginY = request.OriginY ?? settings.OriginY;
        settings.Rotation = request.Rotation ?? settings.Rotation;
        settings.Integrations = CleanFlags(request.Integrations) ?? settings.Integrations;
        settings.TileCache = request.TileCache ?? settings.TileCache;
        settings.CacheHours = request.CacheHours ?? settings.CacheHours;
        settings.AuditEdits = request.AuditEdits ?? settings.AuditEdits;

        if (request.Extra is not null)
        {
            settings.Extra = ToBsonDocument(request.Extra);
        }

        if (request.Unknown is { Count: > 0 })
        {
            foreach (var element in ToBsonDocument(request.Unknown))
            {
                settings.Extra[element.Name] = element.Value;
            }
        }
    }

    /// <summary>Extra values as plain .NET values (string, number, bool, null, nested dictionaries and lists) for JSON output.</summary>
    public static IReadOnlyDictionary<string, object?> ToPlainDictionary(BsonDocument? document)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (document is null)
        {
            return result;
        }

        foreach (var element in document)
        {
            result[element.Name] = ToPlainValue(element.Value);
        }

        return result;
    }

    private static object? ToPlainValue(BsonValue value)
    {
        return value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.Boolean => value.AsBoolean,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.Document => ToPlainDictionary(value.AsBsonDocument),
            BsonType.Array => value.AsBsonArray.Select(ToPlainValue).ToList(),
            BsonType.Null => null,
            _ => value.ToString()
        };
    }

    private static BsonDocument ToBsonDocument(IEnumerable<KeyValuePair<string, JsonElement>> values)
    {
        var document = new BsonDocument();
        foreach (var (key, element) in values)
        {
            var name = SafeKey(key);
            if (name is not null)
            {
                document[name] = ToBsonValue(element);
            }
        }

        return document;
    }

    private static BsonValue ToBsonValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return new BsonString(element.GetString() ?? string.Empty);
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var int32))
                {
                    return new BsonInt32(int32);
                }
                if (element.TryGetInt64(out var int64))
                {
                    return new BsonInt64(int64);
                }
                return new BsonDouble(element.GetDouble());
            case JsonValueKind.True:
                return BsonBoolean.True;
            case JsonValueKind.False:
                return BsonBoolean.False;
            case JsonValueKind.Object:
                return ToBsonDocument(element.EnumerateObject().Select(property => new KeyValuePair<string, JsonElement>(property.Name, property.Value)));
            case JsonValueKind.Array:
                return new BsonArray(element.EnumerateArray().Select(ToBsonValue));
            default:
                return BsonNull.Value;
        }
    }

    /// <summary>Trims keys and drops empty ones; Mongo field names can't contain '.' or start with '$'.</summary>
    private static Dictionary<string, bool>? CleanFlags(Dictionary<string, bool>? flags)
    {
        if (flags is null)
        {
            return null;
        }

        var clean = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (key, value) in flags)
        {
            var name = SafeKey(key);
            if (name is not null)
            {
                clean[name] = value;
            }
        }

        return clean;
    }

    /// <summary>Mongo field names can't contain '.' or start with '$': dots become '_' and leading '$' are dropped.</summary>
    private static string? SafeKey(string? key)
    {
        var name = key?.Trim().Replace('.', '_').TrimStart('$');
        return string.IsNullOrEmpty(name) ? null : name;
    }

    // ---- Shared -----------------------------------------------------------------------------------------------------

    /// <summary>The allowed value matching (case-insensitively) the given one, or null when empty or not allowed.</summary>
    public static string? Canonical(string? value, string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return allowed.FirstOrDefault(option => string.Equals(option, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> CleanList(IEnumerable<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
    }
}
