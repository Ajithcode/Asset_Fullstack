using ManufacturerEntity = A360.MasterDataManagement.Domain.Entities.Manufacturer;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ManufacturerEndpoints
{
    public static RouteGroupBuilder MapManufacturerEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/manufacturers")
            .WithTags("Manufacturers");

        group.MapGet("", GetManufacturersAsync)
            .WithName("GetManufacturers");

        group.MapGet("/{id}", GetManufacturerByIdAsync)
            .WithName("GetManufacturerById");

        group.MapPost("", CreateManufacturerAsync)
            .WithName("CreateManufacturer");

        group.MapPut("/{id}", UpdateManufacturerAsync)
            .WithName("UpdateManufacturer");

        group.MapPatch("/{id}/status", UpdateManufacturerStatusAsync)
            .WithName("UpdateManufacturerStatus");

        group.MapDelete("/{id}", DeleteManufacturerAsync)
            .WithName("DeleteManufacturer");

        return group;
    }

    private static async Task<IResult> GetManufacturersAsync(
        string? search,
        string? status,
        string? country,
        string? clientId,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        CancellationToken cancellationToken)
    {
        var manufacturers = await repository.SearchAsync(
            new ManufacturerFilter(search, status, country, clientId),
            cancellationToken);

        // One query for the models of every manufacturer in the page.
        var models = (await assetModels.GetByManufacturersAsync(
                manufacturers.Select(x => x.Id),
                cancellationToken))
            .ToLookup(x => x.Manufacturer!.Id);

        return Results.Ok(
            manufacturers.Select(x => ManufacturerResponse.FromEntity(x, ToModelList(models[x.Id]))));
    }

    private static async Task<IResult> GetManufacturerByIdAsync(
        string id,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid manufacturer id." });
        }

        var manufacturer = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (manufacturer is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Manufacturer",
            manufacturer.Id,
            manufacturer.ManufacturerName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(manufacturer, assetModels, cancellationToken));
    }

    private static async Task<IResult> CreateManufacturerAsync(
        CreateManufacturerRequest request,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var manufacturer = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            manufacturer.Code,
            manufacturer.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A manufacturer with code '{manufacturer.Code}' already exists." });
        }

        var modelIds = request.Models?.Select(x => x.Id!.Trim()).ToList();

        var modelsError = await CheckModelsExistAsync(
            modelIds,
            assetModels,
            cancellationToken);

        if (modelsError is not null)
        {
            return modelsError;
        }

        manufacturer = await repository.CreateAsync(
            manufacturer,
            cancellationToken);

        if (modelIds is not null)
        {
            await assetModels.SetManufacturerModelsAsync(
                manufacturer.Id,
                manufacturer.ManufacturerName,
                modelIds,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Manufacturer",
            manufacturer.Id,
            manufacturer.ManufacturerName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/manufacturers/{manufacturer.Id}",
            await ToResponseAsync(manufacturer, assetModels, cancellationToken));
    }

    private static async Task<IResult> UpdateManufacturerAsync(
        string id,
        UpdateManufacturerRequest request,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid manufacturer id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var manufacturer = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (manufacturer is null)
        {
            return Results.NotFound();
        }

        var previousName = manufacturer.ManufacturerName;

        request.ApplyTo(manufacturer);

        var existing = await repository.GetByCodeAsync(
            manufacturer.Code,
            manufacturer.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A manufacturer with code '{manufacturer.Code}' already exists." });
        }

        var modelIds = request.Models?.Select(x => x.Id!.Trim()).ToList();

        var modelsError = await CheckModelsExistAsync(
            modelIds,
            assetModels,
            cancellationToken);

        if (modelsError is not null)
        {
            return modelsError;
        }

        var updated = await repository.UpdateAsync(
            id,
            manufacturer,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (modelIds is not null)
        {
            await assetModels.SetManufacturerModelsAsync(
                id,
                manufacturer.ManufacturerName,
                modelIds,
                cancellationToken);
        }
        else if (previousName != manufacturer.ManufacturerName)
        {
            await assetModels.UpdateManufacturerNameAsync(
                id,
                manufacturer.ManufacturerName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Manufacturer",
            manufacturer.Id,
            manufacturer.ManufacturerName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(manufacturer, assetModels, cancellationToken));
    }

    private static async Task<IResult> UpdateManufacturerStatusAsync(
        string id,
        UpdateManufacturerStatusRequest request,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid manufacturer id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var manufacturer = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (manufacturer is null)
        {
            return Results.NotFound();
        }

        manufacturer.Status = request.Status;
        manufacturer.UpdatedBy = request.UpdatedBy;
        manufacturer.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            manufacturer,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Manufacturer",
            manufacturer.Id,
            manufacturer.ManufacturerName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(manufacturer, assetModels, cancellationToken));
    }

    private static async Task<IResult> DeleteManufacturerAsync(
        string id,
        IManufacturerRepository repository,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid manufacturer id." });
        }

        var manufacturer = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (manufacturer is null)
        {
            return Results.NotFound();
        }

        if (await assetModels.AnyWithManufacturerAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Asset models still belong to this manufacturer. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Manufacturer",
            manufacturer.Id,
            manufacturer.ManufacturerName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult?> CheckModelsExistAsync(
        IReadOnlyCollection<string>? modelIds,
        IAssetModelRepository assetModels,
        CancellationToken cancellationToken)
    {
        if (modelIds is null || modelIds.Count == 0)
        {
            return null;
        }

        var invalid = modelIds.FirstOrDefault(x => !MongoObjectId.IsValid(x));

        if (invalid is null)
        {
            var found = (await assetModels.GetByIdsAsync(modelIds, cancellationToken))
                .Select(x => x.Id)
                .ToHashSet();

            invalid = modelIds.FirstOrDefault(x => !found.Contains(x));
        }

        return invalid is null
            ? null
            : Results.BadRequest(
                new { message = $"Asset model '{invalid}' was not found." });
    }

    private static async Task<ManufacturerResponse> ToResponseAsync(
        ManufacturerEntity manufacturer,
        IAssetModelRepository assetModels,
        CancellationToken cancellationToken)
    {
        var models = await assetModels.GetByManufacturersAsync(
            [manufacturer.Id],
            cancellationToken);

        return ManufacturerResponse.FromEntity(manufacturer, ToModelList(models));
    }

    private static IReadOnlyList<MasterDataReferenceDto> ToModelList(
        IEnumerable<A360.MasterDataManagement.Domain.Entities.AssetModel> models)
    {
        return models
            .Select(x => new MasterDataReferenceDto(x.Id, x.ModelName))
            .ToList();
    }
}
