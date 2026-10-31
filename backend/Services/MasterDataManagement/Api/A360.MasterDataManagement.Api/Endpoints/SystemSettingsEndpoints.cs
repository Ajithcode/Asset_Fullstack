using SystemSettingsEntity = A360.MasterDataManagement.Domain.Entities.SystemSettings;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class SystemSettingsEndpoints
{
    public static RouteGroupBuilder MapSystemSettingsEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/system-settings")
            .WithTags("SystemSettings");

        group.MapGet("", GetSystemSettingsAsync)
            .WithName("GetSystemSettings");

        group.MapPut("", UpdateSystemSettingsAsync)
            .WithName("UpdateSystemSettings");

        return group;
    }

    private static async Task<IResult> GetSystemSettingsAsync(
        string? clientId,
        ISystemSettingsRepository repository,
        CancellationToken cancellationToken)
    {
        var settings = await repository.GetByClientAsync(
            clientId,
            cancellationToken);

        return Results.Ok(SystemSettingsResponse.FromEntity(
            settings ?? new SystemSettingsEntity { ClientId = clientId }));
    }

    private static async Task<IResult> UpdateSystemSettingsAsync(
        UpdateSystemSettingsRequest request,
        ISystemSettingsRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var settings = await repository.GetByClientAsync(
            request.ClientId,
            cancellationToken);

        if (settings is null)
        {
            settings = new SystemSettingsEntity
            {
                ClientId = request.ClientId,
                CreatedBy = request.UpdatedBy,
                CreatedAt = DateTime.UtcNow
            };

            request.ApplyTo(settings);

            settings = await repository.CreateAsync(
                settings,
                cancellationToken);
        }
        else
        {
            request.ApplyTo(settings);

            await repository.UpdateAsync(
                settings.Id,
                settings,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "SystemSettings",
            settings.Id,
            settings.General.SystemName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SystemSettingsResponse.FromEntity(settings));
    }
}
