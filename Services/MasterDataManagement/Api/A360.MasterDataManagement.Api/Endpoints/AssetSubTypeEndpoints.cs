using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class AssetSubTypeEndpoints
{
    public static RouteGroupBuilder MapAssetSubTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/asset-sub-types")
            .WithTags("AssetSubTypes");

        group.MapGet("", GetAsync).WithName("GetAssetSubTypes");
        group.MapGet("/{id}", GetByIdAsync).WithName("GetAssetSubTypeById");
        group.MapPost("", CreateAsync).WithName("CreateAssetSubType");
        group.MapPut("/{id}", UpdateAsync).WithName("UpdateAssetSubType");
        group.MapPatch("/{id}/status", UpdateStatusAsync).WithName("UpdateAssetSubTypeStatus");
        group.MapDelete("/{id}", DeleteAsync).WithName("DeleteAssetSubType");

        return group;
    }

    private static async Task<IResult> GetAsync(
        string? search,
        string? status,
        string? assetTypeId,
        string? clientId,
        IAssetSubTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var items = await repository.SearchAsync(
            new AssetSubTypeFilter(search, status, assetTypeId, clientId),
            cancellationToken);

        return Results.Ok(items.Select(AssetSubTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetByIdAsync(
        string id,
        IAssetSubTypeRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid asset sub type id." });
        }

        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(AssetSubTypeResponse.FromEntity(item));
    }

    private static async Task<IResult> CreateAsync(
        CreateAssetSubTypeRequest request,
        IAssetSubTypeRepository repository,
        IAssetTypeRepository assetTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var subType = request.ToEntity();

        if (await repository.GetByCodeAsync(subType.Code, subType.ClientId, cancellationToken) is not null)
        {
            return Results.Conflict(new { message = $"An asset sub type with code '{subType.Code}' already exists." });
        }

        var typeError = await ResolveAssetTypeAsync(subType, assetTypes, cancellationToken);
        if (typeError is not null)
        {
            return typeError;
        }

        subType = await repository.CreateAsync(subType, cancellationToken);

        await eventLogger.LogAsync("AssetSubType", subType.Id, subType.SubTypeName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/master-data/asset-sub-types/{subType.Id}",
            AssetSubTypeResponse.FromEntity(subType));
    }

    private static async Task<IResult> UpdateAsync(
        string id,
        UpdateAssetSubTypeRequest request,
        IAssetSubTypeRepository repository,
        IAssetTypeRepository assetTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid asset sub type id." });
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var subType = await repository.GetByIdAsync(id, cancellationToken);
        if (subType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(subType);

        var existing = await repository.GetByCodeAsync(subType.Code, subType.ClientId, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(new { message = $"An asset sub type with code '{subType.Code}' already exists." });
        }

        var typeError = await ResolveAssetTypeAsync(subType, assetTypes, cancellationToken);
        if (typeError is not null)
        {
            return typeError;
        }

        if (!await repository.UpdateAsync(id, subType, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("AssetSubType", subType.Id, subType.SubTypeName, EventAction.Updated, cancellationToken);

        return Results.Ok(AssetSubTypeResponse.FromEntity(subType));
    }

    private static async Task<IResult> UpdateStatusAsync(
        string id,
        UpdateAssetSubTypeStatusRequest request,
        IAssetSubTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid asset sub type id." });
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var subType = await repository.GetByIdAsync(id, cancellationToken);
        if (subType is null)
        {
            return Results.NotFound();
        }

        subType.Status = request.Status;
        subType.UpdatedBy = request.UpdatedBy;
        subType.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(id, subType, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("AssetSubType", subType.Id, subType.SubTypeName, EventAction.Updated, cancellationToken);

        return Results.Ok(AssetSubTypeResponse.FromEntity(subType));
    }

    private static async Task<IResult> DeleteAsync(
        string id,
        IAssetSubTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid asset sub type id." });
        }

        var subType = await repository.GetByIdAsync(id, cancellationToken);
        if (subType is null)
        {
            return Results.NotFound();
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("AssetSubType", subType.Id, subType.SubTypeName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    /// <summary>The parent must be an asset type of this service; its name is stored with the id.</summary>
    private static async Task<IResult?> ResolveAssetTypeAsync(
        A360.MasterDataManagement.Domain.Entities.AssetSubType subType,
        IAssetTypeRepository assetTypes,
        CancellationToken cancellationToken)
    {
        var reference = subType.AssetType;
        if (reference is null || !MongoObjectId.IsValid(reference.Id))
        {
            return Results.BadRequest(new { message = "Asset type is required." });
        }

        var type = await assetTypes.GetByIdAsync(reference.Id!, cancellationToken);
        if (type is null)
        {
            return Results.BadRequest(new { message = $"Asset type '{reference.Id}' was not found." });
        }

        reference.Name = type.AssetTypeName;
        return null;
    }
}
