using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class TechnicianEndpoints
{
    public static RouteGroupBuilder MapTechnicianEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/technicians")
            .WithTags("Technicians");

        group.MapGet("", GetTechniciansAsync)
            .WithName("GetTechnicians");

        group.MapGet("/{id}", GetTechnicianByIdAsync)
            .WithName("GetTechnicianById");

        group.MapPost("", CreateTechnicianAsync)
            .WithName("CreateTechnician");

        group.MapPut("/{id}", UpdateTechnicianAsync)
            .WithName("UpdateTechnician");

        group.MapPatch("/{id}/status", UpdateTechnicianStatusAsync)
            .WithName("UpdateTechnicianStatus");

        group.MapDelete("/{id}", DeleteTechnicianAsync)
            .WithName("DeleteTechnician");

        return group;
    }

    private static async Task<IResult> GetTechniciansAsync(
        string? search,
        string? status,
        string? primarySkill,
        string? assignedTeamId,
        string? clientId,
        ITechnicianRepository repository,
        CancellationToken cancellationToken)
    {
        var technicians = await repository.SearchAsync(
            new TechnicianFilter(search, status, primarySkill, assignedTeamId, clientId),
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
                new { message = "Invalid technician id." });
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
            technician.TechnicianName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> CreateTechnicianAsync(
        CreateTechnicianRequest request,
        ITechnicianRepository repository,
        ITeamRepository teams,
        IEmployeeRepository employees,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var technician = request.ToEntity();

        var existing = await repository.GetByTechnicianIdAsync(
            technician.TechnicianId,
            technician.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A technician with technician id '{technician.TechnicianId}' already exists." });
        }

        var teamError = await MasterDataReferenceResolver.ResolveTeamAsync(
            technician.AssignedTeam,
            teams,
            cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        var employeeError = await MasterDataReferenceResolver.ResolveEmployeeAsync(
            technician.Employee,
            employees,
            cancellationToken);

        if (employeeError is not null)
        {
            return employeeError;
        }

        technician = await repository.CreateAsync(
            technician,
            cancellationToken);

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.TechnicianName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/technicians/{technician.Id}",
            TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> UpdateTechnicianAsync(
        string id,
        UpdateTechnicianRequest request,
        ITechnicianRepository repository,
        ITeamRepository teams,
        IEmployeeRepository employees,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technician id." });
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

        var previousName = technician.TechnicianName;

        request.ApplyTo(technician);

        var existing = await repository.GetByTechnicianIdAsync(
            technician.TechnicianId,
            technician.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A technician with technician id '{technician.TechnicianId}' already exists." });
        }

        var teamError = await MasterDataReferenceResolver.ResolveTeamAsync(
            technician.AssignedTeam,
            teams,
            cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        var employeeError = await MasterDataReferenceResolver.ResolveEmployeeAsync(
            technician.Employee,
            employees,
            cancellationToken);

        if (employeeError is not null)
        {
            return employeeError;
        }

        var updated = await repository.UpdateAsync(
            id,
            technician,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != technician.TechnicianName)
        {
            await teams.UpdateTeamLeaderNameAsync(
                id,
                technician.TechnicianName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Technician",
            technician.Id,
            technician.TechnicianName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> UpdateTechnicianStatusAsync(
        string id,
        UpdateTechnicianStatusRequest request,
        ITechnicianRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technician id." });
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

        technician.Status = request.Status;
        technician.UpdatedBy = request.UpdatedBy;
        technician.UpdatedAt = DateTime.UtcNow;

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
            technician.TechnicianName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TechnicianResponse.FromEntity(technician));
    }

    private static async Task<IResult> DeleteTechnicianAsync(
        string id,
        ITechnicianRepository repository,
        ITeamRepository teams,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid technician id." });
        }

        var technician = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (technician is null)
        {
            return Results.NotFound();
        }

        if (await teams.AnyWithTeamLeaderAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This technician leads a team. Choose another team leader first." });
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
            technician.TechnicianName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
