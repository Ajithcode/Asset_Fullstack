using DeviceDrawingEntity = A360.Tracking.Domain.Entities.DeviceDrawing;

namespace A360.Tracking.Api.Contracts;

internal static class DeviceDrawingMappings
{
    public static DeviceDrawingEntity ToEntity(this CreateDeviceDrawingRequest request, string? currentUserName)
    {
        return new DeviceDrawingEntity
        {
            ProjectId = Clean(request.ProjectId),
            CountryId = Clean(request.CountryId),
            AreaId = Clean(request.AreaId),
            BuildingId = Clean(request.BuildingId),
            FloorId = Clean(request.FloorId),
            ZoneId = Clean(request.ZoneId),
            ZoneName = Clean(request.ZoneName),
            TopZone = Clean(request.TopZone),
            DeviceReferenceId = Clean(request.DeviceReferenceId),
            DeviceUniqueId = Clean(request.DeviceUniqueId),
            DeviceName = Clean(request.DeviceName),
            UserId = FirstNonEmpty(request.UserId, currentUserName),
            Priority = Clean(request.Priority),
            AssemblyPoint = request.AssemblyPoint,
            Exit = Clean(request.Exit),
            Status = request.Status,
            DeviceGeoJsonData = GeoJsonConversion.ToEntity(request.DeviceGeoJsonData),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUserName),
            // The client sends the time the device was placed; the server time is only a fallback.
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
