using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class StandardEndpoints
{
    public static RouteGroupBuilder MapStandardEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/standards")
            .WithTags("Standards");

        group.MapGet("", GetStandardsAsync)
            .WithName("GetStandards");

        group.MapGet("/{id}", GetStandardByIdAsync)
            .WithName("GetStandardById");

        group.MapPost("", CreateStandardAsync)
            .WithName("CreateStandard");

        group.MapPut("/{id}", UpdateStandardAsync)
            .WithName("UpdateStandard");

        group.MapPatch("/{id}/status", UpdateStandardStatusAsync)
            .WithName("UpdateStandardStatus");

        group.MapDelete("/{id}", DeleteStandardAsync)
            .WithName("DeleteStandard");

        return group;
    }

    private static async Task<IResult> GetStandardsAsync(
        string? search,
        string? status,
        string? organization,
        string? category,
        string? clientId,
        IStandardRepository repository,
        CancellationToken cancellationToken)
    {
        var standards = await repository.SearchAsync(
            new StandardFilter(search, status, organization, category, clientId),
            cancellationToken);

        return Results.Ok(
            standards.Select(StandardResponse.FromEntity));
    }

    private static async Task<IResult> GetStandardByIdAsync(
        string id,
        IStandardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid standard id." });
        }

        var standard = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (standard is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Standard",
            standard.Id,
            standard.StandardName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(StandardResponse.FromEntity(standard));
    }

    private static async Task<IResult> CreateStandardAsync(
        CreateStandardRequest request,
        IStandardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var standard = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            standard.Code,
            standard.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A standard with code '{standard.Code}' already exists." });
        }

        standard = await repository.CreateAsync(
            standard,
            cancellationToken);

        await eventLogger.LogAsync(
            "Standard",
            standard.Id,
            standard.StandardName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/standards/{standard.Id}",
            StandardResponse.FromEntity(standard));
    }

    private static async Task<IResult> UpdateStandardAsync(
        string id,
        UpdateStandardRequest request,
        IStandardRepository repository,
        ICertificationTypeRepository certificationTypes,
        IRegulationRepository regulations,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid standard id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var standard = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (standard is null)
        {
            return Results.NotFound();
        }

        var previousCode = standard.Code;

        request.ApplyTo(standard);

        var existing = await repository.GetByCodeAsync(
            standard.Code,
            standard.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A standard with code '{standard.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            standard,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        // Certification types and regulations point at the standard by code
        if (previousCode != standard.Code)
        {
            await certificationTypes.UpdateStandardCodeAsync(
                previousCode,
                standard.Code,
                standard.ClientId,
                cancellationToken);

            await regulations.UpdateStandardCodeAsync(
                previousCode,
                standard.Code,
                standard.ClientId,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Standard",
            standard.Id,
            standard.StandardName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StandardResponse.FromEntity(standard));
    }

    private static async Task<IResult> UpdateStandardStatusAsync(
        string id,
        UpdateStandardStatusRequest request,
        IStandardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid standard id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var standard = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (standard is null)
        {
            return Results.NotFound();
        }

        standard.Status = request.Status;
        standard.UpdatedBy = request.UpdatedBy;
        standard.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            standard,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Standard",
            standard.Id,
            standard.StandardName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StandardResponse.FromEntity(standard));
    }

    private static async Task<IResult> DeleteStandardAsync(
        string id,
        IStandardRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid standard id." });
        }

        var standard = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (standard is null)
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
            "Standard",
            standard.Id,
            standard.StandardName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
