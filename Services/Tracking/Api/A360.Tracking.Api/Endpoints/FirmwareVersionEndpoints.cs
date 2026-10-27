using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Firmware & Connectivity → Firmware Inventory: firmware versions by device model.</summary>
public static class FirmwareVersionEndpoints
{
    private const string EntityType = "FirmwareVersion";

    public static RouteGroupBuilder MapFirmwareVersionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/firmware-versions").WithTags("Firmware Versions");

        group.MapPost("", CreateFirmwareVersionAsync).WithName("CreateFirmwareVersion");

        return group;
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

        if (await repository.DeviceModelExistsAsync(firmware.DeviceModel, cancellationToken))
        {
            return Results.Conflict(new { message = $"Firmware for device model '{firmware.DeviceModel}' is already registered." });
        }

        await repository.CreateAsync(firmware, cancellationToken);
        await eventLogger.LogAsync(EntityType, firmware.Id, firmware.DeviceModel, EventAction.Created, cancellationToken);

        return Results.Created($"/api/firmware-versions/{firmware.Id}", FirmwareVersionResponse.FromEntity(firmware));
    }
}
