using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Sensor Management: sensors and their measurement points, addressed by sensor id (e.g. SEN-T-80214).</summary>
public static class SensorEndpoints
{
    private const string EntityType = "Sensor";

    public static RouteGroupBuilder MapSensorEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapGet("", GetSensorsAsync).WithName("GetSensors");
        // Literal segments take precedence over /{sensorId}, so these are not read as sensor ids.
        group.MapGet("/types", () => Results.Ok(SensorTypeOption.All)).WithName("GetSensorTypes");
        group.MapGet("/sampling-intervals", () => Results.Ok(TelemetrySamplingInterval.Seconds)).WithName("GetSensorSamplingIntervals");
        group.MapGet("/statuses", () => Results.Ok(SensorStatus.All)).WithName("GetSensorStatuses");
        group.MapGet("/measurement-points", GetMeasurementPointsAsync).WithName("GetSensorMeasurementPoints");
        group.MapGet("/devices", GetDeviceOptionsAsync).WithName("GetSensorDeviceOptions");
        group.MapGet("/devices/{deviceId}/channels", GetDeviceChannelsAsync).WithName("GetSensorDeviceChannels");
        group.MapGet("/form-options", GetFormOptions).WithName("GetSensorFormOptions");
        group.MapGet("/{sensorId}", GetSensorAsync).WithName("GetSensorById");
        group.MapPost("", CreateSensorAsync).WithName("CreateSensor");
        group.MapPut("/{sensorId}", UpdateSensorAsync).WithName("UpdateSensor");
        group.MapDelete("/{sensorId}", DeleteSensorAsync).WithName("DeleteSensor");

