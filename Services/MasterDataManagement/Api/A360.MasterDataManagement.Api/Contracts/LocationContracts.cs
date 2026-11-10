using LocationEntity = A360.MasterDataManagement.Domain.Entities.Location;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateLocationRequest(
    string? LocationName,
    string? Code,
    string? Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public LocationEntity ToEntity()
    {
        return new LocationEntity
        {
            LocationName = LocationName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Building = Building,
            Floor = Floor,
            Zone = Zone,
            ParentLocationId = string.IsNullOrWhiteSpace(ParentLocationId) ? null : ParentLocationId,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateLocationRequest(
    string? LocationName,
    string? Code,
    string? Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(LocationEntity location)
    {
        location.LocationName = LocationName!.Trim();
        location.Code = Code!.Trim();
        location.Type = Type!.Trim();
        location.Building = Building;
        location.Floor = Floor;
        location.Zone = Zone;
        location.ParentLocationId = string.IsNullOrWhiteSpace(ParentLocationId) ? null : ParentLocationId;

        location.UpdatedBy = UpdatedBy;
        location.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            location.Status = Status;
        }
    }
}

public sealed record UpdateLocationStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record LocationResponse(
    string Id,
    string LocationName,
    string Code,
    string Type,
    string? Building,
    string? Floor,
    string? Zone,
    string? ParentLocationId,
    string? ParentLocationName,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static LocationResponse FromEntity(LocationEntity location)
    {
        return new LocationResponse(
            location.Id ?? string.Empty,
            location.LocationName ?? string.Empty,
            location.Code ?? string.Empty,
            location.Type ?? string.Empty,
            location.Building,
            location.Floor,
            location.Zone,
            location.ParentLocationId,
            location.ParentLocationName,
            location.CreatedBy,
            location.CreatedAt,
            location.UpdatedBy,
            location.UpdatedAt,
            location.ClientId,
            location.TenantId,
            location.Status,
            location.IsDeleted
        );
    }
}

/// <summary>One location in the hierarchy tree (e.g. Country → Site → Building → Floor → Zone → Room), children sorted by name.</summary>
public sealed record LocationTreeNodeResponse(
    string Id,
    string LocationName,
    string Code,
    string Type,
    string? ParentLocationId,
    string? Status,
    IReadOnlyList<LocationTreeNodeResponse> Children)
{
    /// <summary>
    /// Builds the tree from a flat list. With <paramref name="rootId"/> only that location's subtree is returned;
    /// otherwise every location whose parent is not in the list (top level, or orphaned) becomes a root.
    /// With <paramref name="status"/>, a location with another status is left out together with everything below it.
    /// </summary>
    public static IReadOnlyList<LocationTreeNodeResponse> Build(
        IReadOnlyCollection<LocationEntity> locations,
        string? rootId,
        int? maxDepth,
        string? status)
    {
        var byId = locations
            .Where(location => !location.IsDeleted && !string.IsNullOrEmpty(location.Id))
            .GroupBy(location => location.Id!)
            .ToDictionary(group => group.Key, group => group.First());
        var childrenByParent = byId.Values
            .Where(location => location.ParentLocationId is not null)
            .ToLookup(location => location.ParentLocationId!);

        bool Included(LocationEntity location) =>
            string.IsNullOrWhiteSpace(status)
            || string.Equals(location.Status?.Trim(), status.Trim(), StringComparison.OrdinalIgnoreCase);

        IEnumerable<LocationEntity> roots = rootId is not null
            ? byId.TryGetValue(rootId, out var root) ? [root] : []
            : byId.Values.Where(location => location.ParentLocationId is null || !byId.ContainsKey(location.ParentLocationId));

        // Guards against corrupt data where a location ends up as its own ancestor.
        var visited = new HashSet<string>();

        return roots
            .Where(Included)
            .OrderBy(location => location.LocationName, StringComparer.OrdinalIgnoreCase)
            .Select(location => ToNode(location, 1))
            .ToList();

        LocationTreeNodeResponse ToNode(LocationEntity location, int depth)
        {
            visited.Add(location.Id!);

            var children = maxDepth is not null && depth >= maxDepth
                ? []
                : childrenByParent[location.Id!]
                    .Where(child => Included(child) && !visited.Contains(child.Id!))
                    .OrderBy(child => child.LocationName, StringComparer.OrdinalIgnoreCase)
                    .Select(child => ToNode(child, depth + 1))
                    .ToList();

            return new LocationTreeNodeResponse(
                location.Id!,
                location.LocationName ?? string.Empty,
                location.Code ?? string.Empty,
                location.Type ?? string.Empty,
                location.ParentLocationId,
                location.Status,
                children);
        }
    }
}
