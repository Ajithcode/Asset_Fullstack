
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class TechnicianEndpoints
{
    public static RouteGroupBuilder MapTechnicianEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/technicians")
            .WithTags("Technicians");

        group.MapGet("", GetTechniciansAsync)
            .WithName("GetTechnicians");

        group.MapGet("/{id}", GetTechnicianByIdAsync)
            .WithName("GetTechnicianById");

        group.MapGet("/asset/{assetId}", GetTechniciansByAssetIdAsync)
            .WithName("GetTechniciansByAssetId");

        group.MapPost("", CreateTechnicianAsync)
            .WithName("CreateTechnician");

        group.MapPut("/{id}", UpdateTechnicianAsync)
            .WithName("UpdateTechnician");

        group.MapDelete("/{id}", DeleteTechnicianAsync)
            .WithName("DeleteTechnician");

        return group;
    }

    private static async Task<IResult> GetTechniciansAsync(
        ITechnicianRepository repository,
        CancellationToken cancellationToken)
    {
        var technicians = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            technicians.Select(TechnicianResponse.FromEntity));
    }

    private static async Task<IResult> GetTechnicianByIdAsync(
        string id,
        ITechnicianRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technicians id." });
        }

        var technician = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (technician is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> GetTechniciansByAssetIdAsync(
        string assetId,
        ITechnicianRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var technicians = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            technicians.Select(TechnicianResponse.FromEntity));
    }

    private static async Task<IResult> CreateTechnicianAsync(
        CreateTechnicianRequest request,
        ITechnicianRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var technician = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/technicians/{technician.Id}",
            TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> UpdateTechnicianAsync(
        string id,
        UpdateTechnicianRequest request,
        ITechnicianRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technicians id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var technician = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (technician is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(technician);

        var updated = await repository.UpdateAsync(
            id,
            technician,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> DeleteTechnicianAsync(
        string id,
        ITechnicianRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technicians id." });
        }

        var technician = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (technician is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
