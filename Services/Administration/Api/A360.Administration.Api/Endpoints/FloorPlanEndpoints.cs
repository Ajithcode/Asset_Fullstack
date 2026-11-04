using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>Floor Plans (Administration > Organization &amp; Locations > Maps &amp; Floor Plans): uploaded plans of locations.</summary>
public static class FloorPlanEndpoints
{
    private const string EntityType = "FloorPlan";
    private const string Route = "/api/floor-plans";

    public static RouteGroupBuilder MapFloorPlanEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("Floor Plans");

        group.MapGet("/options", () => Results.Ok(FloorPlanOptionsResponse.Instance)).WithName("GetFloorPlanOptions");
        group.MapGet("/{floorPlanId}", GetFloorPlanByIdAsync).WithName("GetFloorPlanById");
        group.MapPost("", CreateFloorPlanAsync).WithName("CreateFloorPlan");

        return group;
    }

    private static async Task<IResult> GetFloorPlanByIdAsync(
        string floorPlanId,
        IFloorPlanRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var plan = await repository.GetByFloorPlanIdAsync(floorPlanId, cancellationToken);
        if (plan is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, plan.Id, plan.Name, EventAction.Viewed, cancellationToken);

        return Results.Ok(FloorPlanResponse.FromEntity(plan));
    }

    private static async Task<IResult> CreateFloorPlanAsync(
        CreateFloorPlanRequest request,
        IFloorPlanRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var plan = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.NameVersionExistsAsync(plan.Name, plan.Version, plan.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Floor plan '{plan.Name}' {plan.Version} already exists" });
        }

        plan = await repository.CreateAsync(plan, cancellationToken);

        await eventLogger.LogAsync(EntityType, plan.Id, plan.Name, EventAction.Created, cancellationToken);

        return Results.Created($"{Route}/{plan.FloorPlanId}", FloorPlanResponse.FromEntity(plan));
    }
}
