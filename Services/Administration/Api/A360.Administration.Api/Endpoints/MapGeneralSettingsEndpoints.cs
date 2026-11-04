using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

/// <summary>General Settings (Administration > Organization &amp; Locations > Maps &amp; Floor Plans > Settings).</summary>
public static class MapGeneralSettingsEndpoints
{
    private const string EntityType = "MapGeneralSettings";
    private const string Route = "/api/general-settings";
    private const string LogName = "Maps & Floor Plans General Settings";

    public static RouteGroupBuilder MapMapGeneralSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Route).WithTags("General Settings");

        group.MapGet("", GetCurrentSettingsAsync).WithName("GetGeneralSettings");
        group.MapGet("/options", () => Results.Ok(MapGeneralSettingsOptionsResponse.Instance)).WithName("GetGeneralSettingsOptions");
        group.MapGet("/{settingsId}", GetSettingsByIdAsync).WithName("GetGeneralSettingsById");
        group.MapPost("", SaveSettingsAsync).WithName("SaveGeneralSettings");

        return group;
    }

    /// <summary>The tenant's saved settings, or the defaults (isDefault true, no settingsId) before the first save.</summary>
    private static async Task<IResult> GetCurrentSettingsAsync(
        string? tenantId,
        string? clientId,
        IMapGeneralSettingsRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var currentUser = currentUserAccessor.GetCurrentUser();
        tenantId = FirstNonEmpty(tenantId, currentUser?.TenantId);
        clientId = FirstNonEmpty(clientId, currentUser?.ClientId);

        var settings = await repository.GetByScopeAsync(tenantId, clientId, cancellationToken);

        return settings is null
            ? Results.Ok(MapGeneralSettingsResponse.FromEntity(
                new MapGeneralSettings { TenantId = tenantId, ClientId = clientId }, isDefault: true))
            : Results.Ok(MapGeneralSettingsResponse.FromEntity(settings));
    }

    private static async Task<IResult> GetSettingsByIdAsync(
        string settingsId,
        IMapGeneralSettingsRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var settings = await repository.GetBySettingsIdAsync(settingsId, cancellationToken);
        if (settings is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, settings.Id, LogName, EventAction.Viewed, cancellationToken);

        return Results.Ok(MapGeneralSettingsResponse.FromEntity(settings));
    }

    // Creates the tenant's settings on the first save and replaces them on every save after that.
    private static async Task<IResult> SaveSettingsAsync(
        SaveMapGeneralSettingsRequest request,
        IMapGeneralSettingsRepository repository,
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

        var settings = await repository.GetByScopeAsync(tenantId, clientId, cancellationToken);
        if (settings is null)
        {
            settings = new MapGeneralSettings
            {
                // Generated on the server so the settings get a stable id independent of the Mongo _id.
                SettingsId = Guid.NewGuid().ToString(),
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId,
                ClientId = clientId,
                IsDeleted = false
            };
            request.ApplyTo(settings);

            settings = await repository.CreateAsync(settings, cancellationToken);

            await eventLogger.LogAsync(EntityType, settings.Id, LogName, EventAction.Created, cancellationToken);

            return Results.Created($"{Route}/{settings.SettingsId}", MapGeneralSettingsResponse.FromEntity(settings));
        }

        request.ApplyTo(settings);
        settings.UpdatedBy = userName;
        settings.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(settings.Id, settings, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, settings.Id, LogName, EventAction.Updated, cancellationToken);

        return Results.Ok(MapGeneralSettingsResponse.FromEntity(settings));
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
