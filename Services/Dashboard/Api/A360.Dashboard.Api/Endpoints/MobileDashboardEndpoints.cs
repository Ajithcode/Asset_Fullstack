using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Api.Mobile;

namespace A360.Dashboard.Api.Endpoints;

/// <summary>
/// Mobile app Home &amp; Dashboard. One endpoint per screen of the mobile design; the web dashboard endpoints are
/// unchanged. Lives under /api/dashboards so the gateway's existing dashboard route forwards it.
/// </summary>
/// <remarks>
/// Every endpoint accepts an optional <c>site</c> (site name or id). Every endpoint accepts <c>utcOffsetMinutes</c> (e.g. 240 for UAE) so "today", the calendar and the greeting follow
/// the phone's local time; it defaults to UTC.
/// </remarks>
public static class MobileDashboardEndpoints
{
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapMobileDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dashboards/mobile").WithTags("Mobile Dashboard");

        group.MapGet("/home", GetHomeAsync)
            .WithName("GetMobileDashboardHome")
            .WithSummary("Home screen: greeting, quick-action badges and every summary card.")
            .Produces<MobileHomeResponse>();

        group.MapGet("/asset-overview", GetAssetOverviewAsync)
            .WithName("GetMobileAssetOverview")
            .WithSummary("Asset Overview (View all): totals by status, type and location.")
            .Produces<MobileAssetOverviewResponse>();

        group.MapGet("/maintenance-performance", GetMaintenancePerformanceAsync)
            .WithName("GetMobileMaintenancePerformance")
            .WithSummary("Maintenance Performance (View all): planned / unplanned / overdue, monthly trend, MTTR & MTBF, by asset type.")
            .Produces<MobileMaintenancePerformanceResponse>();

        group.MapGet("/upcoming-activities", GetUpcomingActivitiesAsync)
            .WithName("GetMobileUpcomingActivities")
            .WithSummary("Upcoming Activities (View calendar): work orders, inspections and approvals per day.")
            .Produces<MobileUpcomingActivitiesResponse>();

        group.MapGet("/my-tasks", GetMyTasksAsync)
            .WithName("GetMobileMyTasks")
            .WithSummary("My Tasks: open work orders, inspections and approvals assigned to the signed-in user. Needs a bearer token.")
            .Produces<MobileMyTasksResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/recent-alerts", GetRecentAlertsAsync)
            .WithName("GetMobileRecentAlerts")
            .WithSummary("Recent Alerts (View all): IoT alerts plus overdue work, failed inspections, low stock and maintenance due soon.")
            .Produces<MobileRecentAlertsResponse>();

        group.MapGet("/asset-locations", GetAssetLocationsAsync)
            .WithName("GetMobileAssetLocations")
            .WithSummary("Asset Location (View map): map pins for assets with coordinates.")
            .Produces<MobileAssetLocationResponse>();

        group.MapGet("/asset-health", GetAssetHealthAsync)
            .WithName("GetMobileAssetHealth")
            .WithSummary("Asset Health (View details): healthy / warning / critical, by type and location, top critical assets.")
            .Produces<MobileAssetHealthResponse>();

        group.MapGet("/cost-summary", GetCostSummaryAsync)
            .WithName("GetMobileCostSummary")
            .WithSummary("Cost Summary (View details): total vs last year, by category, monthly trend, by asset type.")
            .Produces<MobileCostSummaryResponse>();

        group.MapGet("/recent-assets", GetRecentAssetsAsync)
            .WithName("GetMobileRecentAssets")
            .WithSummary("Recent Assets (View all): newest assets with status counts, search and paging.")
            .Produces<MobileRecentAssetsResponse>();

