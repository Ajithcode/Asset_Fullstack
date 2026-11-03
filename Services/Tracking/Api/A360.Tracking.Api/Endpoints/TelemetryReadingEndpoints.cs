using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>
/// Telemetry readings (the "Telemetry Data" series) and the IoT Events Timeline, which counts per day:
/// telemetry readings, location updates, geofence events and alerts.
/// </summary>
public static class TelemetryReadingEndpoints
{
    private const string SequenceName = "TelemetryReading";
    private const string ReadingIdPrefix = "TR";
    private const int DefaultLimit = 500;
    private const int MaxLimit = 5000;

    private static readonly string[] LocationUpdateTypes = [LocationEventType.EnteredZone, LocationEventType.DepartedZone];
    private static readonly string[] GeofenceEventTypes = [LocationEventType.GeofenceAlert, LocationEventType.DwellExceeded];

    public static RouteGroupBuilder MapTelemetryReadingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/telemetry-readings").WithTags("Telemetry Readings");

        group.MapGet("", GetReadingsAsync).WithName("GetTelemetryReadings");
        group.MapGet("/{readingId}", GetReadingAsync).WithName("GetTelemetryReadingById");
        group.MapPost("", CreateReadingAsync).WithName("CreateTelemetryReading");
        group.MapDelete("/{readingId}", DeleteReadingAsync).WithName("DeleteTelemetryReading");

        return group;
    }

    public static RouteGroupBuilder MapIoTEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/iot-events").WithTags("IoT Events");

        group.MapGet("/timeline", GetTimelineAsync).WithName("GetIoTEventsTimeline");

        return group;
    }

    /// <summary>Readings of the last <paramref name="days"/> days (default 7), newest first, at most <paramref name="limit"/> (default 500).</summary>
    private static async Task<IResult> GetReadingsAsync(
        ITelemetryReadingRepository repository,
        CancellationToken cancellationToken,
        int? days = null,
        string? deviceId = null,
        string? sensorId = null,
        int? limit = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        if (limit is < 1 or > MaxLimit)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = [$"limit must be between 1 and {MaxLimit}"] });
        }

        var period = DashboardPeriod.LastDays(days);
        var readings = await repository.QueryAsync(period.FromUtc, period.ToUtc, deviceId, sensorId, limit ?? DefaultLimit, cancellationToken);
        return Results.Ok(readings.Select(TelemetryReadingResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetReadingAsync(
        string readingId,
        ITelemetryReadingRepository repository,
        CancellationToken cancellationToken)
    {
        var reading = await repository.GetByReadingIdAsync(readingId.Trim(), cancellationToken);
        return reading is null ? NotFound(readingId) : Results.Ok(TelemetryReadingResponse.FromEntity(reading));
    }

    /// <summary>Readings are high-volume, so they are not written to the event log.</summary>
    private static async Task<IResult> CreateReadingAsync(
        CreateTelemetryReadingRequest request,
        ITelemetryReadingRepository repository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var reading = request.ToEntity($"{ReadingIdPrefix}{nextSequence:D6}", currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(reading, cancellationToken);
        return Results.Created($"/api/telemetry-readings/{reading.ReadingId}", TelemetryReadingResponse.FromEntity(reading));
    }

    private static async Task<IResult> DeleteReadingAsync(
        string readingId,
        ITelemetryReadingRepository repository,
        CancellationToken cancellationToken)
    {
        var reading = await repository.GetByReadingIdAsync(readingId.Trim(), cancellationToken);
        if (reading is null || !await repository.DeleteAsync(reading.Id, cancellationToken))
        {
            return NotFound(readingId);
        }

        return Results.NoContent();
    }

    /// <summary>One point per day of the last <paramref name="days"/> days (default 7; the dashboard uses 7 / 30 / 90).</summary>
    private static async Task<IResult> GetTimelineAsync(
        ITelemetryReadingRepository telemetryRepository,
        ILocationEventRepository locationEventRepository,
        IIoTAlertRepository alertRepository,
        CancellationToken cancellationToken,
        int? days = null)
    {
        if (DashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = DashboardPeriod.LastDays(days);
        var telemetry = ByDay(await telemetryRepository.CountPerDayAsync(period.FromUtc, period.ToUtc, cancellationToken));
        var alerts = ByDay(await alertRepository.CountPerDayAsync(period.FromUtc, period.ToUtc, cancellationToken));
        var locationEvents = await locationEventRepository.CountPerDayByTypeAsync(period.FromUtc, period.ToUtc, cancellationToken);
        var locationUpdates = ByDay(locationEvents.Where(row => LocationUpdateTypes.Contains(row.Key)));
        var geofenceEvents = ByDay(locationEvents.Where(row => GeofenceEventTypes.Contains(row.Key)));

        var points = period.EachDay()
            .Select(day => new IoTEventsTimelinePoint(
                day,
                telemetry.GetValueOrDefault(day),
                locationUpdates.GetValueOrDefault(day),
                geofenceEvents.GetValueOrDefault(day),
                alerts.GetValueOrDefault(day)))
            .ToList();

        var totals = new IoTEventsTimelinePoint(
            "total",
            points.Sum(point => point.TelemetryData),
            points.Sum(point => point.LocationUpdates),
            points.Sum(point => point.GeofenceEvents),
            points.Sum(point => point.Alerts));

        return Results.Ok(new IoTEventsTimelineResponse(period.Days, period.FromUtc, period.ToUtc, totals, points));
    }

    private static Dictionary<string, int> ByDay(IEnumerable<DailyCount> counts)
    {
        return counts.GroupBy(row => row.Date).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
    }

    private static IResult NotFound(string readingId)
    {
        return Results.NotFound(new { message = $"Telemetry reading '{readingId}' was not found." });
    }
}
