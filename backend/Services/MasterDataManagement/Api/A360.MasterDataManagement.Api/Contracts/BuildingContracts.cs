using BuildingEntity = A360.MasterDataManagement.Domain.Entities.Building;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateBuildingRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
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
    string? Category,
    StructureAddressDto? Address,
    double? Latitude,
    double? Longitude,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public BuildingEntity ToEntity()
    {
        return new BuildingEntity
        {
            Name = Name!.Trim(),
            Code = Code!.Trim(),
            Site = Site?.ToReference(),
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
            Category = StructureText.Clean(Category),
            Address = Address?.ToValue(),
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

public sealed record UpdateBuildingRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
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
    string? Category,
    StructureAddressDto? Address,
    double? Latitude,
    double? Longitude,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(BuildingEntity building)
    {
        building.Name = Name!.Trim();
        building.Code = Code!.Trim();
        building.Site = Site?.ToReference();
        building.ParentLevel = string.IsNullOrWhiteSpace(ParentLevel) ? null : ParentLevel.Trim();
        building.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        building.Capacity = Capacity;
        building.Area = Area;

        building.ImageUrl = StructureText.Clean(ImageUrl);
        building.Images = StructureText.CleanList(Images);
        building.AllowAssetAssignment = AllowAssetAssignment;
        building.EnableTracking = EnableTracking;
        building.RestrictedAccess = RestrictedAccess;
        building.ShowOnMap = ShowOnMap;
        building.Category = StructureText.Clean(Category);
        building.Address = Address?.ToValue();
        building.Latitude = Latitude;
        building.Longitude = Longitude;
        building.UpdatedBy = UpdatedBy;
        building.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            building.Status = Status;
        }
    }
}

public sealed record UpdateBuildingStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record BuildingResponse(
    string Id,
    string Name,
    string Code,
    MasterDataReferenceDto? Site,
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
    string? Category,
    StructureAddressDto? Address,
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
    public static BuildingResponse FromEntity(BuildingEntity building)
    {
        return new BuildingResponse(
            building.Id ?? string.Empty,
            building.Name ?? string.Empty,
            building.Code ?? string.Empty,
            building.Site is null
                ? null
                : MasterDataReferenceDto.FromReference(building.Site),
            building.ParentLevel,
            building.Description,
            building.Capacity,
            building.Area,
            building.ImageUrl,
            building.Images ?? [],
            building.AllowAssetAssignment,
            building.EnableTracking,
            building.RestrictedAccess,
            building.ShowOnMap,
            building.Category,
            StructureAddressDto.FromValue(building.Address),
            building.Latitude,
            building.Longitude,
            building.CreatedBy,
            building.CreatedAt,
            building.UpdatedBy,
            building.UpdatedAt,
            building.ClientId,
            building.TenantId,
            building.Status,
            building.IsDeleted
        );
    }
}
