using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class SystemConfigurationEndpoints
{
    private const string EntityType = "SystemConfiguration";
    private const string Route = "/api/administration/system-configuration";

    public static RouteGroupBuilder MapSystemConfigurationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("System Configuration");

        group.MapGet("", GetSystemConfigurationAsync).WithName("GetSystemConfiguration");
        group.MapPost("", SaveSystemConfigurationAsync).WithName("SaveSystemConfiguration");

        return group;
    }

    private static async Task<IResult> GetSystemConfigurationAsync(
        string? tenantId,
        string? clientId,
        ISystemConfigurationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        var configuration = await repository.GetByScopeAsync(
            FirstNonEmpty(tenantId, currentUser?.TenantId),
            FirstNonEmpty(clientId, currentUser?.ClientId),
            cancellationToken);

        return configuration is null
            ? Results.NotFound()
            : Results.Ok(SystemConfigurationResponse.FromEntity(configuration));
    }

    // Creates the tenant's configuration on the first save and replaces it on every save after that.
    private static async Task<IResult> SaveSystemConfigurationAsync(
        SaveSystemConfigurationRequest request,
        ISystemConfigurationRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var tenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId);
        var clientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId);
        var userName = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);

        var configuration = await repository.GetByScopeAsync(tenantId, clientId, cancellationToken);
        if (configuration is null)
        {
            configuration = new SystemConfiguration
            {
                // Generated on the server so the configuration gets a stable id independent of the Mongo _id.
                ConfigurationId = Guid.NewGuid().ToString(),
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId,
                ClientId = clientId,
                IsDeleted = false
            };
            request.ApplyTo(configuration);

            configuration = await repository.CreateAsync(configuration, cancellationToken);

            await eventLogger.LogAsync(EntityType, configuration.Id, configuration.PlatformIdentity.PlatformName, EventAction.Created, cancellationToken);

            return Results.Created(Route, SystemConfigurationResponse.FromEntity(configuration));
        }

        request.ApplyTo(configuration);
        configuration.UpdatedBy = userName;
        configuration.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(configuration.Id, configuration, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, configuration.Id, configuration.PlatformIdentity.PlatformName, EventAction.Updated, cancellationToken);

        return Results.Ok(SystemConfigurationResponse.FromEntity(configuration));
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
