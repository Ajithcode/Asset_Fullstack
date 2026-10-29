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

        group.MapPost("", CreateIntegrationAsync).WithName("CreateIntegration");

        return group;
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
}
