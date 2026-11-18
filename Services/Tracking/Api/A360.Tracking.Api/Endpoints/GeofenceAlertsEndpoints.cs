using A360.Tracking.Api.Alerts;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Geofence &amp; Alerts page (read-only): KPIs, indoor + outdoor zone list with live figures, paged alert history.</summary>
public static class GeofenceAlertsEndpoints
{
    public static RouteGroupBuilder MapGeofenceAlertsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/geofence-alerts").WithTags("Geofence & Alerts");

        group.MapGet("/summary", GetSummaryAsync).WithName("GetGeofenceAlertsSummary");
        group.MapGet("/events", GetEventsAsync).WithName("GetGeofenceAlertEvents");

        return group;
    }

    /// <param name="todayFrom">Start of "today" in the viewer's time zone (UTC instant); UTC midnight when omitted.</param>
    private static async Task<IResult> GetSummaryAsync(GeofenceAlertsService service, CancellationToken cancellationToken, DateTime? todayFrom = null)
    {
        return Results.Ok(await service.GetSummaryAsync(todayFrom, cancellationToken));
    }

    /// <param name="targetId">Outdoor geofence id or "zone:{id}".</param>
    /// <param name="status">Open, Acknowledged, Assigned, In Progress, Resolved, Closed, or Unresolved.</param>
    /// <param name="severity">Informational, Low, Medium, High or Critical.</param>
    private static async Task<IResult> GetEventsAsync(
        GeofenceAlertsService service,
        CancellationToken cancellationToken,
        int? page = 1,
        int? pageSize = 10,
        DateTime? from = null,
        DateTime? to = null,
        string? targetId = null,
        string? eventType = null,
        string? status = null,
        string? severity = null,
        string? search = null)
    {
        return Results.Ok(await service.GetEventsAsync(new GeofenceEventQuery(page, pageSize, from, to, targetId, eventType, status, severity, search), cancellationToken));
    }
}
