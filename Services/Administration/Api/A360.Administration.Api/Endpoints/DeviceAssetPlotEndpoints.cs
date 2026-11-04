using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>Device &amp; Asset Plotting (Administration > Organization &amp; Locations > Maps &amp; Floor Plans): assets and devices plotted on floor plans.</summary>
public static class DeviceAssetPlotEndpoints
{
    private const string EntityType = "DeviceAssetPlot";
    private const string Route = "/api/device-asset-plotting";

    public static RouteGroupBuilder MapDeviceAssetPlotEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("Device & Asset Plotting");

        group.MapGet("/{plotId}", GetDeviceAssetPlotByIdAsync).WithName("GetDeviceAssetPlotById");
        group.MapPost("", CreateDeviceAssetPlotAsync).WithName("CreateDeviceAssetPlot");

        return group;
    }

    private static async Task<IResult> GetDeviceAssetPlotByIdAsync(
        string plotId,
        IDeviceAssetPlotRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plot = await repository.GetByPlotIdAsync(plotId, cancellationToken);
        if (plot is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, plot.Id, plot.ItemName, EventAction.Viewed, cancellationToken);

        return Results.Ok(DeviceAssetPlotResponse.FromEntity(plot));
    }

    private static async Task<IResult> CreateDeviceAssetPlotAsync(
        CreateDeviceAssetPlotRequest request,
        IDeviceAssetPlotRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plot = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (plot.ItemId is not null
            && await repository.ItemPlottedAsync(plot.ItemType, plot.ItemId, plot.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"{plot.ItemType} '{plot.ItemId}' is already plotted" });
        }

        plot = await repository.CreateAsync(plot, cancellationToken);

        await eventLogger.LogAsync(EntityType, plot.Id, plot.ItemName, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/{plot.PlotId}", DeviceAssetPlotResponse.FromEntity(plot));
    }
}
