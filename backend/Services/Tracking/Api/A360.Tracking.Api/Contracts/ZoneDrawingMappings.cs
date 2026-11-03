using ZoneDrawingEntity = A360.Tracking.Domain.Entities.ZoneDrawing;

namespace A360.Tracking.Api.Contracts;

internal static class ZoneDrawingMappings
{
    public static ZoneDrawingEntity ToEntity(this CreateZoneDrawingRequest request, string? currentUserName)
    {
        return new ZoneDrawingEntity
        {
            ProjectId = Clean(request.ProjectId),
            CountryId = Clean(request.CountryId),
            AreaId = Clean(request.AreaId),
            BuildingId = Clean(request.BuildingId),
            FloorId = Clean(request.FloorId),
            ZoneId = Clean(request.ZoneId),
            ZoneName = Clean(request.ZoneName),
            TopZone = Clean(request.TopZone),
            Priority = Clean(request.Priority),
            AssemblyPoint = request.AssemblyPoint,
            Exit = Clean(request.Exit),
            Status = request.Status,
            GeoJsonData = GeoJsonConversion.ToEntity(request.GeoJsonData),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUserName),
            // The client sends the time the zone was drawn; the server time is only a fallback.
            CreatedAt = request.CreatedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            ClientId = Clean(request.ClientId),
            IsDeleted = false
        };
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
