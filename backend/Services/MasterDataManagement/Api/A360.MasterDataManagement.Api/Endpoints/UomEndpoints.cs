using UomEntity = A360.MasterDataManagement.Domain.Entities.Uom;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class UomEndpoints
{
    public static RouteGroupBuilder MapUomEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/uoms")
            .WithTags("Uoms");

        group.MapGet("", GetUomsAsync)
            .WithName("GetUoms");

        group.MapGet("/{id}", GetUomByIdAsync)
            .WithName("GetUomById");

        group.MapPost("", CreateUomAsync)
            .WithName("CreateUom");

        group.MapPut("/{id}", UpdateUomAsync)
            .WithName("UpdateUom");

        group.MapPatch("/{id}/status", UpdateUomStatusAsync)
            .WithName("UpdateUomStatus");

        group.MapDelete("/{id}", DeleteUomAsync)
            .WithName("DeleteUom");

        return group;
    }

    private static async Task<IResult> GetUomsAsync(
        string? search,
        string? status,
        string? type,
        string? clientId,
        IUomRepository repository,
        CancellationToken cancellationToken)
    {
        var uoms = await repository.SearchAsync(
            new UomFilter(search, status, type, clientId),
            cancellationToken);

        return Results.Ok(
            uoms.Select(UomResponse.FromEntity));
    }

    private static async Task<IResult> GetUomByIdAsync(
        string id,
        IUomRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid unit of measure id." });
        }

        var uom = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (uom is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Uom",
            uom.Id,
            uom.UomName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(UomResponse.FromEntity(uom));
    }

    private static async Task<IResult> CreateUomAsync(
        CreateUomRequest request,
        IUomRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var uom = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            uom.Code,
            uom.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A unit of measure with code '{uom.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            uom,
            repository,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        uom = await repository.CreateAsync(
            uom,
            cancellationToken);

        await eventLogger.LogAsync(
            "Uom",
            uom.Id,
            uom.UomName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/uoms/{uom.Id}",
            UomResponse.FromEntity(uom));
    }

    private static async Task<IResult> UpdateUomAsync(
        string id,
        UpdateUomRequest request,
        IUomRepository repository,
        IItemRepository items,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid unit of measure id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var uom = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (uom is null)
        {
            return Results.NotFound();
        }

        var previousName = uom.UomName;

        request.ApplyTo(uom);

        var existing = await repository.GetByCodeAsync(
            uom.Code,
            uom.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A unit of measure with code '{uom.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            uom,
            repository,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        if (uom.BaseUom is not null && await repository.AnyWithBaseUomAsync(id, cancellationToken))
        {
            return Results.BadRequest(
                new { message = "Other units are based on this one, so it must stay a base unit." });
        }

        var updated = await repository.UpdateAsync(
            id,
            uom,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != uom.UomName)
        {
            await items.UpdateUomNameAsync(
                id,
                uom.UomName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Uom",
            uom.Id,
            uom.UomName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(UomResponse.FromEntity(uom));
    }

    private static async Task<IResult> UpdateUomStatusAsync(
        string id,
        UpdateUomStatusRequest request,
        IUomRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid unit of measure id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var uom = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (uom is null)
        {
            return Results.NotFound();
        }

        uom.Status = request.Status;
        uom.UpdatedBy = request.UpdatedBy;
        uom.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            uom,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Uom",
            uom.Id,
            uom.UomName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(UomResponse.FromEntity(uom));
    }

    private static async Task<IResult> DeleteUomAsync(
        string id,
        IUomRepository repository,
        IItemRepository items,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid unit of measure id." });
        }

        var uom = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (uom is null)
        {
            return Results.NotFound();
        }

        if (await items.AnyWithUomAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Items still use this unit of measure. Change or delete them first." });
        }

        if (await repository.AnyWithBaseUomAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Other units are based on this one. Change their base unit first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Uom",
            uom.Id,
            uom.UomName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the base unit exists, is a base unit itself, has the same type and is not this unit, copying its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        UomEntity uom,
        IUomRepository repository,
        CancellationToken cancellationToken)
    {
        var baseId = uom.BaseUom?.Id;

        if (baseId is null)
        {
            return null;
        }

        if (baseId == uom.Id)
        {
            return Results.BadRequest(
                new { message = "A unit of measure cannot be its own base unit." });
        }

        var baseUom = MongoObjectId.IsValid(baseId)
            ? await repository.GetByIdAsync(baseId, cancellationToken)
            : null;

        if (baseUom is null)
        {
            return Results.BadRequest(
                new { message = $"Unit of measure '{baseId}' was not found." });
        }

        if (baseUom.BaseUom is not null)
        {
            return Results.BadRequest(
                new { message = $"{baseUom.UomName} is based on another unit, so it cannot be a base unit." });
        }

        if (!string.Equals(baseUom.Type, uom.Type, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                new { message = $"The base unit must be a {uom.Type} unit." });
        }

        uom.BaseUom!.Name = baseUom.UomName;
        return null;
    }
}
