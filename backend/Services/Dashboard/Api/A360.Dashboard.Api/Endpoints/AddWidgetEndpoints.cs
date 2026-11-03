using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Repository.Repositories;

namespace A360.Dashboard.Api.Endpoints;

public static class AddWidgetEndpoints
{
    public static RouteGroupBuilder MapAddWidgetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/add-widgets").WithTags("Add Widgets");

        // Read-only: the addwidget collection is populated directly in the database.
        group.MapGet("/{widgetId}", GetAddWidgetByIdAsync).WithName("GetAddWidgetById");

        return group;
    }

    private static async Task<IResult> GetAddWidgetByIdAsync(
        string widgetId,
        IAddWidgetRepository repository,
        CancellationToken cancellationToken)
    {
        var widget = await repository.GetByWidgetIdAsync(widgetId, cancellationToken);
        if (widget is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(WidgetResponse.FromEntity(widget));
    }
}