        return group;
    }

    private static async Task<IResult> GetHomeAsync(
        MobileDashboardService service,
        string? site,
        int? utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (ClockOrError(utcOffsetMinutes, out var clock) is { } error)
        {
            return error;
        }

        return Results.Ok(await service.GetHomeAsync(clock, site, cancellationToken));
    }

    private static async Task<IResult> GetAssetOverviewAsync(
        MobileDashboardService service,
        string? search,
        string? site,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetAssetOverviewAsync(search, site, cancellationToken));
    }

    private static async Task<IResult> GetMaintenancePerformanceAsync(
        MobileDashboardService service,
        int? months,
        string? assetType,
        string? site,
        int? utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (ClockOrError(utcOffsetMinutes, out var clock) is { } error)
        {
            return error;
        }

        var window = months ?? MobileDashboardService.DefaultMaintenanceMonths;
        if (window is < 1 or > 24)
        {
            return Invalid("months", "Months must be between 1 and 24.");
        }

        return Results.Ok(await service.GetMaintenancePerformanceAsync(clock, window, assetType, site, cancellationToken));
    }

    private static async Task<IResult> GetUpcomingActivitiesAsync(
        MobileDashboardService service,
        DateOnly? from,
        DateOnly? to,
        string? type,
        string? site,
        int? utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (ClockOrError(utcOffsetMinutes, out var clock) is { } error)
        {
            return error;
        }

        if (from is not null && to is not null && to < from)
        {
            return Invalid("to", "To must be on or after From.");
        }

        var start = from ?? clock.Today;
        if (to is not null && to.Value.DayNumber - start.DayNumber > 366)
        {
            return Invalid("to", "The range can be at most one year.");
        }

        return Results.Ok(await service.GetUpcomingActivitiesAsync(clock, from, to, type, site, cancellationToken));
    }

    private static async Task<IResult> GetMyTasksAsync(
        MobileDashboardService service,
        string? type,
        string? site,
        int? utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (ClockOrError(utcOffsetMinutes, out var clock) is { } error)
        {
            return error;
        }

        var tasks = await service.GetMyTasksAsync(clock, type, site, cancellationToken);
        return tasks is null ? Results.Unauthorized() : Results.Ok(tasks);
    }

    private static async Task<IResult> GetRecentAlertsAsync(
        MobileDashboardService service,
        string? severity,
        string? site,
        int? days,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var window = days ?? MobileDashboardService.DefaultAlertDays;
        if (window is < 1 or > 90)
        {
            return Invalid("days", "Days must be between 1 and 90.");
        }

        if (PagingOrError(page, pageSize, out var currentPage, out var size) is { } error)
        {
            return error;
        }

        return Results.Ok(await service.GetRecentAlertsAsync(Clock(0), window, severity, site, currentPage, size, cancellationToken));
    }

    private static async Task<IResult> GetAssetLocationsAsync(
        MobileDashboardService service,
        string? status,
        string? search,
        string? site,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetAssetLocationsAsync(status, search, site, cancellationToken));
    }

    private static async Task<IResult> GetAssetHealthAsync(
        MobileDashboardService service,
        int? top,
        string? site,
        CancellationToken cancellationToken)
    {
        var count = top ?? 5;
        if (count is < 1 or > 50)
        {
            return Invalid("top", "Top must be between 1 and 50.");
        }

        return Results.Ok(await service.GetAssetHealthAsync(Clock(0), count, site, cancellationToken));
    }

    private static async Task<IResult> GetCostSummaryAsync(
        MobileDashboardService service,
        string? period,
        int? year,
        string? site,
        CancellationToken cancellationToken)
    {
        if (period is not null && !new[] { "ytd", "year", "last-12-months", "12m" }.Contains(period.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Invalid("period", "Period must be ytd, year or last-12-months.");
        }

        if (year is < 2000 or > 2100)
        {
            return Invalid("year", "Year must be between 2000 and 2100.");
        }

        return Results.Ok(await service.GetCostSummaryAsync(Clock(0), period, year, site, cancellationToken));
    }

    private static async Task<IResult> GetRecentAssetsAsync(
        MobileDashboardService service,
        string? status,
        string? search,
        string? assetType,
        string? site,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (PagingOrError(page, pageSize, out var currentPage, out var size) is { } error)
        {
            return error;
        }

        return Results.Ok(await service.GetRecentAssetsAsync(status, search, assetType, site, currentPage, size, cancellationToken));
    }

    private static MobileClock Clock(int offsetMinutes) => new(DateTime.UtcNow, TimeSpan.FromMinutes(offsetMinutes));

    private static IResult? ClockOrError(int? utcOffsetMinutes, out MobileClock clock)
    {
        var offset = utcOffsetMinutes ?? 0;
        clock = Clock(Math.Clamp(offset, -14 * 60, 14 * 60));

        return offset is < -14 * 60 or > 14 * 60
            ? Invalid("utcOffsetMinutes", "UTC offset must be between -840 and 840 minutes.")
            : null;
    }

    private static IResult? PagingOrError(int? page, int? pageSize, out int currentPage, out int size)
    {
        currentPage = page ?? 1;
        size = pageSize ?? 20;

        if (currentPage < 1)
        {
            return Invalid("page", "Page must be 1 or more.");
        }

        return size is < 1 or > MaxPageSize ? Invalid("pageSize", $"Page size must be between 1 and {MaxPageSize}.") : null;
    }

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
