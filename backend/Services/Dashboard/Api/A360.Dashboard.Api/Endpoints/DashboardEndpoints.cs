using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Api.Validation;
using A360.Dashboard.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Dashboard.Api.Endpoints;

public static class DashboardEndpoints
{
    private const string EntityType = "Dashboard";

    public static RouteGroupBuilder MapDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dashboards").WithTags("Dashboards");

        group.MapGet("", GetDashboardsAsync).WithName("GetDashboards");
        group.MapGet("/{dashboardId}", GetDashboardByIdAsync).WithName("GetDashboardById");
        group.MapPost("", CreateDashboardAsync).WithName("CreateDashboard");
        group.MapPut("/{dashboardId}", UpdateDashboardAsync).WithName("UpdateDashboard");
        group.MapDelete("/{dashboardId}", DeleteDashboardAsync).WithName("DeleteDashboard");

        return group;
    }

    private static async Task<IResult> GetDashboardsAsync(
        string? tenantId,
        string? clientId,
        IDashboardRepository repository,
        CancellationToken cancellationToken)
    {
        var dashboards = await repository.GetAsync(tenantId, clientId, cancellationToken);

        return Results.Ok(dashboards.Select(DashboardResponse.FromEntity));
    }

    private static async Task<IResult> GetDashboardByIdAsync(
        string dashboardId,
        IDashboardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var dashboard = await repository.GetByDashboardIdAsync(dashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, dashboard.Id, dashboard.DashboardName, EventAction.Viewed, cancellationToken);

        return Results.Ok(DashboardResponse.FromEntity(dashboard));
    }

    private static async Task<IResult> CreateDashboardAsync(
        CreateDashboardRequest request,
        IDashboardRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var dashboard = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, dashboard.Id, dashboard.DashboardName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/dashboards/{dashboard.DashboardId}", DashboardResponse.FromEntity(dashboard));
    }

    private static async Task<IResult> UpdateDashboardAsync(
        string dashboardId,
        UpdateDashboardRequest request,
        IDashboardRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var dashboard = await repository.GetByDashboardIdAsync(dashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(dashboard, currentUserAccessor.GetCurrentUser());

        var updated = await repository.UpdateAsync(dashboard.Id, dashboard, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, dashboard.Id, dashboard.DashboardName, EventAction.Updated, cancellationToken);

        return Results.Ok(DashboardResponse.FromEntity(dashboard));
    }

    private static async Task<IResult> DeleteDashboardAsync(
        string dashboardId,
        IDashboardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var dashboard = await repository.GetByDashboardIdAsync(dashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(dashboard.Id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, dashboard.Id, dashboard.DashboardName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }
}
