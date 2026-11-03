using ZoneEntity = A360.MasterDataManagement.Domain.Entities.Zone;
using FloorEntity = A360.MasterDataManagement.Domain.Entities.Floor;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ZoneEndpoints
{
    public static RouteGroupBuilder MapZoneEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/zones")
            .WithTags("Zones");

        group.MapGet("", GetZonesAsync)
            .WithName("GetZones");

        group.MapGet("/{id}", GetZoneByIdAsync)
            .WithName("GetZoneById");

        group.MapPost("", CreateZoneAsync)
            .WithName("CreateZone");

        group.MapPut("/{id}", UpdateZoneAsync)
            .WithName("UpdateZone");

        group.MapPatch("/{id}/status", UpdateZoneStatusAsync)
            .WithName("UpdateZoneStatus");

        group.MapDelete("/{id}", DeleteZoneAsync)
            .WithName("DeleteZone");

        return group;
    }

    private static async Task<IResult> GetZonesAsync(
        string? search,
        string? status,
        string? siteId,
        string? buildingId,
        string? floorId,
        string? parentLevel,
        string? clientId,
        IZoneRepository repository,
        CancellationToken cancellationToken)
    {
        var zones = await repository.SearchAsync(
            new ZoneFilter(search, status, siteId, buildingId, floorId, parentLevel, clientId),
            cancellationToken);

        return Results.Ok(
            zones.Select(ZoneResponse.FromEntity));
    }

    private static async Task<IResult> GetZoneByIdAsync(
        string id,
        IZoneRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid zone id." });
        }

        var zone = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (zone is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Zone",
            zone.Id,
            zone.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> CreateZoneAsync(
        CreateZoneRequest request,
        IZoneRepository repository,
        IBuildingRepository buildings,
        IFloorRepository floors,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zone = request.ToEntity();

        var referenceError = await ResolveReferencesAsync(
            zone,
            buildings,
            floors,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            zone.Code,
            zone.BuildingId,
            zone.FloorId,
            zone.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A zone with code '{zone.Code}' already exists on {Placement(zone)}." });
        }

        zone = await repository.CreateAsync(
            zone,
            cancellationToken);

        await eventLogger.LogAsync(
            "Zone",
            zone.Id,
            zone.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/zones/{zone.Id}",
            ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> UpdateZoneAsync(
        string id,
        UpdateZoneRequest request,
        IZoneRepository repository,
        IBuildingRepository buildings,
        IFloorRepository floors,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid zone id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zone = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (zone is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(zone);

        var referenceError = await ResolveReferencesAsync(
            zone,
            buildings,
            floors,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            zone.Code,
            zone.BuildingId,
            zone.FloorId,
            zone.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A zone with code '{zone.Code}' already exists on {Placement(zone)}." });
        }

        var updated = await repository.UpdateAsync(
            id,
            zone,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Zone",
            zone.Id,
            zone.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> UpdateZoneStatusAsync(
        string id,
        UpdateZoneStatusRequest request,
        IZoneRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid zone id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zone = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (zone is null)
        {
            return Results.NotFound();
        }

        zone.Status = request.Status;
        zone.UpdatedBy = request.UpdatedBy;
        zone.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            zone,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Zone",
            zone.Id,
            zone.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> DeleteZoneAsync(
        string id,
        IZoneRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid zone id." });
        }

        var zone = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (zone is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Zone",
            zone.Id,
            zone.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the floor (when given) and building exist and agree with each other, copies their names onto
    /// the zone, then resolves the site against the floor's or building's site.
    /// Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        ZoneEntity zone,
        IBuildingRepository buildings,
        IFloorRepository floors,
        ISiteRepository sites,
        CancellationToken cancellationToken)
    {
        FloorEntity? floor = null;

        if (zone.FloorId is not null)
        {
            floor = MongoObjectId.IsValid(zone.FloorId)
                ? await floors.GetByIdAsync(zone.FloorId, cancellationToken)
                : null;

            if (floor is null)
            {
                return Results.BadRequest(
                    new { message = $"Floor '{zone.FloorId}' was not found." });
            }

            if (string.IsNullOrEmpty(zone.BuildingId))
            {
                zone.BuildingId = floor.BuildingId;
            }
            else if (zone.BuildingId != floor.BuildingId)
            {
                return Results.BadRequest(
                    new { message = $"Floor '{floor.Name}' does not belong to building '{zone.BuildingId}'." });
            }

            zone.FloorName = floor.Name;
        }
        else
        {
            zone.FloorName = null;
        }

        var building = MongoObjectId.IsValid(zone.BuildingId)
            ? await buildings.GetByIdAsync(zone.BuildingId, cancellationToken)
            : null;

        if (building is null)
        {
            return Results.BadRequest(
                new { message = $"Building '{zone.BuildingId}' was not found." });
        }

        zone.BuildingName = building.Name;

        var (site, siteError) = floor is null
            ? await MasterDataReferenceResolver.ResolveSiteFromParentAsync(
                zone.Site, building.Site, $"building '{building.Name}'", sites, cancellationToken)
            : await MasterDataReferenceResolver.ResolveSiteFromParentAsync(
                zone.Site, floor.Site, $"floor '{floor.Name}'", sites, cancellationToken);

        zone.Site = site;
        return siteError;
    }

    private static string Placement(ZoneEntity zone)
    {
        return zone.FloorName is null
            ? $"building '{zone.BuildingName}'"
            : $"floor '{zone.FloorName}' of building '{zone.BuildingName}'";
    }
}
