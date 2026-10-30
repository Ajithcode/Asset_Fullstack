using FloorEntity = A360.MasterDataManagement.Domain.Entities.Floor;

namespace A360.MasterDataManagement.Api.Contracts;

// BuildingName is not taken from the request: it is always copied from the building.
public sealed record CreateFloorRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
    string? BuildingId,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? ImageUrl,
    List<string>? Images,
    bool AllowAssetAssignment,
    bool EnableTracking,
    bool RestrictedAccess,
    bool ShowOnMap,
    int? LevelNumber,
    double? Latitude,
    double? Longitude,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public FloorEntity ToEntity()
    {
        return new FloorEntity
        {
            Name = Name!.Trim(),
            Code = Code!.Trim(),
            Site = Site?.ToReference(),
            BuildingId = BuildingId!.Trim(),
            BuildingName = string.Empty,
            ParentLevel = string.IsNullOrWhiteSpace(ParentLevel) ? null : ParentLevel.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Capacity = Capacity,
            Area = Area,

            ImageUrl = StructureText.Clean(ImageUrl),
            Images = StructureText.CleanList(Images),
            AllowAssetAssignment = AllowAssetAssignment,
            EnableTracking = EnableTracking,
            RestrictedAccess = RestrictedAccess,
            ShowOnMap = ShowOnMap,
            LevelNumber = LevelNumber,
            Latitude = Latitude,
            Longitude = Longitude,
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateFloorRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
    string? BuildingId,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? ImageUrl,
    List<string>? Images,
    bool AllowAssetAssignment,
    bool EnableTracking,
    bool RestrictedAccess,
    bool ShowOnMap,
    int? LevelNumber,
    double? Latitude,
    double? Longitude,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(FloorEntity floor)
    {
        floor.Name = Name!.Trim();
        floor.Code = Code!.Trim();
        floor.Site = Site?.ToReference();
        floor.BuildingId = BuildingId!.Trim();
        floor.ParentLevel = string.IsNullOrWhiteSpace(ParentLevel) ? null : ParentLevel.Trim();
        floor.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        floor.Capacity = Capacity;
        floor.Area = Area;

        floor.ImageUrl = StructureText.Clean(ImageUrl);
        floor.Images = StructureText.CleanList(Images);
        floor.AllowAssetAssignment = AllowAssetAssignment;
        floor.EnableTracking = EnableTracking;
        floor.RestrictedAccess = RestrictedAccess;
        floor.ShowOnMap = ShowOnMap;
        floor.LevelNumber = LevelNumber;
        floor.Latitude = Latitude;
        floor.Longitude = Longitude;
        floor.UpdatedBy = UpdatedBy;
        floor.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            floor.Status = Status;
        }
    }
}

public sealed record UpdateFloorStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record FloorResponse(
    string Id,
    string Name,
    string Code,
    MasterDataReferenceDto? Site,
    string BuildingId,
    string BuildingName,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? ImageUrl,
    IReadOnlyList<string> Images,
    bool AllowAssetAssignment,
    bool EnableTracking,
    bool RestrictedAccess,
    bool ShowOnMap,
    int? LevelNumber,
    double? Latitude,
    double? Longitude,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static FloorResponse FromEntity(FloorEntity floor)
    {
        return new FloorResponse(
            floor.Id ?? string.Empty,
            floor.Name ?? string.Empty,
            floor.Code ?? string.Empty,
            floor.Site is null
                ? null
                : MasterDataReferenceDto.FromReference(floor.Site),
            floor.BuildingId ?? string.Empty,
            floor.BuildingName ?? string.Empty,
            floor.ParentLevel,
            floor.Description,
            floor.Capacity,
            floor.Area,
            floor.ImageUrl,
            floor.Images ?? [],
            floor.AllowAssetAssignment,
            floor.EnableTracking,
            floor.RestrictedAccess,
            floor.ShowOnMap,
            floor.LevelNumber,
            floor.Latitude,
            floor.Longitude,
            floor.CreatedBy,
            floor.CreatedAt,
            floor.UpdatedBy,
            floor.UpdatedAt,
            floor.ClientId,
            floor.TenantId,
            floor.Status,
            floor.IsDeleted
        );
    }
}
