using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Api.Validation;
using A360.Dashboard.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Dashboard.Api.Endpoints;

public static class WidgetEndpoints
{
    private const string EntityType = "Widget";

    public static RouteGroupBuilder MapWidgetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/widgets").WithTags("Widgets");

        group.MapGet("", GetWidgetsAsync).WithName("GetWidgets");
        group.MapGet("/{widgetId}", GetWidgetByIdAsync).WithName("GetWidgetById");
        group.MapPost("", CreateWidgetAsync).WithName("CreateWidget");
        group.MapPut("/{widgetId}", UpdateWidgetAsync).WithName("UpdateWidget");
        group.MapDelete("/{widgetId}", DeleteWidgetAsync).WithName("DeleteWidget");

        return group;
    }

    private static async Task<IResult> GetWidgetsAsync(
        string? dashboardId,
        string? tenantId,
        string? clientId,
        string? moduleName,
        IWidgetRepository repository,
        CancellationToken cancellationToken)
    {
        var widgets = await repository.GetAsync(dashboardId, tenantId, clientId, moduleName, cancellationToken);

        return Results.Ok(widgets.Select(WidgetResponse.FromEntity));
    }

    private static async Task<IResult> GetWidgetByIdAsync(
        string widgetId,
        IWidgetRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var widget = await repository.GetByWidgetIdAsync(widgetId, cancellationToken);
        if (widget is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, widget.Id, widget.ModuleName, EventAction.Viewed, cancellationToken);

        return Results.Ok(WidgetResponse.FromEntity(widget));
    }

    private static async Task<IResult> CreateWidgetAsync(
        CreateWidgetRequest request,
        IWidgetRepository repository,
        IDashboardRepository dashboardRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var dashboardErrors = await ValidateDashboardExistsAsync(request.DashboardId!, dashboardRepository, cancellationToken);
        if (dashboardErrors is not null)
        {
            return Results.ValidationProblem(dashboardErrors);
        }

        var widget = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, widget.Id, widget.ModuleName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/widgets/{widget.WidgetId}", WidgetResponse.FromEntity(widget));
    }

    private static async Task<IResult> UpdateWidgetAsync(
        string widgetId,
        UpdateWidgetRequest request,
        IWidgetRepository repository,
        IDashboardRepository dashboardRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var widget = await repository.GetByWidgetIdAsync(widgetId, cancellationToken);
        if (widget is null)
        {
            return Results.NotFound();
        }

        var dashboardErrors = await ValidateDashboardExistsAsync(request.DashboardId!, dashboardRepository, cancellationToken);
        if (dashboardErrors is not null)
        {
            return Results.ValidationProblem(dashboardErrors);
        }

        request.ApplyTo(widget, currentUserAccessor.GetCurrentUser());

        var updated = await repository.UpdateAsync(widget.Id, widget, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, widget.Id, widget.ModuleName, EventAction.Updated, cancellationToken);

        return Results.Ok(WidgetResponse.FromEntity(widget));
    }

    private static async Task<IResult> DeleteWidgetAsync(
        string widgetId,
        IWidgetRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var widget = await repository.GetByWidgetIdAsync(widgetId, cancellationToken);
        if (widget is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(widget.Id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, widget.Id, widget.ModuleName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<Dictionary<string, string[]>?> ValidateDashboardExistsAsync(
        string dashboardId,
        IDashboardRepository dashboardRepository,
        CancellationToken cancellationToken)
    {
        var dashboard = await dashboardRepository.GetByDashboardIdAsync(dashboardId.Trim(), cancellationToken);

        return dashboard is null
            ? new Dictionary<string, string[]> { ["DashboardId"] = [$"Dashboard '{dashboardId}' was not found"] }
            : null;
    }
}
