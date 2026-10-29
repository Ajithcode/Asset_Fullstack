using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class LocationMappings
{
    public static Location ToEntity(this CreateLocationRequest request, CurrentUser? currentUser)
    {
        return new Location
        {
            // Generated on the server so every location gets a stable id independent of the Mongo _id.
            LocationId = Guid.NewGuid().ToString(),
            LocationCode = request.LocationCode!.Trim(),
            LocationName = request.LocationName!.Trim(),
            LocationType = request.LocationType!.Trim(),
            ParentLocationId = string.IsNullOrWhiteSpace(request.ParentLocationId) ? null : request.ParentLocationId.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Country = request.Country!.Trim(),
            City = request.City!.Trim(),
            TimeZone = request.TimeZone!.Trim(),
            LocationManager = request.LocationManager!.Trim(),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
