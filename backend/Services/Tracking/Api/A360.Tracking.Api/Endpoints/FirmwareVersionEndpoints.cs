using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Firmware & Connectivity → Firmware Inventory: firmware versions by device model, addressed by record id.</summary>
public static class FirmwareVersionEndpoints
{
    private const string EntityType = "FirmwareVersion";

    public static RouteGroupBuilder MapFirmwareVersionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/firmware-versions").WithTags("Firmware Versions");

        group.MapGet("", GetFirmwareVersionsAsync).WithName("GetFirmwareVersions");
        group.MapGet("/{id}", GetFirmwareVersionAsync).WithName("GetFirmwareVersionById");
        group.MapPost("", CreateFirmwareVersionAsync).WithName("CreateFirmwareVersion");
        group.MapPut("/{id}", UpdateFirmwareVersionAsync).WithName("UpdateFirmwareVersion");
        group.MapDelete("/{id}", DeleteFirmwareVersionAsync).WithName("DeleteFirmwareVersion");

        return group;
    }

    private static async Task<IResult> GetFirmwareVersionsAsync(
        IFirmwareVersionRepository repository,
        CancellationToken cancellationToken)
    {
        var firmwareVersions = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(firmwareVersions).Select(FirmwareVersionResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetFirmwareVersionAsync(
        string id,
        IFirmwareVersionRepository repository,
        CancellationToken cancellationToken)
    {
        var firmware = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        return firmware is null || firmware.IsDeleted
            ? NotFound(id)
            : Results.Ok(FirmwareVersionResponse.FromEntity(firmware));
    }

    private static async Task<IResult> CreateFirmwareVersionAsync(
        CreateFirmwareVersionRequest request,
        IFirmwareVersionRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var firmware = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.DeviceModelExistsAsync(firmware.DeviceModel, cancellationToken: cancellationToken))
        {
            return Results.Conflict(new { message = $"Firmware for device model '{firmware.DeviceModel}' is already registered." });
        }

        await repository.CreateAsync(firmware, cancellationToken);
        await eventLogger.LogAsync(EntityType, firmware.Id, firmware.DeviceModel, EventAction.Created, cancellationToken);

        return Results.Created($"/api/firmware-versions/{firmware.Id}", FirmwareVersionResponse.FromEntity(firmware));
    }

    /// <summary>Takes the same body as create.</summary>
    private static async Task<IResult> UpdateFirmwareVersionAsync(
        string id,
        CreateFirmwareVersionRequest request,
        IFirmwareVersionRepository repository,
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

        var currentUser = currentUserAccessor.GetCurrentUser();
        var firmware = request.ToEntity(currentUser).Replacing(existing, currentUser);

        if (await repository.DeviceModelExistsAsync(firmware.DeviceModel, existing.Id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Firmware for device model '{firmware.DeviceModel}' is already registered." });
        }

        if (!await repository.UpdateAsync(existing.Id, firmware, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, firmware.Id, firmware.DeviceModel, EventAction.Updated, cancellationToken);

        return Results.Ok(FirmwareVersionResponse.FromEntity(firmware));
    }

    private static async Task<IResult> DeleteFirmwareVersionAsync(
        string id,
        IFirmwareVersionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var firmware = await repository.GetByIdAsync(id.Trim(), cancellationToken);
        if (firmware is null || firmware.IsDeleted || !await repository.DeleteAsync(firmware.Id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, firmware.Id, firmware.DeviceModel, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Firmware version '{id}' was not found." });
    }
}
