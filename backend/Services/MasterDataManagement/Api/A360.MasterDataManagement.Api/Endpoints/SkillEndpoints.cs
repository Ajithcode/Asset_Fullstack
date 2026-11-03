using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class SkillEndpoints
{
    public static RouteGroupBuilder MapSkillEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/skills")
            .WithTags("Skills");

        group.MapGet("", GetSkillsAsync)
            .WithName("GetSkills");

        group.MapGet("/{id}", GetSkillByIdAsync)
            .WithName("GetSkillById");

        group.MapPost("", CreateSkillAsync)
            .WithName("CreateSkill");

        group.MapPut("/{id}", UpdateSkillAsync)
            .WithName("UpdateSkill");

        group.MapPatch("/{id}/status", UpdateSkillStatusAsync)
            .WithName("UpdateSkillStatus");

        group.MapDelete("/{id}", DeleteSkillAsync)
            .WithName("DeleteSkill");

        return group;
    }

    private static async Task<IResult> GetSkillsAsync(
        string? search,
        string? status,
        string? category,
        string? type,
        string? clientId,
        ISkillRepository repository,
        CancellationToken cancellationToken)
    {
        var skills = await repository.SearchAsync(
            new SkillFilter(search, status, category, type, clientId),
            cancellationToken);

        return Results.Ok(
            skills.Select(SkillResponse.FromEntity));
    }

    private static async Task<IResult> GetSkillByIdAsync(
        string id,
        ISkillRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid skill id." });
        }

        var skill = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (skill is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Skill",
            skill.Id,
            skill.SkillName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SkillResponse.FromEntity(skill));
    }

    private static async Task<IResult> CreateSkillAsync(
        CreateSkillRequest request,
        ISkillRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var skill = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            skill.Code,
            skill.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A skill with code '{skill.Code}' already exists." });
        }

        skill = await repository.CreateAsync(
            skill,
            cancellationToken);

        await eventLogger.LogAsync(
            "Skill",
            skill.Id,
            skill.SkillName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/skills/{skill.Id}",
            SkillResponse.FromEntity(skill));
    }

    private static async Task<IResult> UpdateSkillAsync(
        string id,
        UpdateSkillRequest request,
        ISkillRepository repository,
        IEmployeeRepository employees,
        ITechnicianRepository technicians,
        IServiceProviderRepository serviceProviders,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid skill id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var skill = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (skill is null)
        {
            return Results.NotFound();
        }

        var previousName = skill.SkillName;

        request.ApplyTo(skill);

        var existing = await repository.GetByCodeAsync(
            skill.Code,
            skill.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A skill with code '{skill.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            skill,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        // People and service providers hold skills by name.
        if (previousName != skill.SkillName)
        {
            await employees.RenameSkillAsync(previousName, skill.SkillName, skill.ClientId, cancellationToken);
            await technicians.RenameSkillAsync(previousName, skill.SkillName, skill.ClientId, cancellationToken);
            await serviceProviders.RenameSkillAsync(previousName, skill.SkillName, skill.ClientId, cancellationToken);
        }

        await eventLogger.LogAsync(
            "Skill",
            skill.Id,
            skill.SkillName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SkillResponse.FromEntity(skill));
    }

    private static async Task<IResult> UpdateSkillStatusAsync(
        string id,
        UpdateSkillStatusRequest request,
        ISkillRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid skill id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var skill = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (skill is null)
        {
            return Results.NotFound();
        }

        skill.Status = request.Status;
        skill.UpdatedBy = request.UpdatedBy;
        skill.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            skill,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Skill",
            skill.Id,
            skill.SkillName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SkillResponse.FromEntity(skill));
    }

    private static async Task<IResult> DeleteSkillAsync(
        string id,
        ISkillRepository repository,
        IEmployeeRepository employees,
        ITechnicianRepository technicians,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid skill id." });
        }

        var skill = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (skill is null)
        {
            return Results.NotFound();
        }

        var holders = await employees.CountWithSkillAsync(skill.SkillName, skill.ClientId, cancellationToken)
            + await technicians.CountWithSkillAsync(skill.SkillName, skill.ClientId, cancellationToken);

        if (holders > 0)
        {
            return Results.Conflict(
                new { message = $"{skill.SkillName} is held by {holders} people. Mark it Inactive instead." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Skill",
            skill.Id,
            skill.SkillName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
