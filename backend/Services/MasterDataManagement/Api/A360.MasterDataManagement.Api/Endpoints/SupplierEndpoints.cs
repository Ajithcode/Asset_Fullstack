using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class SupplierEndpoints
{
    public static RouteGroupBuilder MapSupplierEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/suppliers")
            .WithTags("Suppliers");

        group.MapGet("", GetSuppliersAsync)
            .WithName("GetSuppliers");

        group.MapGet("/{id}", GetSupplierByIdAsync)
            .WithName("GetSupplierById");

        group.MapPost("", CreateSupplierAsync)
            .WithName("CreateSupplier");

        group.MapPut("/{id}", UpdateSupplierAsync)
            .WithName("UpdateSupplier");

        group.MapPatch("/{id}/status", UpdateSupplierStatusAsync)
            .WithName("UpdateSupplierStatus");

        group.MapDelete("/{id}", DeleteSupplierAsync)
            .WithName("DeleteSupplier");

        return group;
    }

    private static async Task<IResult> GetSuppliersAsync(
        string? search,
        string? status,
        string? country,
        string? category,
        string? clientId,
        ISupplierRepository repository,
        CancellationToken cancellationToken)
    {
        var suppliers = await repository.SearchAsync(
            new SupplierFilter(search, status, country, category, clientId),
            cancellationToken);

        return Results.Ok(
            suppliers.Select(SupplierResponse.FromEntity));
    }

    private static async Task<IResult> GetSupplierByIdAsync(
        string id,
        ISupplierRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid supplier id." });
        }

        var supplier = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (supplier is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Supplier",
            supplier.Id,
            supplier.SupplierName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SupplierResponse.FromEntity(supplier));
    }

    private static async Task<IResult> CreateSupplierAsync(
        CreateSupplierRequest request,
        ISupplierRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var supplier = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            supplier.Code,
            supplier.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A supplier with code '{supplier.Code}' already exists." });
        }

        supplier = await repository.CreateAsync(
            supplier,
            cancellationToken);

        await eventLogger.LogAsync(
            "Supplier",
            supplier.Id,
            supplier.SupplierName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/suppliers/{supplier.Id}",
            SupplierResponse.FromEntity(supplier));
    }

    private static async Task<IResult> UpdateSupplierAsync(
        string id,
        UpdateSupplierRequest request,
        ISupplierRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid supplier id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var supplier = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (supplier is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(supplier);

        var existing = await repository.GetByCodeAsync(
            supplier.Code,
            supplier.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A supplier with code '{supplier.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            supplier,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Supplier",
            supplier.Id,
            supplier.SupplierName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SupplierResponse.FromEntity(supplier));
    }

    private static async Task<IResult> UpdateSupplierStatusAsync(
        string id,
        UpdateSupplierStatusRequest request,
        ISupplierRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid supplier id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var supplier = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (supplier is null)
        {
            return Results.NotFound();
        }

        supplier.Status = request.Status;
        supplier.UpdatedBy = request.UpdatedBy;
        supplier.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            supplier,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Supplier",
            supplier.Id,
            supplier.SupplierName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SupplierResponse.FromEntity(supplier));
    }

    private static async Task<IResult> DeleteSupplierAsync(
        string id,
        ISupplierRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid supplier id." });
        }

        var supplier = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (supplier is null)
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
            "Supplier",
            supplier.Id,
            supplier.SupplierName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
