using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Geofence & Alerts → Alert Rule Builder.</summary>
public static class AlertRuleEndpoints
{
    private const string EntityType = "AlertRule";

    public static RouteGroupBuilder MapAlertRuleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/alert-rules").WithTags("Alert Rules");

        group.MapPost("", CreateAlertRuleAsync).WithName("CreateAlertRule");

        return group;
    }

    private static async Task<IResult> CreateAlertRuleAsync(
        CreateAlertRuleRequest request,
        IAlertRuleRepository repository,
        IGeofenceRepository geofenceRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var geofence = await geofenceRepository.GetByIdAsync(request.GeofenceId!.Trim(), cancellationToken);
        if (geofence is null || geofence.IsDeleted)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["GeofenceId"] = [$"Geofence '{request.GeofenceId}' was not found"]
            });
        }

        var rule = request.ToEntity(geofence, currentUserAccessor.GetCurrentUser());

        if (await repository.NameExistsAsync(rule.GeofenceId, rule.Name, cancellationToken))
        {
            return Results.Conflict(new { message = $"Alert rule '{rule.Name}' already exists for {geofence.Name}." });
        }

        await repository.CreateAsync(rule, cancellationToken);
        await geofenceRepository.IncrementRuleCountAsync(geofence.Id, cancellationToken);
        await eventLogger.LogAsync(EntityType, rule.Id, $"{geofence.Name} - {rule.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/alert-rules/{rule.Id}", AlertRuleResponse.FromEntity(rule));
    }
}
