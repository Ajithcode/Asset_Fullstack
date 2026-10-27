using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Sensor Management: sensors and their measurement points, addressed by sensor id (e.g. SEN-T-80214).</summary>
public static class SensorEndpoints
{
    private const string EntityType = "Sensor";

    public static RouteGroupBuilder MapSensorEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapGet("/{sensorId}", GetSensorAsync).WithName("GetSensorById");
        group.MapPost("", CreateSensorAsync).WithName("CreateSensor");

        return group;
    }

    private static async Task<IResult> GetSensorAsync(
        string sensorId,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var sensor = await repository.GetBySensorIdAsync(sensorId.Trim(), cancellationToken);
        return sensor is null
            ? Results.NotFound(new { message = $"Sensor '{sensorId}' was not found." })
            : Results.Ok(SensorResponse.FromEntity(sensor));
    }

    private static async Task<IResult> CreateSensorAsync(
        CreateSensorRequest request,
        ISensorRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var sensor = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.SensorIdExistsAsync(sensor.SensorId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sensor '{sensor.SensorId}' is already registered." });
        }

        await repository.CreateAsync(sensor, cancellationToken);
        await eventLogger.LogAsync(EntityType, sensor.SensorId, $"{sensor.SensorId} - {sensor.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/sensors/{sensor.SensorId}", SensorResponse.FromEntity(sensor));
    }
}
