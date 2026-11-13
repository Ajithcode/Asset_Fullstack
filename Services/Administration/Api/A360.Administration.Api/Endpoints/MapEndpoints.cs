using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>
/// Maps &amp; Floor Plans: floor plans with embedded versions, zones, plotted assets/devices, layers, layer sets and settings.
/// Positions on a plan are percent of the plan (0-100). Locations are Master Data Management ids.
/// </summary>
public static class MapEndpoints
{
    private const string Route = "/api/administration/maps";
    private const string FloorPlanEntity = "FloorPlan";
    private const string ZoneEntity = "MapZone";
    private const string PlotEntity = "MapPlot";
    private const string LayerEntity = "MapLayer";
    private const string LayerSetEntity = "MapLayerSet";
    private const string SettingsEntity = "MapSettings";

    public static RouteGroupBuilder MapMapEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("Maps & Floor Plans");

        group.MapGet("/summary", GetSummaryAsync).WithName("GetMapSummary");

        group.MapGet("/floor-plans", GetFloorPlansAsync).WithName("GetFloorPlans");
        group.MapGet("/floor-plans/{floorPlanId}", GetFloorPlanByIdAsync).WithName("GetFloorPlanById");
        group.MapPost("/floor-plans", CreateFloorPlanAsync).WithName("CreateFloorPlan");
        group.MapPut("/floor-plans/{floorPlanId}", UpdateFloorPlanAsync).WithName("UpdateFloorPlan");
        group.MapDelete("/floor-plans/{floorPlanId}", DeleteFloorPlanAsync).WithName("DeleteFloorPlan");
        group.MapPost("/floor-plans/{floorPlanId}/publish", PublishFloorPlanAsync).WithName("PublishFloorPlan");
        group.MapPost("/floor-plans/{floorPlanId}/archive", ArchiveFloorPlanAsync).WithName("ArchiveFloorPlan");

        group.MapGet("/floor-plans/{floorPlanId}/versions", GetVersionsAsync).WithName("GetFloorPlanVersions");
        group.MapPost("/floor-plans/{floorPlanId}/versions", CreateVersionAsync).WithName("CreateFloorPlanVersion");
        group.MapPost("/floor-plans/{floorPlanId}/versions/{version}/publish", PublishVersionAsync).WithName("PublishFloorPlanVersion");
        group.MapPost("/floor-plans/{floorPlanId}/versions/{version}/restore", RestoreVersionAsync).WithName("RestoreFloorPlanVersion");

        group.MapGet("/zones/options", GetZoneOptions).WithName("GetMapZoneOptions");
        group.MapGet("/floor-plans/{floorPlanId}/zones", GetZonesAsync).WithName("GetMapZones");
        group.MapPost("/floor-plans/{floorPlanId}/zones", CreateZoneAsync).WithName("CreateMapZone");
        group.MapPut("/zones/{zoneId}", UpdateZoneAsync).WithName("UpdateMapZone");
        group.MapDelete("/zones/{zoneId}", DeleteZoneAsync).WithName("DeleteMapZone");

        group.MapGet("/floor-plans/{floorPlanId}/plots", GetPlotsAsync).WithName("GetMapPlots");
        group.MapPut("/floor-plans/{floorPlanId}/plots/{kind}/{itemId}", SavePlotAsync).WithName("SaveMapPlot");
        group.MapDelete("/floor-plans/{floorPlanId}/plots/{kind}/{itemId}", DeletePlotAsync).WithName("DeleteMapPlot");

        group.MapGet("/floor-plans/{floorPlanId}/layers", GetLayersAsync).WithName("GetMapLayers");
        group.MapPut("/floor-plans/{floorPlanId}/layers/{layerKey}", SaveLayerAsync).WithName("SaveMapLayer");
        group.MapGet("/layer-sets", GetLayerSetsAsync).WithName("GetMapLayerSets");
        group.MapPost("/layer-sets", CreateLayerSetAsync).WithName("CreateMapLayerSet");
        group.MapDelete("/layer-sets/{layerSetId}", DeleteLayerSetAsync).WithName("DeleteMapLayerSet");

        group.MapGet("/settings", GetSettingsAsync).WithName("GetMapSettings");
        group.MapPut("/settings", SaveSettingsAsync).WithName("SaveMapSettings");

