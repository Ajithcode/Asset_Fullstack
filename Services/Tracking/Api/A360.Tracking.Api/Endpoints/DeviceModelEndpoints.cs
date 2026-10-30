using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Type & Model: model specifications, addressed by model number (e.g. EM300-TH).</summary>
public static class DeviceModelEndpoints
{
    private const string EntityType = "DeviceModel";

    public static RouteGroupBuilder MapDeviceModelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-models").WithTags("Device Models");

        // Literal segments take precedence over /{modelNumber}, so these are not read as model numbers.
        group.MapGet("/device-types", GetDeviceTypesAsync).WithName("GetDeviceModelTypes");
        group.MapGet("/connectivities", () => Results.Ok(DeviceModelConnectivity.All)).WithName("GetDeviceModelConnectivities");
        group.MapGet("/power-sources", () => Results.Ok(DeviceModelPowerSource.All)).WithName("GetDeviceModelPowerSources");
        group.MapGet("/measurements", () => Results.Ok(DeviceMeasurement.All)).WithName("GetDeviceModelMeasurements");
        group.MapGet("/configurable-attributes", () => Results.Ok(DeviceModelAttribute.All)).WithName("GetDeviceModelConfigurableAttributes");
        group.MapGet("/statuses", () => Results.Ok(DeviceModelStatus.All)).WithName("GetDeviceModelStatuses");
        group.MapGet("/{modelNumber}", GetDeviceModelAsync).WithName("GetDeviceModelByModelNumber");
        group.MapPost("", CreateDeviceModelAsync).WithName("CreateDeviceModel");

        return group;
    }

    /// <summary>The standard device types plus any other type already used by a model, each with its model count.</summary>
    private static async Task<IResult> GetDeviceTypesAsync(
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var counts = await repository.CountByDeviceTypeAsync(cancellationToken);
        var usedTypes = counts.Keys.Where(type => !DeviceModelType.All.Contains(type, StringComparer.OrdinalIgnoreCase));

        return Results.Ok(DeviceModelType.All
            .Concat(usedTypes.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            .Select(type => new DeviceTypeOptionResponse(
                type,
                counts.Where(count => string.Equals(count.Key, type, StringComparison.OrdinalIgnoreCase)).Sum(count => count.Value))));
    }

    private static async Task<IResult> GetDeviceModelAsync(
        string modelNumber,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        return model is null
            ? Results.NotFound(new { message = $"Device model '{modelNumber}' was not found." })
            : Results.Ok(DeviceModelResponse.FromEntity(model));
    }

    private static async Task<IResult> CreateDeviceModelAsync(
        CreateDeviceModelRequest request,
        IDeviceModelRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.ModelNumberExistsAsync(model.ModelNumber, cancellationToken))
        {
            return Results.Conflict(new { message = $"Device model '{model.ModelNumber}' is already registered." });
        }

        await repository.CreateAsync(model, cancellationToken);
        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-models/{model.ModelNumber}", DeviceModelResponse.FromEntity(model));
    }
}
