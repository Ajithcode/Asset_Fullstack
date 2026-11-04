using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class MapLibraryZoneMappings
{
    public static MapLibraryZone ToEntity(this CreateMapLibraryZoneRequest request, CurrentUser? currentUser)
    {
        return new MapLibraryZone
        {
            // Generated on the server so every zone gets a stable id independent of the Mongo _id.
            ZoneId = Guid.NewGuid().ToString(),
            FloorPlanId = Optional(request.FloorPlanId),
            FloorPlanName = Optional(request.FloorPlanName),
            ZoneName = request.ZoneName!.Trim(),
            ZoneCode = request.ZoneCode!.Trim().ToUpperInvariant(),
            ZoneType = MapLibraryZoneValidationExtensions.Normalize(request.ZoneType, MapZoneType.All)!,
            ParentLocationId = Optional(request.ParentLocationId),
            ParentLocationPath = Optional(request.ParentLocationPath),
            Area = request.Area is null ? null : Math.Round(request.Area.Value, 2),
            Description = Optional(request.Description),
            AccessRule = MapLibraryZoneValidationExtensions.Normalize(request.AccessRule, MapZoneAccessRule.All),
            Status = MapLibraryZoneValidationExtensions.Normalize(request.Status, MapZoneStatus.All) ?? MapZoneStatus.Active,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
