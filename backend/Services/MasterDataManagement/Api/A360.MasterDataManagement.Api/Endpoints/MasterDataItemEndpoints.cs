using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class MasterDataItemEndpoints
{
    public static RouteGroupBuilder MapMasterDataItemEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data-items")
            .WithTags("MasterDataItems");

        group.MapGet("", GetMasterDataItemsAsync)
            .WithName("GetMasterDataItems");

        group.MapGet("/{id}", GetMasterDataItemByIdAsync)
            .WithName("GetMasterDataItemById");

        group.MapGet("/type/{masterType}", GetMasterDataItemsByMasterTypeAsync)
            .WithName("GetMasterDataItemsByMasterType");

        group.MapPost("", CreateMasterDataItemAsync)
            .WithName("CreateMasterDataItem");

        group.MapPut("/{id}", UpdateMasterDataItemAsync)
            .WithName("UpdateMasterDataItem");

        group.MapDelete("/{id}", DeleteMasterDataItemAsync)
            .WithName("DeleteMasterDataItem");

        return group;
    }

    private static async Task<IResult> GetMasterDataItemsAsync(
        IMasterDataItemRepository repository,
        CancellationToken cancellationToken)
    {
        var masterDataItems = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            masterDataItems.Select(MasterDataItemResponse.FromEntity));
    }

    private static async Task<IResult> GetMasterDataItemByIdAsync(
        string id,
        IMasterDataItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid master data item id." });
        }

        var masterDataItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (masterDataItem is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MasterDataItem",
            masterDataItem.Id,
            masterDataItem.Name,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(MasterDataItemResponse.FromEntity(masterDataItem));
    }

    private static async Task<IResult> GetMasterDataItemsByMasterTypeAsync(
        string masterType,
        IMasterDataItemRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(masterType))
        {
            return Results.BadRequest(
                new { message = "Master type is required." });
        }

        var masterDataItems = await repository.GetByMasterTypeAsync(
            masterType,
            cancellationToken);

        return Results.Ok(
            masterDataItems.Select(MasterDataItemResponse.FromEntity));
    }

    private static async Task<IResult> CreateMasterDataItemAsync(
        CreateMasterDataItemRequest request,
        IMasterDataItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var masterDataItem = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "MasterDataItem",
            masterDataItem.Id,
            masterDataItem.Name,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data-items/{masterDataItem.Id}",
            MasterDataItemResponse.FromEntity(masterDataItem));
    }

    private static async Task<IResult> UpdateMasterDataItemAsync(
        string id,
        UpdateMasterDataItemRequest request,
        IMasterDataItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid master data item id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var masterDataItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (masterDataItem is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(masterDataItem);

        var updated = await repository.UpdateAsync(
            id,
            masterDataItem,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MasterDataItem",
            masterDataItem.Id,
            masterDataItem.Name,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(MasterDataItemResponse.FromEntity(masterDataItem));
    }

    private static async Task<IResult> DeleteMasterDataItemAsync(
        string id,
        IMasterDataItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid master data item id." });
        }

        var masterDataItem = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (masterDataItem is null)
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
            "MasterDataItem",
            masterDataItem.Id,
            masterDataItem.Name,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
