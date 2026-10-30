using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class CertificationTypeEndpoints
{
    public static RouteGroupBuilder MapCertificationTypeEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/certification-types")
            .WithTags("CertificationTypes");

        group.MapGet("", GetCertificationTypesAsync)
            .WithName("GetCertificationTypes");

        group.MapGet("/{id}", GetCertificationTypeByIdAsync)
            .WithName("GetCertificationTypeById");

        group.MapPost("", CreateCertificationTypeAsync)
            .WithName("CreateCertificationType");

        group.MapPut("/{id}", UpdateCertificationTypeAsync)
            .WithName("UpdateCertificationType");

        group.MapPatch("/{id}/status", UpdateCertificationTypeStatusAsync)
            .WithName("UpdateCertificationTypeStatus");

        group.MapDelete("/{id}", DeleteCertificationTypeAsync)
            .WithName("DeleteCertificationType");

        return group;
    }

    private static async Task<IResult> GetCertificationTypesAsync(
        string? search,
        string? status,
        string? category,
        string? applicableTo,
        string? clientId,
        ICertificationTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var certificationTypes = await repository.SearchAsync(
            new CertificationTypeFilter(search, status, category, applicableTo, clientId),
            cancellationToken);

        return Results.Ok(
            certificationTypes.Select(CertificationTypeResponse.FromEntity));
    }

    private static async Task<IResult> GetCertificationTypeByIdAsync(
        string id,
        ICertificationTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid certification type id." });
        }

        var certificationType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (certificationType is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "CertificationType",
            certificationType.Id,
            certificationType.CertificationTypeName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(CertificationTypeResponse.FromEntity(certificationType));
    }

    private static async Task<IResult> CreateCertificationTypeAsync(
        CreateCertificationTypeRequest request,
        ICertificationTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var certificationType = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            certificationType.Code,
            certificationType.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A certification type with code '{certificationType.Code}' already exists." });
        }

        certificationType = await repository.CreateAsync(
            certificationType,
            cancellationToken);

        await eventLogger.LogAsync(
            "CertificationType",
            certificationType.Id,
            certificationType.CertificationTypeName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/certification-types/{certificationType.Id}",
            CertificationTypeResponse.FromEntity(certificationType));
    }

    private static async Task<IResult> UpdateCertificationTypeAsync(
        string id,
        UpdateCertificationTypeRequest request,
        ICertificationTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid certification type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var certificationType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (certificationType is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(certificationType);

        var existing = await repository.GetByCodeAsync(
            certificationType.Code,
            certificationType.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A certification type with code '{certificationType.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            certificationType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "CertificationType",
            certificationType.Id,
            certificationType.CertificationTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CertificationTypeResponse.FromEntity(certificationType));
    }

    private static async Task<IResult> UpdateCertificationTypeStatusAsync(
        string id,
        UpdateCertificationTypeStatusRequest request,
        ICertificationTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid certification type id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var certificationType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (certificationType is null)
        {
            return Results.NotFound();
        }

        certificationType.Status = request.Status;
        certificationType.UpdatedBy = request.UpdatedBy;
        certificationType.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            certificationType,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "CertificationType",
            certificationType.Id,
            certificationType.CertificationTypeName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CertificationTypeResponse.FromEntity(certificationType));
    }

    private static async Task<IResult> DeleteCertificationTypeAsync(
        string id,
        ICertificationTypeRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid certification type id." });
        }

        var certificationType = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (certificationType is null)
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
            "CertificationType",
            certificationType.Id,
            certificationType.CertificationTypeName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