        return group;
    }

    // ---- Summary ----------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetSummaryAsync(
        string? locationId,
        string? clientId,
        string? tenantId,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        CancellationToken cancellationToken)
    {
        var plans = await repository.GetAsync(locationId, null, null, null, clientId, tenantId, cancellationToken);
        var planIds = plans.Select(plan => plan.FloorPlanId).ToList();

        var zones = await zoneRepository.CountByFloorPlansAsync(planIds, cancellationToken);
        var plots = await plotRepository.CountByKindAsync(planIds, cancellationToken);

        return Results.Ok(new MapSummaryResponse(
            plans.Count,
            zones.Values.Sum(),
            plots.GetValueOrDefault("device"),
            plots.GetValueOrDefault("asset")));
    }

    // ---- Floor plans ------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetFloorPlansAsync(
        string? locationId,
        string? status,
        string? type,
        string? search,
        string? clientId,
        string? tenantId,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        CancellationToken cancellationToken)
    {
        var plans = await repository.GetAsync(locationId, status, type, search, clientId, tenantId, cancellationToken);
        var planIds = plans.Select(plan => plan.FloorPlanId).ToList();

        var zones = await zoneRepository.CountByFloorPlansAsync(planIds, cancellationToken);
        var plots = await plotRepository.CountByFloorPlansAsync(planIds, cancellationToken);

        return Results.Ok(plans.Select(plan => FloorPlanResponse.FromEntity(
            plan,
            zones.GetValueOrDefault(plan.FloorPlanId),
            plots.GetValueOrDefault(plan.FloorPlanId))));
    }

    private static async Task<IResult> GetFloorPlanByIdAsync(
        string floorPlanId,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Viewed, cancellationToken);

        return Results.Ok(await ToResponseAsync(plan, zoneRepository, plotRepository, cancellationToken));
    }

    private static async Task<IResult> CreateFloorPlanAsync(
        CreateFloorPlanRequest request,
        IFloorPlanRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plan = request.ToEntity(currentUserAccessor.GetCurrentUser());
        plan = await repository.CreateAsync(plan, cancellationToken);

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/floor-plans/{plan.FloorPlanId}", FloorPlanResponse.FromEntity(plan, 0, 0));
    }

    private static async Task<IResult> UpdateFloorPlanAsync(
        string floorPlanId,
        UpdateFloorPlanRequest request,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        request.ApplyTo(plan, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(plan, zoneRepository, plotRepository, cancellationToken));
    }

    private static async Task<IResult> DeleteFloorPlanAsync(
        string floorPlanId,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        IMapLayerRepository layerRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        // Soft delete: the record stays for the audit trail, and so do its zones, plots and layer options
        var userName = currentUserAccessor.GetCurrentUser()?.UserName ?? plan.UpdatedBy;
        plan.IsDeleted = true;
        plan.UpdatedBy = userName;
        plan.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(plan.Id, plan, cancellationToken);

        await zoneRepository.SoftDeleteByFloorPlanAsync(plan.FloorPlanId, userName, cancellationToken);
        await plotRepository.SoftDeleteByFloorPlanAsync(plan.FloorPlanId, userName, cancellationToken);
        await layerRepository.SoftDeleteByFloorPlanAsync(plan.FloorPlanId, userName, cancellationToken);

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // Publishes the plan's current version.
    private static async Task<IResult> PublishFloorPlanAsync(
        string floorPlanId,
        MapActionRequest? request,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var version = plan.FindVersion(plan.CurrentVersion);
        if (version is null)
        {
            return Results.Conflict(new { message = "The floor plan has no current version to publish" });
        }

        plan.Publish(version, ActingUser(request?.UpdatedBy, currentUserAccessor));

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(plan, zoneRepository, plotRepository, cancellationToken));
    }

    private static async Task<IResult> ArchiveFloorPlanAsync(
        string floorPlanId,
        MapActionRequest? request,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        plan.Status = MapOptions.Archived;
        plan.UpdatedBy = ActingUser(request?.UpdatedBy, currentUserAccessor);
        plan.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, plan.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(plan, zoneRepository, plotRepository, cancellationToken));
    }

    // ---- Versions ---------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetVersionsAsync(
        string floorPlanId,
        IFloorPlanRepository repository,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(plan.NewestFirst().Select(version => VersionResponse.FromEntity(version, plan.CurrentVersion)));
    }

    // New Draft version; it becomes current only when published.
    private static async Task<IResult> CreateVersionAsync(
        string floorPlanId,
        CreateVersionRequest request,
        IFloorPlanRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var version = request.ToVersion(plan, currentUserAccessor.GetCurrentUser());
        plan.Versions.Add(version);
        plan.UpdatedBy = version.UploadedBy;
        plan.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, $"{plan.Name} {version.Version}", EventAction.Updated, cancellationToken);

        return Results.Created(
            $"{Route}/floor-plans/{plan.FloorPlanId}/versions/{version.Version}",
            VersionResponse.FromEntity(version, plan.CurrentVersion));
    }

    private static async Task<IResult> PublishVersionAsync(
        string floorPlanId,
        string version,
        MapActionRequest? request,
        IFloorPlanRepository repository,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        var match = plan?.FindVersion(version);
        if (plan is null || match is null)
        {
            return Results.NotFound();
        }

        plan.Publish(match, ActingUser(request?.UpdatedBy, currentUserAccessor));

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, $"{plan.Name} {match.Version}", EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(plan, zoneRepository, plotRepository, cancellationToken));
    }

    // Copies an earlier version as a new Draft version.
    private static async Task<IResult> RestoreVersionAsync(
        string floorPlanId,
        string version,
        MapActionRequest? request,
        IFloorPlanRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        var source = plan?.FindVersion(version);
        if (plan is null || source is null)
        {
            return Results.NotFound();
        }

        var userName = ActingUser(request?.UpdatedBy, currentUserAccessor);
        var copy = source.RestoreCopy(plan, userName);
        plan.Versions.Add(copy);
        plan.UpdatedBy = userName;
        plan.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(plan.Id, plan, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(FloorPlanEntity, plan.Id, $"{plan.Name} {copy.Version}", EventAction.Updated, cancellationToken);

        return Results.Created(
            $"{Route}/floor-plans/{plan.FloorPlanId}/versions/{copy.Version}",
            VersionResponse.FromEntity(copy, plan.CurrentVersion));
    }

    // ---- Zones ------------------------------------------------------------------------------------------------------

    private static IResult GetZoneOptions()
    {
        return Results.Ok(new ZoneOptionsResponse(
            MapOptions.ZoneTypes,
            MapOptions.ZoneShapes,
            MapOptions.AccessRules,
            MapOptions.ZoneColors));
    }

    private static async Task<IResult> GetZonesAsync(
        string floorPlanId,
        string? clientId,
        string? tenantId,
        IFloorPlanRepository floorPlanRepository,
        IMapZoneRepository repository,
        CancellationToken cancellationToken)
    {
        if (await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var zones = await repository.GetByFloorPlanAsync(floorPlanId, clientId, tenantId, cancellationToken);

        return Results.Ok(zones.Select(ZoneResponse.FromEntity));
    }

    private static async Task<IResult> CreateZoneAsync(
        string floorPlanId,
        SaveZoneRequest request,
        IFloorPlanRepository floorPlanRepository,
        IMapZoneRepository repository,
        IMapLocationResolver locationResolver,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var zone = request.ToZone(plan, currentUserAccessor.GetCurrentUser());

        if (await repository.CodeExistsAsync(plan.FloorPlanId, zone.Code, cancellationToken))
        {
            return Results.Conflict(new { message = $"Zone code '{zone.Code}' is already used on this floor plan" });
        }

        // Site > building > floor > Master Data zone (same code) of the drawn zone
        zone.Location = await locationResolver.ForZoneAsync(plan, zone.Code, cancellationToken);

        zone = await repository.CreateAsync(zone, cancellationToken);

        await eventLogger.LogAsync(ZoneEntity, zone.Id, zone.Name, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/zones/{zone.ZoneId}", ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> UpdateZoneAsync(
        string zoneId,
        SaveZoneRequest request,
        IMapZoneRepository repository,
        IFloorPlanRepository floorPlanRepository,
        IMapPlotRepository plotRepository,
        IMapLocationResolver locationResolver,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var zone = await repository.GetByZoneIdAsync(zoneId, cancellationToken);
        if (zone is null)
        {
            return Results.NotFound();
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        request.ApplyTo(zone, currentUserAccessor.GetCurrentUser());

        if (await repository.CodeExistsAsync(zone.FloorPlanId, zone.Code, cancellationToken, exceptZoneId: zone.ZoneId))
        {
            return Results.Conflict(new { message = $"Zone code '{zone.Code}' is already used on this floor plan" });
        }

        // The code links the drawn zone to its Master Data zone: refresh the location, and the items plotted in it
        var plan = await floorPlanRepository.GetByFloorPlanIdAsync(zone.FloorPlanId, cancellationToken);
        if (plan is not null)
        {
            zone.Location = await locationResolver.ForZoneAsync(plan, zone.Code, cancellationToken);
        }

        if (!await repository.UpdateAsync(zone.Id, zone, cancellationToken))
        {
            return Results.NotFound();
        }

        if (plan is not null)
        {
            var plots = await plotRepository.GetByFloorPlanAsync(zone.FloorPlanId, null, null, null, cancellationToken);
            foreach (var plot in plots.Where(p => p.ZoneId == zone.ZoneId))
            {
                await locationResolver.ApplyToPlotAsync(plan, plot, cancellationToken);
                await plotRepository.UpdateAsync(plot.Id, plot, cancellationToken);
            }
        }

        await eventLogger.LogAsync(ZoneEntity, zone.Id, zone.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(ZoneResponse.FromEntity(zone));
    }

    private static async Task<IResult> DeleteZoneAsync(
        string zoneId,
        IMapZoneRepository repository,
        IMapPlotRepository plotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var zone = await repository.GetByZoneIdAsync(zoneId, cancellationToken);
        if (zone is null)
        {
            return Results.NotFound();
        }

        // Soft delete: the record stays for the audit trail
        var userName = currentUserAccessor.GetCurrentUser()?.UserName ?? zone.UpdatedBy;
        zone.IsDeleted = true;
        zone.UpdatedBy = userName;
        zone.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(zone.Id, zone, cancellationToken);

        // Items placed in the zone stay on the plan, just no longer in a zone
        await plotRepository.ClearZoneAsync(zone.ZoneId, userName, cancellationToken);

        await eventLogger.LogAsync(ZoneEntity, zone.Id, zone.Name, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // ---- Plots ------------------------------------------------------------------------------------------------------

    private static async Task<IResult> GetPlotsAsync(
        string floorPlanId,
        string? kind,
        string? clientId,
        string? tenantId,
        IFloorPlanRepository floorPlanRepository,
        IMapPlotRepository repository,
        CancellationToken cancellationToken)
    {
        var validationErrors = MapValidationExtensions.ValidateKind(kind, required: false);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var plots = await repository.GetByFloorPlanAsync(
            floorPlanId,
            MapMappings.Canonical(kind, MapOptions.PlotKinds),
            clientId,
            tenantId,
            cancellationToken);

        return Results.Ok(plots.Select(PlotResponse.FromEntity));
    }

    // Upsert: places the item on the plan or moves it. A removed item is placed again on its old row.
    private static async Task<IResult> SavePlotAsync(
        string floorPlanId,
        string kind,
        string itemId,
        SavePlotRequest request,
        IFloorPlanRepository floorPlanRepository,
        IMapPlotRepository repository,
        IMapLocationResolver locationResolver,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(kind);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plan = await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var plotKind = MapMappings.Canonical(kind, MapOptions.PlotKinds)!;
        var key = itemId.Trim();

        var plot = await repository.GetByKeyAsync(plan.FloorPlanId, plotKind, key, includeDeleted: true, cancellationToken);
        if (plot is null)
        {
            plot = new MapPlot
            {
                FloorPlanId = plan.FloorPlanId,
                Kind = plotKind,
                ItemId = key,
                CreatedBy = MapMappings.FirstNonEmpty(request.UpdatedBy, currentUser?.UserName),
                CreatedAt = DateTime.UtcNow
            };
            request.ApplyTo(plot, plan, currentUser);
            // Site > building > floor > zone, drawn zone name / code, device / asset ids
            await locationResolver.ApplyToPlotAsync(plan, plot, cancellationToken);
            plot = await repository.CreateAsync(plot, cancellationToken);

            await eventLogger.LogAsync(PlotEntity, plot.Id, plot.Name ?? plot.ItemId, EventAction.Created, cancellationToken);

            return Results.Ok(PlotResponse.FromEntity(plot));
        }

        request.ApplyTo(plot, plan, currentUser);
        await locationResolver.ApplyToPlotAsync(plan, plot, cancellationToken);

        if (!await repository.UpdateAsync(plot.Id, plot, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(PlotEntity, plot.Id, plot.Name ?? plot.ItemId, EventAction.Updated, cancellationToken);

        return Results.Ok(PlotResponse.FromEntity(plot));
    }

    private static async Task<IResult> DeletePlotAsync(
        string floorPlanId,
        string kind,
        string itemId,
        IFloorPlanRepository floorPlanRepository,
        IMapPlotRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = MapValidationExtensions.ValidateKind(kind, required: true);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var plot = await repository.GetByKeyAsync(
            floorPlanId,
            MapMappings.Canonical(kind, MapOptions.PlotKinds)!,
            itemId.Trim(),
            includeDeleted: false,
            cancellationToken);
        if (plot is null)
        {
            return Results.NotFound();
        }

        // Soft delete: the record stays for the audit trail
        plot.IsDeleted = true;
        plot.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName ?? plot.UpdatedBy;
        plot.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(plot.Id, plot, cancellationToken);

        await eventLogger.LogAsync(PlotEntity, plot.Id, plot.Name ?? plot.ItemId, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // ---- Layers -----------------------------------------------------------------------------------------------------

    // Always the eight layers in their fixed order; stored options win over the defaults.
    private static async Task<IResult> GetLayersAsync(
        string floorPlanId,
        IFloorPlanRepository floorPlanRepository,
        IMapLayerRepository repository,
        CancellationToken cancellationToken)
    {
        var plan = await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var stored = (await repository.GetByFloorPlanAsync(plan.FloorPlanId, cancellationToken))
            .GroupBy(layer => layer.LayerKey)
            .ToDictionary(group => group.Key, group => group.First());

        return Results.Ok(MapOptions.Layers.Select(definition => LayerResponse.FromEntity(
            definition,
            stored.GetValueOrDefault(definition.Key) ?? MapMappings.DefaultLayer(definition, plan))));
    }

    private static async Task<IResult> SaveLayerAsync(
        string floorPlanId,
        string layerKey,
        SaveLayerRequest request,
        IFloorPlanRepository floorPlanRepository,
        IMapLayerRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate(layerKey);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plan = await floorPlanRepository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var definition = MapOptions.Layers.First(layer => string.Equals(layer.Key, layerKey.Trim(), StringComparison.OrdinalIgnoreCase));

        var layer = await repository.GetByKeyAsync(plan.FloorPlanId, definition.Key, cancellationToken);
        if (layer is null)
        {
            layer = MapMappings.DefaultLayer(definition, plan);
            layer.CreatedBy = MapMappings.FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
            layer.CreatedAt = DateTime.UtcNow;
            request.ApplyTo(layer, currentUser);
            layer = await repository.CreateAsync(layer, cancellationToken);

            await eventLogger.LogAsync(LayerEntity, layer.Id, $"{plan.Name} {definition.Name}", EventAction.Created, cancellationToken);

            return Results.Ok(LayerResponse.FromEntity(definition, layer));
        }

        if (layer.IsDeleted)
        {
            // A soft-deleted row starts again from the defaults
            var fresh = MapMappings.DefaultLayer(definition, plan);
            fresh.Id = layer.Id;
            fresh.CreatedBy = layer.CreatedBy;
            fresh.CreatedAt = layer.CreatedAt;
            layer = fresh;
        }

        request.ApplyTo(layer, currentUser);

        if (!await repository.UpdateAsync(layer.Id, layer, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(LayerEntity, layer.Id, $"{plan.Name} {definition.Name}", EventAction.Updated, cancellationToken);

        return Results.Ok(LayerResponse.FromEntity(definition, layer));
    }

    private static async Task<IResult> GetLayerSetsAsync(
        string? clientId,
        string? tenantId,
        IMapLayerSetRepository repository,
        CancellationToken cancellationToken)
    {
        var sets = await repository.GetAsync(clientId, tenantId, cancellationToken);

        return Results.Ok(sets.Select(LayerSetResponse.FromEntity));
    }

    private static async Task<IResult> CreateLayerSetAsync(
        CreateLayerSetRequest request,
        IMapLayerSetRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var set = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.NameExistsAsync(set.Name, set.ClientId, set.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Layer set '{set.Name}' already exists" });
        }

        set = await repository.CreateAsync(set, cancellationToken);

        await eventLogger.LogAsync(LayerSetEntity, set.Id, set.Name, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/layer-sets/{set.LayerSetId}", LayerSetResponse.FromEntity(set));
    }

    private static async Task<IResult> DeleteLayerSetAsync(
        string layerSetId,
        IMapLayerSetRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var set = await repository.GetByLayerSetIdAsync(layerSetId, cancellationToken);
        if (set is null)
        {
            return Results.NotFound();
        }

        // Soft delete: the record stays for the audit trail
        set.IsDeleted = true;
        set.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName ?? set.UpdatedBy;
        set.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(set.Id, set, cancellationToken);

        await eventLogger.LogAsync(LayerSetEntity, set.Id, set.Name, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    // ---- Settings ---------------------------------------------------------------------------------------------------

    // The tenant's saved settings, or the defaults when none were saved yet.
    private static async Task<IResult> GetSettingsAsync(
        string? clientId,
        string? tenantId,
        IMapSettingsRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        var settings = await repository.GetByScopeAsync(
            MapMappings.FirstNonEmpty(clientId, currentUser?.ClientId),
            MapMappings.FirstNonEmpty(tenantId, currentUser?.TenantId),
            cancellationToken);

        return Results.Ok(MapSettingsResponse.FromEntity(settings ?? new MapSettings()));
    }

    // Creates the tenant's settings on the first save and updates them on every save after that.
    private static async Task<IResult> SaveSettingsAsync(
        SaveMapSettingsRequest request,
        IMapSettingsRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var clientId = MapMappings.FirstNonEmpty(request.ClientId, currentUser?.ClientId);
        var tenantId = MapMappings.FirstNonEmpty(request.TenantId, currentUser?.TenantId);
        var userName = MapMappings.FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);

        var settings = await repository.GetByScopeAsync(clientId, tenantId, cancellationToken);
        if (settings is null)
        {
            settings = new MapSettings
            {
                // Generated on the server so the settings get a stable id independent of the Mongo _id.
                SettingsId = Guid.NewGuid().ToString(),
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                ClientId = clientId,
                TenantId = tenantId,
                IsDeleted = false
            };
            request.ApplyTo(settings);

            settings = await repository.CreateAsync(settings, cancellationToken);

            await eventLogger.LogAsync(SettingsEntity, settings.Id, "Map Settings", EventAction.Created, cancellationToken);

            return Results.Ok(MapSettingsResponse.FromEntity(settings));
        }

        request.ApplyTo(settings);
        settings.UpdatedBy = userName;
        settings.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(settings.Id, settings, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(SettingsEntity, settings.Id, "Map Settings", EventAction.Updated, cancellationToken);

        return Results.Ok(MapSettingsResponse.FromEntity(settings));
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------

    private static async Task<FloorPlanResponse> ToResponseAsync(
        FloorPlan plan,
        IMapZoneRepository zoneRepository,
        IMapPlotRepository plotRepository,
        CancellationToken cancellationToken)
    {
        string[] planIds = [plan.FloorPlanId];
        var zones = await zoneRepository.CountByFloorPlansAsync(planIds, cancellationToken);
        var plots = await plotRepository.CountByFloorPlansAsync(planIds, cancellationToken);

        return FloorPlanResponse.FromEntity(plan, zones.GetValueOrDefault(plan.FloorPlanId), plots.GetValueOrDefault(plan.FloorPlanId));
    }

    /// <summary>Who performed an action: the name in the body, else the signed-in user.</summary>
    private static string? ActingUser(string? requested, ICurrentUserAccessor currentUserAccessor)
    {
        return MapMappings.FirstNonEmpty(requested, currentUserAccessor.GetCurrentUser()?.UserName);
    }
}
