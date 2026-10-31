using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class PermitTypeEndpoints
{
    public static RouteGroupBuilder MapPermitTypeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/permit-types")
            .WithTags("PermitTypes");

        group.MapGet("", GetPermitTypesAsync)
            .WithName("GetPermitTypes");

        group.MapGet("/{id}", GetPermitTypeByIdAsync)
            .WithName("GetPermitTypeById");

        group.MapPost("", CreatePermitTypeAsync)
            .WithName("CreatePermitType");

        group.MapPut("/{id}", UpdatePermitTypeAsync)
            .WithName("UpdatePermitType");

        group.MapPatch("/{id}/status", UpdatePermitTypeStatusAsync)
            .WithName("UpdatePermitTypeStatus");

        group.MapDelete("/{id}", DeletePermitTypeAsync)
            .WithName("DeletePermitType");

        return group;
    }

    private static async Task<IResult> GetPermitTypesAsync(
        string? search,
        string? status,
        string? category,
        string? clientId,
        IPermitTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var permitTypes = await repository.SearchAsync(
            new PermitTypeFilter(search, status, category, clientId),
            cancellationToken);

        return Results.Ok(
            permitTypes.Select(PermitTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetPermitTypeByIdAsync(
        string id,
        IPermitTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit type id." });
        }

        var permitType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PermitType",
            permitType.Id,
            permitType.PermitTypeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PermitTypeResponse.FromEntity(permitType));
    }

    private static async Task<IResult> CreatePermitTypeAsync(
        CreatePermitTypeRequest request,
        IPermitTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permitType = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            permitType.Code,
            permitType.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A permit type with code '{permitType.Code}' already exists." });
        }

        permitType = await repository.CreateAsync(
            permitType,
            cancellationToken);

        await eventLogger.LogAsync(
            "PermitType",
            permitType.Id,
            permitType.PermitTypeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/permit-types/{permitType.Id}",
            PermitTypeResponse.FromEntity(permitType));
    }

    private static async Task<IResult> UpdatePermitTypeAsync(
        string id,
        UpdatePermitTypeRequest request,
        IPermitTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permitType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(permitType);

        var existing = await repository.GetByCodeAsync(
            permitType.Code,
            permitType.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A permit type with code '{permitType.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            permitType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PermitType",
            permitType.Id,
            permitType.PermitTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PermitTypeResponse.FromEntity(permitType));
    }

    private static async Task<IResult> UpdatePermitTypeStatusAsync(
        string id,
        UpdatePermitTypeStatusRequest request,
        IPermitTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var permitType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitType is null)
        {
            return Results.NotFound();
        }

        permitType.Status = request.Status;
        permitType.UpdatedBy = request.UpdatedBy;
        permitType.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            permitType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PermitType",
            permitType.Id,
            permitType.PermitTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PermitTypeResponse.FromEntity(permitType));
    }

    private static async Task<IResult> DeletePermitTypeAsync(
        string id,
        IPermitTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid permit type id." });
        }

        var permitType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (permitType is null)
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
            "PermitType",
            permitType.Id,
            permitType.PermitTypeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
