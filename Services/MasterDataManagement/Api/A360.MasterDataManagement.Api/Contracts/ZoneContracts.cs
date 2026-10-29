using ZoneEntity = A360.MasterDataManagement.Domain.Entities.Zone;

namespace A360.MasterDataManagement.Api.Contracts;

// BuildingName and FloorName are not taken from the request: they are always copied from the records.
// BuildingId may be left out when FloorId is given; it is then taken from the floor.
public sealed record CreateZoneRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
    string? BuildingId,
    string? FloorId,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ZoneEntity ToEntity()
    {
        return new ZoneEntity
        {
            Name = Name!.Trim(),
            Code = Code!.Trim(),
            Site = Site?.ToReference(),
            BuildingId = BuildingId?.Trim() ?? string.Empty,
            BuildingName = string.Empty,
            FloorId = string.IsNullOrWhiteSpace(FloorId) ? null : FloorId.Trim(),
            ParentLevel = string.IsNullOrWhiteSpace(ParentLevel) ? null : ParentLevel.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Capacity = Capacity,
            Area = Area,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateZoneRequest(
    string? Name,
    string? Code,
    MasterDataReferenceDto? Site,
    string? BuildingId,
    string? FloorId,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ZoneEntity zone)
    {
        zone.Name = Name!.Trim();
        zone.Code = Code!.Trim();
        zone.Site = Site?.ToReference();
        zone.BuildingId = BuildingId?.Trim() ?? string.Empty;
        zone.FloorId = string.IsNullOrWhiteSpace(FloorId) ? null : FloorId.Trim();
        zone.FloorName = null;
        zone.ParentLevel = string.IsNullOrWhiteSpace(ParentLevel) ? null : ParentLevel.Trim();
        zone.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        zone.Capacity = Capacity;
        zone.Area = Area;

        zone.UpdatedBy = UpdatedBy;
        zone.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            zone.Status = Status;
        }
    }
}

public sealed record UpdateZoneStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ZoneResponse(
    string Id,
    string Name,
    string Code,
    MasterDataReferenceDto? Site,
    string BuildingId,
    string BuildingName,
    string? FloorId,
    string? FloorName,
    string? ParentLevel,
    string? Description,
    int? Capacity,
    double? Area,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ZoneResponse FromEntity(ZoneEntity zone)
    {
        return new ZoneResponse(
            zone.Id ?? string.Empty,
            zone.Name ?? string.Empty,
            zone.Code ?? string.Empty,
            zone.Site is null
                ? null
                : MasterDataReferenceDto.FromReference(zone.Site),
            zone.BuildingId ?? string.Empty,
            zone.BuildingName ?? string.Empty,
            zone.FloorId,
            zone.FloorName,
            zone.ParentLevel,
            zone.Description,
            zone.Capacity,
            zone.Area,
            zone.CreatedBy,
            zone.CreatedAt,
            zone.UpdatedBy,
            zone.UpdatedAt,
            zone.ClientId,
            zone.TenantId,
            zone.Status,
            zone.IsDeleted
        );
    }
}
