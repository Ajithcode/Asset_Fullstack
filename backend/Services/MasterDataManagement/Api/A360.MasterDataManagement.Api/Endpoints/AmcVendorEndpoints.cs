using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class AmcVendorEndpoints
{
    public static RouteGroupBuilder MapAmcVendorEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/amc-vendors")
            .WithTags("AmcVendors");

        group.MapGet("", GetAmcVendorsAsync)
            .WithName("GetAmcVendors");

        group.MapGet("/{id}", GetAmcVendorByIdAsync)
            .WithName("GetAmcVendorById");

        group.MapPost("", CreateAmcVendorAsync)
            .WithName("CreateAmcVendor");

        group.MapPut("/{id}", UpdateAmcVendorAsync)
            .WithName("UpdateAmcVendor");

        group.MapPatch("/{id}/status", UpdateAmcVendorStatusAsync)
            .WithName("UpdateAmcVendorStatus");

        group.MapDelete("/{id}", DeleteAmcVendorAsync)
            .WithName("DeleteAmcVendor");

        return group;
    }

    private static async Task<IResult> GetAmcVendorsAsync(
        string? search,
        string? status,
        string? assetTypeId,
        string? contractType,
        DateOnly? activeOn,
        DateOnly? endsBy,
        string? clientId,
        IAmcVendorRepository repository,
        CancellationToken cancellationToken)
    {
        var amcVendors = await repository.SearchAsync(
            new AmcVendorFilter(search, status, assetTypeId, contractType, activeOn, endsBy, clientId),
            cancellationToken);

        return Results.Ok(
            amcVendors.Select(AmcVendorResponse.FromEntity));
    }

    private static async Task<IResult> GetAmcVendorByIdAsync(
        string id,
        IAmcVendorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid AMC vendor id." });
        }

        var amcVendor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (amcVendor is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AmcVendor",
            amcVendor.Id,
            amcVendor.AmcVendorName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AmcVendorResponse.FromEntity(amcVendor));
    }

    private static async Task<IResult> CreateAmcVendorAsync(
        CreateAmcVendorRequest request,
        IAmcVendorRepository repository,
        IAssetTypeRepository assetTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var amcVendor = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            amcVendor.Code,
            amcVendor.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An AMC vendor with code '{amcVendor.Code}' already exists." });
        }

        var assetTypeError = await MasterDataReferenceResolver.ResolveAssetTypeAsync(
            amcVendor.AssetType,
            assetTypes,
            cancellationToken);

        if (assetTypeError is not null)
        {
            return assetTypeError;
        }

        amcVendor = await repository.CreateAsync(
            amcVendor,
            cancellationToken);

        await eventLogger.LogAsync(
            "AmcVendor",
            amcVendor.Id,
            amcVendor.AmcVendorName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/amc-vendors/{amcVendor.Id}",
            AmcVendorResponse.FromEntity(amcVendor));
    }

    private static async Task<IResult> UpdateAmcVendorAsync(
        string id,
        UpdateAmcVendorRequest request,
        IAmcVendorRepository repository,
        IAssetTypeRepository assetTypes,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid AMC vendor id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var amcVendor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (amcVendor is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(amcVendor);

        var existing = await repository.GetByCodeAsync(
            amcVendor.Code,
            amcVendor.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An AMC vendor with code '{amcVendor.Code}' already exists." });
        }

        var assetTypeError = await MasterDataReferenceResolver.ResolveAssetTypeAsync(
            amcVendor.AssetType,
            assetTypes,
            cancellationToken);

        if (assetTypeError is not null)
        {
            return assetTypeError;
        }

        var updated = await repository.UpdateAsync(
            id,
            amcVendor,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AmcVendor",
            amcVendor.Id,
            amcVendor.AmcVendorName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AmcVendorResponse.FromEntity(amcVendor));
    }

    private static async Task<IResult> UpdateAmcVendorStatusAsync(
        string id,
        UpdateAmcVendorStatusRequest request,
        IAmcVendorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid AMC vendor id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var amcVendor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (amcVendor is null)
        {
            return Results.NotFound();
        }

        amcVendor.Status = request.Status;
        amcVendor.UpdatedBy = request.UpdatedBy;
        amcVendor.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            amcVendor,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AmcVendor",
            amcVendor.Id,
            amcVendor.AmcVendorName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AmcVendorResponse.FromEntity(amcVendor));
    }

    private static async Task<IResult> DeleteAmcVendorAsync(
        string id,
        IAmcVendorRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid AMC vendor id." });
        }

        var amcVendor = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (amcVendor is null)
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
            "AmcVendor",
            amcVendor.Id,
            amcVendor.AmcVendorName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
