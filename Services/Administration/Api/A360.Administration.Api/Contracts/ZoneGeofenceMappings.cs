using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class ZoneGeofenceMappings
{
    public static ZoneGeofence ToEntity(this CreateZoneGeofenceRequest request, CurrentUser? currentUser)
    {
        return new ZoneGeofence
        {
            // Generated on the server so every zone gets a stable id independent of the Mongo _id.
            ZoneId = Guid.NewGuid().ToString(),
            FloorPlanId = Optional(request.FloorPlanId),
            FloorPlanName = Optional(request.FloorPlanName),
            ZoneName = request.ZoneName!.Trim(),
            ZoneCode = request.ZoneCode!.Trim().ToUpperInvariant(),
            ZoneType = ZoneGeofenceValidationExtensions.Normalize(request.ZoneType, MapZoneType.All)!,
            LocationId = Optional(request.LocationId),
            LocationPath = Optional(request.LocationPath),
            Area = Round(request.Area),
            Perimeter = Round(request.Perimeter),
            Description = Optional(request.Description),
            AccessRule = ZoneGeofenceValidationExtensions.Normalize(request.AccessRule, MapZoneAccessRule.All),
            Status = ZoneGeofenceValidationExtensions.Normalize(request.Status, MapZoneStatus.All) ?? MapZoneStatus.Active,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static double? Round(double? value)
    {
        return value is null ? null : Math.Round(value.Value, 2);
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
