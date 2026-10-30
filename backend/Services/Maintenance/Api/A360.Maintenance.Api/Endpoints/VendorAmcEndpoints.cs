
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class VendorAmcEndpoints
{
    public static RouteGroupBuilder MapVendorAmcEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/vendor-amc")
            .WithTags("VendorAmc");

        group.MapGet("", GetVendorAmcsAsync)
            .WithName("GetVendorAmcs");

        group.MapGet("/{id}", GetVendorAmcByIdAsync)
            .WithName("GetVendorAmcById");

        group.MapGet("/asset/{assetId}", GetVendorAmcsByAssetIdAsync)
            .WithName("GetVendorAmcsByAssetId");

        group.MapPost("", CreateVendorAmcAsync)
            .WithName("CreateVendorAmc");

        group.MapPut("/{id}", UpdateVendorAmcAsync)
            .WithName("UpdateVendorAmc");

        group.MapDelete("/{id}", DeleteVendorAmcAsync)
            .WithName("DeleteVendorAmc");

        return group;
    }

    private static async Task<IResult> GetVendorAmcsAsync(
        IVendorAmcRepository repository,
        CancellationToken cancellationToken)
    {
        var vendorAmcs = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            vendorAmcs.Select(VendorAmcResponse.FromEntity));
    }

    private static async Task<IResult> GetVendorAmcByIdAsync(
        string id,
        IVendorAmcRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid vendoramc id." });
        }

        var vendorAmc = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (vendorAmc is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "VendorAmc",
            vendorAmc.Id,
            vendorAmc.VendorName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(VendorAmcResponse.FromEntity(vendorAmc));
    }

    private static async Task<IResult> GetVendorAmcsByAssetIdAsync(
        string assetId,
        IVendorAmcRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var vendorAmcs = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            vendorAmcs.Select(VendorAmcResponse.FromEntity));
    }

    private static async Task<IResult> CreateVendorAmcAsync(
        CreateVendorAmcRequest request,
        IVendorAmcRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var vendorAmc = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "VendorAmc",
            vendorAmc.Id,
            vendorAmc.VendorName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/vendor-amc/{vendorAmc.Id}",
            VendorAmcResponse.FromEntity(vendorAmc));
    }

    private static async Task<IResult> UpdateVendorAmcAsync(
        string id,
        UpdateVendorAmcRequest request,
        IVendorAmcRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid vendoramc id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var vendorAmc = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (vendorAmc is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(vendorAmc);

        var updated = await repository.UpdateAsync(
            id,
            vendorAmc,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "VendorAmc",
            vendorAmc.Id,
            vendorAmc.VendorName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(VendorAmcResponse.FromEntity(vendorAmc));
    }

    private static async Task<IResult> DeleteVendorAmcAsync(
        string id,
        IVendorAmcRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid vendoramc id." });
        }

        var vendorAmc = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (vendorAmc is null)
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
            "VendorAmc",
            vendorAmc.Id,
            vendorAmc.VendorName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
