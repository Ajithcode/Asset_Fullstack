using A360.Administration.Api.Contracts;
using A360.Administration.Api.Security;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class IntegrationEndpoints
{
    private const string EntityType = "Integration";

    public static RouteGroupBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/administration/integrations").WithTags("Integrations");

        group.MapGet("", GetIntegrationsAsync).WithName("GetIntegrations");
        group.MapGet("/{integrationId}", GetIntegrationByIdAsync).WithName("GetIntegrationById");
        group.MapPost("", CreateIntegrationAsync).WithName("CreateIntegration");
        group.MapPut("/{integrationId}", UpdateIntegrationAsync).WithName("UpdateIntegration");
        group.MapDelete("/{integrationId}", DeleteIntegrationAsync).WithName("DeleteIntegration");

        return group;
    }

    private static async Task<IResult> GetIntegrationsAsync(
        string? tenantId,
        string? clientId,
        string? environment,
        string? status,
        IIntegrationRepository repository,
        CancellationToken cancellationToken)
    {
        var integrations = await repository.GetAsync(tenantId, clientId, environment, status, cancellationToken);

        return Results.Ok(integrations.Select(IntegrationResponse.FromEntity));
    }

    private static async Task<IResult> GetIntegrationByIdAsync(
        string integrationId,
        IIntegrationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var integration = await repository.GetByIntegrationIdAsync(integrationId, cancellationToken);
        if (integration is null)
        {
            return NotFound(integrationId);
        }

        await eventLogger.LogAsync(EntityType, integration.Id, integration.IntegrationName, EventAction.Viewed, cancellationToken);

        return Results.Ok(IntegrationResponse.FromEntity(integration));
    }

    private static async Task<IResult> CreateIntegrationAsync(
        CreateIntegrationRequest request,
        IIntegrationRepository repository,
        ISecretProtector secretProtector,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var integration = request.ToEntity(currentUserAccessor.GetCurrentUser(), secretProtector);

        if (await repository.IntegrationCodeExistsAsync(integration.IntegrationCode, integration.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Integration code '{integration.IntegrationCode}' already exists" });
        }

        integration = await repository.CreateAsync(integration, cancellationToken);

        await eventLogger.LogAsync(EntityType, integration.Id, integration.IntegrationName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/integrations/{integration.IntegrationId}",
            IntegrationResponse.FromEntity(integration));
    }

    private static async Task<IResult> UpdateIntegrationAsync(
        string integrationId,
        UpdateIntegrationRequest request,
        IIntegrationRepository repository,
        ISecretProtector secretProtector,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var integration = await repository.GetByIntegrationIdAsync(integrationId, cancellationToken);
        if (integration is null)
        {
            return NotFound(integrationId);
        }

        request.ApplyTo(integration, currentUserAccessor.GetCurrentUser(), secretProtector);

        if (await repository.IntegrationCodeExistsAsync(
                integration.IntegrationCode,
                integration.TenantId,
                cancellationToken,
                integration.IntegrationId))
        {
            return Results.Conflict(new { message = $"Integration code '{integration.IntegrationCode}' already exists" });
        }

        if (!await repository.UpdateAsync(integration.Id, integration, cancellationToken))
        {
            return NotFound(integrationId);
        }

        await eventLogger.LogAsync(EntityType, integration.Id, integration.IntegrationName, EventAction.Updated, cancellationToken);

        return Results.Ok(IntegrationResponse.FromEntity(integration));
    }

    private static async Task<IResult> DeleteIntegrationAsync(
        string integrationId,
        IIntegrationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var integration = await repository.GetByIntegrationIdAsync(integrationId, cancellationToken);
        if (integration is null)
        {
            return NotFound(integrationId);
        }

        // Soft delete: the record (and its encrypted secret) stays for audit.
        integration.MarkDeleted(currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(integration.Id, integration, cancellationToken))
        {
            return NotFound(integrationId);
        }

        await eventLogger.LogAsync(EntityType, integration.Id, integration.IntegrationName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string integrationId)
    {
        return Results.NotFound(new { message = $"Integration '{integrationId}' was not found" });
    }
}
