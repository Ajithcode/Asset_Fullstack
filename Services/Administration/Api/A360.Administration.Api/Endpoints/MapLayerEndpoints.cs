using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>Layers (Administration > Organization &amp; Locations > Maps &amp; Floor Plans): toggleable layers of a floor plan.</summary>
public static class MapLayerEndpoints
{
    private const string EntityType = "MapLayer";
    private const string Route = "/api/layers";

    public static RouteGroupBuilder MapMapLayerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("Layers");

        group.MapGet("/options", () => Results.Ok(MapLayerOptionsResponse.Instance)).WithName("GetLayerOptions");
        group.MapGet("/{layerId}", GetLayerByIdAsync).WithName("GetLayerById");
        group.MapPost("", CreateLayerAsync).WithName("CreateLayer");

        return group;
    }

    private static async Task<IResult> GetLayerByIdAsync(
        string layerId,
        IMapLayerRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var layer = await repository.GetByLayerIdAsync(layerId, cancellationToken);
        if (layer is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, layer.Id, layer.LayerName, EventAction.Viewed, cancellationToken);

        return Results.Ok(MapLayerResponse.FromEntity(layer));
    }

    private static async Task<IResult> CreateLayerAsync(
        CreateMapLayerRequest request,
        IMapLayerRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var layer = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.LayerNameExistsAsync(layer.LayerName, layer.FloorPlanId, layer.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Layer '{layer.LayerName}' already exists on this floor plan" });
        }

        layer = await repository.CreateAsync(layer, cancellationToken);

        await eventLogger.LogAsync(EntityType, layer.Id, layer.LayerName, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/{layer.LayerId}", MapLayerResponse.FromEntity(layer));
    }
}
