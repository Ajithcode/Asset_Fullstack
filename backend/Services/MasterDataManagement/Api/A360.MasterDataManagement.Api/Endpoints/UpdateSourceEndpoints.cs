using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class UpdateSourceEndpoints
{
    public static RouteGroupBuilder MapUpdateSourceEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/update-sources")
            .WithTags("UpdateSources");

        group.MapGet("", GetUpdateSourcesAsync)
            .WithName("GetUpdateSources");

        group.MapGet("/{id}", GetUpdateSourceByIdAsync)
            .WithName("GetUpdateSourceById");

        group.MapPost("", CreateUpdateSourceAsync)
            .WithName("CreateUpdateSource");

        group.MapPut("/{id}", UpdateUpdateSourceAsync)
            .WithName("UpdateUpdateSource");

        group.MapPatch("/{id}/status", UpdateUpdateSourceStatusAsync)
            .WithName("UpdateUpdateSourceStatus");

        group.MapDelete("/{id}", DeleteUpdateSourceAsync)
            .WithName("DeleteUpdateSource");

        return group;
    }

    private static async Task<IResult> GetUpdateSourcesAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        IUpdateSourceRepository repository,
        CancellationToken cancellationToken)
    {
        var updateSources = await repository.SearchAsync(
            new UpdateSourceFilter(search, status, type, clientId),
            cancellationToken);

        return Results.Ok(
            updateSources.Select(UpdateSourceResponse.FromEntity));
    }

    private static async Task<IResult> GetUpdateSourceByIdAsync(
        string id,
        IUpdateSourceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid update source id." });
        }

        var updateSource = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (updateSource is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "UpdateSource",
            updateSource.Id,
            updateSource.SourceName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(UpdateSourceResponse.FromEntity(updateSource));
    }

    private static async Task<IResult> CreateUpdateSourceAsync(
        CreateUpdateSourceRequest request,
        IUpdateSourceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var updateSource = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            updateSource.Code,
            updateSource.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An update source with code '{updateSource.Code}' already exists." });
        }

        updateSource = await repository.CreateAsync(
            updateSource,
            cancellationToken);

        await eventLogger.LogAsync(
            "UpdateSource",
            updateSource.Id,
            updateSource.SourceName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/update-sources/{updateSource.Id}",
            UpdateSourceResponse.FromEntity(updateSource));
    }

    private static async Task<IResult> UpdateUpdateSourceAsync(
        string id,
        UpdateUpdateSourceRequest request,
        IUpdateSourceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid update source id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var updateSource = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (updateSource is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(updateSource);

        var existing = await repository.GetByCodeAsync(
            updateSource.Code,
            updateSource.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An update source with code '{updateSource.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            updateSource,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "UpdateSource",
            updateSource.Id,
            updateSource.SourceName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(UpdateSourceResponse.FromEntity(updateSource));
    }

    private static async Task<IResult> UpdateUpdateSourceStatusAsync(
        string id,
        UpdateUpdateSourceStatusRequest request,
        IUpdateSourceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid update source id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var updateSource = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (updateSource is null)
        {
            return Results.NotFound();
        }

        updateSource.Status = request.Status;
        updateSource.UpdatedBy = request.UpdatedBy;
        updateSource.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            updateSource,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "UpdateSource",
            updateSource.Id,
            updateSource.SourceName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(UpdateSourceResponse.FromEntity(updateSource));
    }

    private static async Task<IResult> DeleteUpdateSourceAsync(
        string id,
        IUpdateSourceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid update source id." });
        }

        var updateSource = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (updateSource is null)
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
            "UpdateSource",
            updateSource.Id,
            updateSource.SourceName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
