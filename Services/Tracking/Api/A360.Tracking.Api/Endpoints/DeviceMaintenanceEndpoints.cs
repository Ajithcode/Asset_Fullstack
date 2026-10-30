using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Sequences;
using A360.Security;
using A360.Tracking.Api.Contracts;
using A360.Tracking.Api.Validation;
using A360.Tracking.Domain.Entities;
using A360.Tracking.Repository.Repositories;

namespace A360.Tracking.Api.Endpoints;

/// <summary>Device Maintenance: service jobs on devices and gateways, addressed by maintenance id (e.g. DM000001).</summary>
public static class DeviceMaintenanceEndpoints
{
    private const string EntityType = "DeviceMaintenance";
    private const string SequenceName = "DeviceMaintenance";
    private const string MaintenanceIdPrefix = "DM";
    private const int MaxSearchResults = 100;

    public static RouteGroupBuilder MapDeviceMaintenanceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/device-maintenance").WithTags("Device Maintenance");

        group.MapGet("", GetDeviceMaintenanceListAsync).WithName("GetDeviceMaintenanceList");
        // Create Service Request dropdowns. Literal segments take precedence over /{maintenanceId}.
        // Technician / vendor comes from GET /api/master-data/technicians and /api/master-data/amc-vendors.
        group.MapGet("/options", () => Results.Ok(DeviceMaintenanceOptionsResponse.Instance)).WithName("GetDeviceMaintenanceOptions");
        group.MapGet("/devices", GetDevicesAsync).WithName("GetDeviceMaintenanceDevices");
        group.MapGet("/maintenance-types", () => Results.Ok(DeviceMaintenanceType.All)).WithName("GetDeviceMaintenanceTypes");
        group.MapGet("/priorities", () => Results.Ok(DeviceMaintenancePriority.All)).WithName("GetDeviceMaintenancePriorities");
        group.MapGet("/statuses", () => Results.Ok(DeviceMaintenanceStatus.All)).WithName("GetDeviceMaintenanceStatuses");

        group.MapGet("/{maintenanceId}", GetDeviceMaintenanceAsync).WithName("GetDeviceMaintenanceById");
        group.MapPost("", CreateDeviceMaintenanceAsync).WithName("CreateDeviceMaintenance");
        group.MapPut("/{maintenanceId}", UpdateDeviceMaintenanceAsync).WithName("UpdateDeviceMaintenance");
        group.MapDelete("/{maintenanceId}", DeleteDeviceMaintenanceAsync).WithName("DeleteDeviceMaintenance");

        return group;
    }

    private static async Task<IResult> GetDeviceMaintenanceListAsync(
        IDeviceMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var jobs = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(TrackingRecords.Listed(jobs).Select(DeviceMaintenanceResponse.FromEntity).ToList());
    }

    private static async Task<IResult> GetDevicesAsync(
        string? search,
        int? limit,
        IIoTDeviceRepository deviceRepository,
        IDeviceMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var devices = await deviceRepository.SearchAsync(search, Math.Clamp(limit ?? 50, 1, MaxSearchResults), cancellationToken);
        var latestJobs = await repository.GetLatestByDeviceIdsAsync(devices.Select(device => device.DeviceId).ToList(), cancellationToken);

        return Results.Ok(devices
            .Select(device => MaintenanceDeviceOptionResponse.FromEntity(device, latestJobs.GetValueOrDefault(device.DeviceId)))
            .ToList());
    }

    private static async Task<IResult> GetDeviceMaintenanceAsync(
        string maintenanceId,
        IDeviceMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByMaintenanceIdAsync(maintenanceId.Trim(), cancellationToken);
        return job is null
            ? NotFound(maintenanceId)
            : Results.Ok(DeviceMaintenanceResponse.FromEntity(job));
    }

    private static async Task<IResult> CreateDeviceMaintenanceAsync(
        CreateDeviceMaintenanceRequest request,
        IDeviceMaintenanceRepository repository,
        IIoTDeviceRepository deviceRepository,
        ISequenceGenerator sequenceGenerator,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        // The form only picks a device, so model, asset and site are filled in from what is already known about it.
        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            var requestedDeviceId = request.DeviceId.Trim();
            var device = await deviceRepository.GetByDeviceIdAsync(requestedDeviceId, cancellationToken);
            var latestJobs = await repository.GetLatestByDeviceIdsAsync([device?.DeviceId ?? requestedDeviceId], cancellationToken);
            request = request.WithDeviceDefaults(device, latestJobs.Values.FirstOrDefault());
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        // Checked before the id is generated, so a rejected job does not use up a sequence number.
        if (await DuplicateJobAsync(request, null, repository, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var job = request.ToEntity($"{MaintenanceIdPrefix}{nextSequence:D6}", currentUserAccessor.GetCurrentUser());

        await repository.CreateAsync(job, cancellationToken);
        await eventLogger.LogAsync(EntityType, job.MaintenanceId, $"{job.DeviceId} - {job.MaintenanceType}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/device-maintenance/{job.MaintenanceId}", DeviceMaintenanceResponse.FromEntity(job));
    }

    /// <summary>Takes the same body as create; the job keeps its maintenance id.</summary>
    private static async Task<IResult> UpdateDeviceMaintenanceAsync(
        string maintenanceId,
        CreateDeviceMaintenanceRequest request,
        IDeviceMaintenanceRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var existing = await repository.GetByMaintenanceIdAsync(maintenanceId.Trim(), cancellationToken);
        if (existing is null)
        {
            return NotFound(maintenanceId);
        }

        if (await DuplicateJobAsync(request, existing.Id, repository, cancellationToken) is { } duplicate)
        {
            return duplicate;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var job = request.ToEntity(existing.MaintenanceId, currentUser).Replacing(existing, currentUser);

        if (!await repository.UpdateAsync(existing.Id, job, cancellationToken))
        {
            return NotFound(maintenanceId);
        }

        await eventLogger.LogAsync(EntityType, job.MaintenanceId, $"{job.DeviceId} - {job.MaintenanceType}", EventAction.Updated, cancellationToken);

        return Results.Ok(DeviceMaintenanceResponse.FromEntity(job));
    }

    private static async Task<IResult> DeleteDeviceMaintenanceAsync(
        string maintenanceId,
        IDeviceMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var job = await repository.GetByMaintenanceIdAsync(maintenanceId.Trim(), cancellationToken);
        if (job is null || !await repository.DeleteAsync(job.Id, cancellationToken))
        {
            return NotFound(maintenanceId);
        }

        await eventLogger.LogAsync(EntityType, job.MaintenanceId, $"{job.DeviceId} - {job.MaintenanceType}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // excludeId is the job being updated, so it does not clash with itself.
    private static async Task<IResult?> DuplicateJobAsync(
        CreateDeviceMaintenanceRequest request,
        string? excludeId,
        IDeviceMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var deviceId = request.DeviceId!.Trim();
        var maintenanceType = ContractText.Canonical(request.MaintenanceType, Domain.Entities.DeviceMaintenanceType.All)!;

        return await repository.ExistsAsync(deviceId, maintenanceType, request.DueDateOnly!.Value, excludeId, cancellationToken)
            ? Results.Conflict(new { message = $"{maintenanceType} maintenance for '{deviceId}' is already scheduled on {request.DueDateOnly:dd MMM yyyy}." })
            : null;
    }

    private static IResult NotFound(string maintenanceId)
    {
        return Results.NotFound(new { message = $"Device maintenance '{maintenanceId}' was not found." });
    }
}
