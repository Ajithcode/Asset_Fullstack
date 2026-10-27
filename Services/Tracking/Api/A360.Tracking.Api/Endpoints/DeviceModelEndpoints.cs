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

        group.MapGet("/{modelNumber}", GetDeviceModelAsync).WithName("GetDeviceModelByModelNumber");
        group.MapPost("", CreateDeviceModelAsync).WithName("CreateDeviceModel");

        return group;
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
