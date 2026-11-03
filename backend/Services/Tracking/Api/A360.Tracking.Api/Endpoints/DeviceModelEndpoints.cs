using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Type & Model: model specifications, addressed by model number (e.g. EM300-TH).</summary>
public static class DeviceModelEndpoints
{
    private const string EntityType = "DeviceModel";

    public static RouteGroupBuilder MapDeviceModelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-models").WithTags("Device Models");

        group.MapGet("", GetDeviceModelsAsync).WithName("GetDeviceModels");
        group.MapGet("/{modelNumber}", GetDeviceModelAsync).WithName("GetDeviceModelByModelNumber");
        group.MapPost("", CreateDeviceModelAsync).WithName("CreateDeviceModel");
        group.MapPut("/{modelNumber}", UpdateDeviceModelAsync).WithName("UpdateDeviceModel");
        group.MapDelete("/{modelNumber}", DeleteDeviceModelAsync).WithName("DeleteDeviceModel");

        return group;
    }

    private static async Task<IResult> GetDeviceModelsAsync(
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(models).Select(DeviceModelResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetDeviceModelAsync(
        string modelNumber,
        IDeviceModelRepository repository,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        return model is null
            ? NotFound(modelNumber)
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

    /// <summary>Takes the same body as create; its ModelNumber must match the one in the URL.</summary>
    private static async Task<IResult> UpdateDeviceModelAsync(
        string modelNumber,
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

        if (TrackingRecords.KeyChanged("ModelNumber", modelNumber, request.ModelNumber) is { } keyChanged)
        {
            return keyChanged;
        }

        var existing = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(modelNumber);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var model = request.ToEntity(currentUser).Replacing(existing, currentUser);
        model.ModelNumber = existing.ModelNumber;

        if (!await repository.UpdateAsync(existing.Id, model, cancellationToken))
        {
            return NotFound(modelNumber);
        }

        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceModelResponse.FromEntity(model));
    }

    private static async Task<IResult> DeleteDeviceModelAsync(
        string modelNumber,
        IDeviceModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByModelNumberAsync(modelNumber.Trim(), cancellationToken);
        if (model is null || !await repository.DeleteAsync(model.Id, cancellationToken))
        {
            return NotFound(modelNumber);
        }

        await eventLogger.LogAsync(EntityType, model.ModelNumber, $"{model.Manufacturer} {model.ModelNumber}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string modelNumber)
    {
        return Results.NotFound(new { message = $"Device model '{modelNumber}' was not found." });
    }
}
