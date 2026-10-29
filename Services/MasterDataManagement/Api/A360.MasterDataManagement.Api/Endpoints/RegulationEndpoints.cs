using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class RegulationEndpoints
{
    public static RouteGroupBuilder MapRegulationEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/regulations")
            .WithTags("Regulations");

        group.MapGet("", GetRegulationsAsync)
            .WithName("GetRegulations");

        group.MapGet("/{id}", GetRegulationByIdAsync)
            .WithName("GetRegulationById");

        group.MapPost("", CreateRegulationAsync)
            .WithName("CreateRegulation");

        group.MapPut("/{id}", UpdateRegulationAsync)
            .WithName("UpdateRegulation");

        group.MapPatch("/{id}/status", UpdateRegulationStatusAsync)
            .WithName("UpdateRegulationStatus");

        group.MapDelete("/{id}", DeleteRegulationAsync)
            .WithName("DeleteRegulation");

        return group;
    }

    private static async Task<IResult> GetRegulationsAsync(
        string? search,
        string? status,
        string? country,
        string? category,
        string? clientId,
        IRegulationRepository repository,
        CancellationToken cancellationToken)
    {
        var regulations = await repository.SearchAsync(
            new RegulationFilter(search, status, country, category, clientId),
            cancellationToken);

        return Results.Ok(
            regulations.Select(RegulationResponse.FromEntity));
    }

    private static async Task<IResult> GetRegulationByIdAsync(
        string id,
        IRegulationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid regulation id." });
        }

        var regulation = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (regulation is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Regulation",
            regulation.Id,
            regulation.RegulationName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(RegulationResponse.FromEntity(regulation));
    }

    private static async Task<IResult> CreateRegulationAsync(
        CreateRegulationRequest request,
        IRegulationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var regulation = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            regulation.Code,
            regulation.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A regulation with code '{regulation.Code}' already exists." });
        }

        regulation = await repository.CreateAsync(
            regulation,
            cancellationToken);

        await eventLogger.LogAsync(
            "Regulation",
            regulation.Id,
            regulation.RegulationName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/regulations/{regulation.Id}",
            RegulationResponse.FromEntity(regulation));
    }

    private static async Task<IResult> UpdateRegulationAsync(
        string id,
        UpdateRegulationRequest request,
        IRegulationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid regulation id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var regulation = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (regulation is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(regulation);

        var existing = await repository.GetByCodeAsync(
            regulation.Code,
            regulation.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A regulation with code '{regulation.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            regulation,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Regulation",
            regulation.Id,
            regulation.RegulationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(RegulationResponse.FromEntity(regulation));
    }

    private static async Task<IResult> UpdateRegulationStatusAsync(
        string id,
        UpdateRegulationStatusRequest request,
        IRegulationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid regulation id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var regulation = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (regulation is null)
        {
            return Results.NotFound();
        }

        regulation.Status = request.Status;
        regulation.UpdatedBy = request.UpdatedBy;
        regulation.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            regulation,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Regulation",
            regulation.Id,
            regulation.RegulationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(RegulationResponse.FromEntity(regulation));
    }

    private static async Task<IResult> DeleteRegulationAsync(
        string id,
        IRegulationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid regulation id." });
        }

        var regulation = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (regulation is null)
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
            "Regulation",
            regulation.Id,
            regulation.RegulationName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
