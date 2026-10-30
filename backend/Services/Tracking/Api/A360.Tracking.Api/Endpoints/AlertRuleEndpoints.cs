using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Geofence & Alerts → Alert Rule Builder, addressed by alert rule id.</summary>
public static class AlertRuleEndpoints
{
    private const string EntityType = "AlertRule";

    public static RouteGroupBuilder MapAlertRuleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/alert-rules").WithTags("Alert Rules");

        group.MapGet("", GetAlertRulesAsync).WithName("GetAlertRules");
        group.MapGet("/{id}", GetAlertRuleAsync).WithName("GetAlertRuleById");
        group.MapPost("", CreateAlertRuleAsync).WithName("CreateAlertRule");
        group.MapPut("/{id}", UpdateAlertRuleAsync).WithName("UpdateAlertRule");
        group.MapDelete("/{id}", DeleteAlertRuleAsync).WithName("DeleteAlertRule");

        return group;
    }

    private static async Task<IResult> GetAlertRulesAsync(
        IAlertRuleRepository repository,
        CancellationToken cancellationToken,
        string? geofenceId = null)
    {
        var rules = TrackingRecords.Listed(await repository.GetAllAsync(cancellationToken));

        if (!string.IsNullOrWhiteSpace(geofenceId))
        {
            rules = rules.Where(rule => rule.GeofenceId == geofenceId.Trim());
        }

        return Results.Ok(rules.Select(AlertRuleResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetAlertRuleAsync(
        string id,
        IAlertRuleRepository repository,
        CancellationToken cancellationToken)
    {
        var rule = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        return rule is null || rule.IsDeleted
            ? NotFound(id)
            : Results.Ok(AlertRuleResponse.FromEntity(rule));
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

        var geofence = await GetGeofenceAsync(request.GeofenceId!, geofenceRepository, cancellationToken);
        if (geofence is null)
        {
            return GeofenceNotFound(request.GeofenceId!);
        }

        var rule = request.ToEntity(geofence, currentUserAccessor.GetCurrentUser());

        if (await repository.NameExistsAsync(rule.GeofenceId, rule.Name, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Alert rule '{rule.Name}' already exists for {geofence.Name}." });
        }

        await repository.CreateAsync(rule, cancellationToken);
        await geofenceRepository.IncrementRuleCountAsync(geofence.Id, cancellationToken);
        await eventLogger.LogAsync(EntityType, rule.Id, $"{geofence.Name} - {rule.Name}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/alert-rules/{rule.Id}", AlertRuleResponse.FromEntity(rule));
    }

    /// <summary>Takes the same body as create; moving a rule to another geofence moves its count too.</summary>
    private static async Task<IResult> UpdateAlertRuleAsync(
        string id,
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

        var existing = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (existing is null || existing.IsDeleted)
        {
            return NotFound(id);
        }

        var geofence = await GetGeofenceAsync(request.GeofenceId!, geofenceRepository, cancellationToken);
        if (geofence is null)
        {
            return GeofenceNotFound(request.GeofenceId!);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var rule = request.ToEntity(geofence, currentUser).Replacing(existing, currentUser);

        if (await repository.NameExistsAsync(rule.GeofenceId, rule.Name, existing.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Alert rule '{rule.Name}' already exists for {geofence.Name}." });
        }

        if (!await repository.UpdateAsync(existing.Id, rule, cancellationToken))
        {
            return NotFound(id);
        }

        if (rule.GeofenceId != existing.GeofenceId)
        {
            await geofenceRepository.DecrementRuleCountAsync(existing.GeofenceId, cancellationToken);
            await geofenceRepository.IncrementRuleCountAsync(rule.GeofenceId, cancellationToken);
        }

        await eventLogger.LogAsync(EntityType, rule.Id, $"{geofence.Name} - {rule.Name}", EventAction.Updated, cancellationToken);

        return Results.Ok(AlertRuleResponse.FromEntity(rule));
    }

    private static async Task<IResult> DeleteAlertRuleAsync(
        string id,
        IAlertRuleRepository repository,
        IGeofenceRepository geofenceRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var rule = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (rule is null || rule.IsDeleted)
        {
            return NotFound(id);
        }

        if (!await repository.DeleteAsync(rule.Id, cancellationToken))
        {
            return NotFound(id);
        }

        await geofenceRepository.DecrementRuleCountAsync(rule.GeofenceId, cancellationToken);
        await eventLogger.LogAsync(EntityType, rule.Id, $"{rule.GeofenceName} - {rule.Name}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<Geofence?> GetGeofenceAsync(string geofenceId, IGeofenceRepository geofenceRepository, CancellationToken cancellationToken)
    {
        var geofence = await geofenceRepository.GetByIdAsync(geofenceId.Trim(), cancellationToken);
        return geofence is null || geofence.IsDeleted ? null : geofence;
    }

    private static IResult GeofenceNotFound(string geofenceId)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["GeofenceId"] = [$"Geofence '{geofenceId}' was not found"]
        });
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Alert rule '{id}' was not found." });
    }
}
