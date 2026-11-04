using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class DeviceAssetPlotMappings
{
    public static DeviceAssetPlot ToEntity(this CreateDeviceAssetPlotRequest request, CurrentUser? currentUser)
    {
        return new DeviceAssetPlot
        {
            // Generated on the server so every plot gets a stable id independent of the Mongo _id.
            PlotId = Guid.NewGuid().ToString(),
            FloorPlanId = Optional(request.FloorPlanId),
            FloorPlanName = Optional(request.FloorPlanName),
            ItemType = DeviceAssetPlotValidationExtensions.Normalize(request.ItemType, DeviceAssetPlotItemType.All)!,
            ItemId = Optional(request.ItemId),
            ItemCode = Optional(request.ItemCode),
            ItemName = request.ItemName!.Trim(),
            AssetCategory = Optional(request.AssetCategory),
            AssetType = Optional(request.AssetType),
            Model = Optional(request.Model),
            SerialNumber = Optional(request.SerialNumber),
            LocationId = Optional(request.LocationId),
            LocationPath = request.LocationPath!.Trim(),
            ZoneId = Optional(request.ZoneId),
            ZoneCode = Optional(request.ZoneCode),
            ZoneName = Optional(request.ZoneName),
            PositionX = request.PositionX!.Value,
            PositionY = request.PositionY!.Value,
            Orientation = request.Orientation ?? 0,
            Icon = Optional(request.Icon),
            Color = Optional(request.Color)?.ToUpperInvariant(),
            Status = DeviceAssetPlotValidationExtensions.Normalize(request.Status, DeviceAssetPlotStatus.All) ?? DeviceAssetPlotStatus.Active,
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