        return group;
    }

    private static async Task<IResult> GetSensorsAsync(
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var sensors = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(sensors).Select(SensorResponse.FromEntity).ToList());
    }

    /// <summary>Measurement point suggestions: the common ones for the sensor type, then any already used on sensors of that type.</summary>
    private static async Task<IResult> GetMeasurementPointsAsync(
        string? sensorType,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var hasType = !string.IsNullOrWhiteSpace(sensorType);
        var defaults = hasType
            ? SensorMeasurementPoint.BySensorType.GetValueOrDefault(sensorType!.Trim()) ?? []
            : SensorMeasurementPoint.BySensorType.Values.SelectMany(points => points);

        var sensors = await repository.GetAllAsync(cancellationToken);
        var used = TrackingRecords.Listed(sensors)
            .Where(sensor => !hasType || string.Equals(sensor.SensorType, sensorType!.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(sensor => sensor.MeasurementPoint)
            .Where(point => !string.IsNullOrWhiteSpace(point))
            .Select(point => point!.Trim())
            .Order(StringComparer.OrdinalIgnoreCase);

        return Results.Ok(defaults.Concat(used).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Linked device dropdown: IoT devices matching the search (id, name, serial or asset), each with the
    /// channels already used by sensors and the next free channel.
    /// </summary>
    private static async Task<IResult> GetDeviceOptionsAsync(
        string? search,
        int? limit,
        IIoTDeviceRepository devices,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var matches = await devices.SearchAsync(search, Math.Clamp(limit ?? 50, 1, 100), cancellationToken);
        var options = new List<SensorDeviceOptionResponse>();

        foreach (var device in matches.Where(device => !string.Equals(device.DeviceType, IoTDeviceType.Gateway, StringComparison.OrdinalIgnoreCase)))
        {
            var used = await UsedChannelsAsync(device.DeviceId, null, repository, cancellationToken);
            options.Add(new SensorDeviceOptionResponse(
                device.DeviceId, device.DeviceName, device.DeviceType, device.Model,
                device.SiteId, device.SiteName,
                device.AssetRegistrationId, device.AssetCode, device.AssetName,
                used, NextChannel(used)));
        }

        return Results.Ok(options);
    }

    private static async Task<IResult> GetDeviceChannelsAsync(
        string deviceId,
        IIoTDeviceRepository devices,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var device = await devices.GetByDeviceIdAsync(deviceId.Trim(), cancellationToken);
        if (device is null)
        {
            return Results.NotFound(new { message = $"IoT device '{deviceId}' was not found." });
        }

        var used = await UsedChannelsAsync(device.DeviceId, null, repository, cancellationToken);
        return Results.Ok(new SensorChannelsResponse(device.DeviceId, used, NextChannel(used)));
    }

    private static IResult GetFormOptions()
    {
        return Results.Ok(new SensorFormOptionsResponse(
            SensorTypeOption.All,
            TelemetrySamplingInterval.Seconds.Select(seconds => new SamplingIntervalOptionResponse(seconds, IntervalLabel(seconds))).ToList(),
            SensorMeasurementPoint.BySensorType,
            SensorStatus.All));
    }

    private static async Task<IResult> GetSensorAsync(
        string sensorId,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var sensor = await repository.GetBySensorIdAsync(sensorId.Trim(), cancellationToken);
        return sensor is null
            ? NotFound(sensorId)
            : Results.Ok(SensorResponse.FromEntity(sensor));
    }

    private static async Task<IResult> CreateSensorAsync(
        CreateSensorRequest request,
        ISensorRepository repository,
        IIoTDeviceRepository devices,
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

        if (await ResolveLinksAsync(sensor, null, repository, devices, cancellationToken) is { } linkError)
        {
            return linkError;
        }

        await repository.CreateAsync(sensor, cancellationToken);
        await eventLogger.LogAsync(EntityType, sensor.SensorId, $"{sensor.SensorId} - {sensor.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/sensors/{sensor.SensorId}", SensorResponse.FromEntity(sensor));
    }

    /// <summary>Takes the same body as create; its SensorId must match the one in the URL.</summary>
    private static async Task<IResult> UpdateSensorAsync(
        string sensorId,
        CreateSensorRequest request,
        ISensorRepository repository,
        IIoTDeviceRepository devices,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (TrackingRecords.KeyChanged("SensorId", sensorId, request.ResolvedSensorId()) is { } keyChanged)
        {
            return keyChanged;
        }

        var existing = await repository.GetBySensorIdAsync(sensorId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(sensorId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var sensor = request.ToEntity(currentUser).Replacing(existing, currentUser);
        sensor.SensorId = existing.SensorId;

        if (await ResolveLinksAsync(sensor, existing.Id, repository, devices, cancellationToken) is { } linkError)
        {
            return linkError;
        }

        if (!await repository.UpdateAsync(existing.Id, sensor, cancellationToken))
        {
            return NotFound(sensorId);
        }

        await eventLogger.LogAsync(EntityType, sensor.SensorId, $"{sensor.SensorId} - {sensor.Name}", EventAction.Updated, cancellationToken);

        return Results.Ok(SensorResponse.FromEntity(sensor));
    }

    private static async Task<IResult> DeleteSensorAsync(
        string sensorId,
        ISensorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var sensor = await repository.GetBySensorIdAsync(sensorId.Trim(), cancellationToken);
        if (sensor is null || !await repository.DeleteAsync(sensor.Id, cancellationToken))
        {
            return NotFound(sensorId);
        }

        await eventLogger.LogAsync(EntityType, sensor.SensorId, $"{sensor.SensorId} - {sensor.Name}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// Checks the serial number is free, the linked device exists and the channel is free on it,
    /// and fills device model, site and asset from the device when the request left them out.
    /// Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveLinksAsync(
        Sensor sensor,
        string? exceptId,
        ISensorRepository repository,
        IIoTDeviceRepository devices,
        CancellationToken cancellationToken)
    {
        if (await repository.SerialNumberExistsAsync(sensor.SerialNumber!, exceptId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Serial number '{sensor.SerialNumber}' is already registered to another sensor." });
        }

        var device = await devices.GetByDeviceIdAsync(sensor.DeviceId!, cancellationToken);
        if (device is null)
        {
            return Results.BadRequest(new { message = $"Linked device '{sensor.DeviceId}' was not found." });
        }

        sensor.DeviceId = device.DeviceId;
        sensor.DeviceModel ??= device.Model;

        if (sensor.SiteId is null && sensor.SiteName is null)
        {
            sensor.SiteId = device.SiteId;
            sensor.SiteName = device.SiteName;
        }

        if (sensor.Channel is { } channel)
        {
            var used = await UsedChannelsAsync(device.DeviceId, exceptId, repository, cancellationToken);
            if (used.Contains(channel))
            {
                return Results.Conflict(new { message = $"Channel {channel} on device '{device.DeviceId}' is already used by another sensor." });
            }
        }

        return null;
    }

    private static async Task<IReadOnlyList<int>> UsedChannelsAsync(
        string deviceId,
        string? exceptId,
        ISensorRepository repository,
        CancellationToken cancellationToken)
    {
        var sensors = await repository.GetByDeviceAsync(deviceId, cancellationToken);
        return sensors
            .Where(sensor => sensor.Id != exceptId && sensor.Channel is not null)
            .Select(sensor => sensor.Channel!.Value)
            .Distinct()
            .Order()
            .ToList();
    }

    private static int NextChannel(IReadOnlyList<int> used)
    {
        var next = 1;
        while (used.Contains(next))
        {
            next++;
        }

        return next;
    }

    private static string IntervalLabel(int seconds)
    {
        return seconds switch
        {
            < 60 => $"{seconds} seconds",
            60 => "60 seconds",
            < 3600 => $"{seconds / 60} minutes",
            3600 => "1 hour",
            _ => $"{seconds / 3600} hours"
        };
    }

    private static IResult NotFound(string sensorId)
    {
        return Results.NotFound(new { message = $"Sensor '{sensorId}' was not found." });
    }
}
