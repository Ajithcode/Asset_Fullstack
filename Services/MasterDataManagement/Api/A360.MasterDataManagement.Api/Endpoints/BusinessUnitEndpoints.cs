using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class BusinessUnitEndpoints
{
    public static RouteGroupBuilder MapBusinessUnitEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/business-units")
            .WithTags("BusinessUnits");

        group.MapGet("", GetBusinessUnitsAsync)
            .WithName("GetBusinessUnits");

        group.MapGet("/{id}", GetBusinessUnitByIdAsync)
            .WithName("GetBusinessUnitById");

        group.MapPost("", CreateBusinessUnitAsync)
            .WithName("CreateBusinessUnit");

        group.MapPut("/{id}", UpdateBusinessUnitAsync)
            .WithName("UpdateBusinessUnit");

        group.MapPatch("/{id}/status", UpdateBusinessUnitStatusAsync)
            .WithName("UpdateBusinessUnitStatus");

        group.MapDelete("/{id}", DeleteBusinessUnitAsync)
            .WithName("DeleteBusinessUnit");

        return group;
    }

    private static async Task<IResult> GetBusinessUnitsAsync(
        string? search,
        string? status,
        string? parentOrganization,
        string? department,
        string? site,
        string? clientId,
        IBusinessUnitRepository repository,
        CancellationToken cancellationToken)
    {
        var businessUnits = await repository.SearchAsync(
            new BusinessUnitFilter(search, status, parentOrganization, department, site, clientId),
            cancellationToken);

        return Results.Ok(
            businessUnits.Select(BusinessUnitResponse.FromEntity));
    }

    private static async Task<IResult> GetBusinessUnitByIdAsync(
        string id,
        IBusinessUnitRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid business unit id." });
        }

        var businessUnit = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (businessUnit is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "BusinessUnit",
            businessUnit.Id,
            businessUnit.BusinessUnitName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(BusinessUnitResponse.FromEntity(businessUnit));
    }

    private static async Task<IResult> CreateBusinessUnitAsync(
        CreateBusinessUnitRequest request,
        IBusinessUnitRepository repository,
        IOrganizationRepository organizations,
        IDepartmentRepository departments,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var businessUnit = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            businessUnit.Code,
            businessUnit.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A business unit with code '{businessUnit.Code}' already exists." });
        }

        var parentError = await MasterDataReferenceResolver.ResolveOrganizationAsync(
            businessUnit.ParentOrganization,
            organizations,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        var departmentError = await MasterDataReferenceResolver.ResolveDepartmentsAsync(
            businessUnit.Departments,
            departments,
            cancellationToken);

        if (departmentError is not null)
        {
            return departmentError;
        }

        var siteError = await MasterDataReferenceResolver.ResolveSitesAsync(
            businessUnit.Sites,
            sites,
            cancellationToken);

        if (siteError is not null)
        {
            return siteError;
        }

        businessUnit = await repository.CreateAsync(
            businessUnit,
            cancellationToken);

        await eventLogger.LogAsync(
            "BusinessUnit",
            businessUnit.Id,
            businessUnit.BusinessUnitName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/business-units/{businessUnit.Id}",
            BusinessUnitResponse.FromEntity(businessUnit));
    }

    private static async Task<IResult> UpdateBusinessUnitAsync(
        string id,
        UpdateBusinessUnitRequest request,
        IBusinessUnitRepository repository,
        IOrganizationRepository organizations,
        IDepartmentRepository departments,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid business unit id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var businessUnit = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (businessUnit is null)
        {
            return Results.NotFound();
        }

        var previousName = businessUnit.BusinessUnitName;

        request.ApplyTo(businessUnit);

        var existing = await repository.GetByCodeAsync(
            businessUnit.Code,
            businessUnit.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A business unit with code '{businessUnit.Code}' already exists." });
        }

        var parentError = await MasterDataReferenceResolver.ResolveOrganizationAsync(
            businessUnit.ParentOrganization,
            organizations,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        var departmentError = await MasterDataReferenceResolver.ResolveDepartmentsAsync(
            businessUnit.Departments,
            departments,
            cancellationToken);

        if (departmentError is not null)
        {
            return departmentError;
        }

        var siteError = await MasterDataReferenceResolver.ResolveSitesAsync(
            businessUnit.Sites,
            sites,
            cancellationToken);

        if (siteError is not null)
        {
            return siteError;
        }

        var updated = await repository.UpdateAsync(
            id,
            businessUnit,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != businessUnit.BusinessUnitName)
        {
            await organizations.UpdateBusinessUnitNameAsync(
                id,
                businessUnit.BusinessUnitName,
                cancellationToken);

            await departments.UpdateBusinessUnitNameAsync(
                id,
                businessUnit.BusinessUnitName,
                cancellationToken);

            await sites.UpdateBusinessUnitNameAsync(
                id,
                businessUnit.BusinessUnitName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "BusinessUnit",
            businessUnit.Id,
            businessUnit.BusinessUnitName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BusinessUnitResponse.FromEntity(businessUnit));
    }

    private static async Task<IResult> UpdateBusinessUnitStatusAsync(
        string id,
        UpdateBusinessUnitStatusRequest request,
        IBusinessUnitRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid business unit id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var businessUnit = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (businessUnit is null)
        {
            return Results.NotFound();
        }

        businessUnit.Status = request.Status;
        businessUnit.UpdatedBy = request.UpdatedBy;
        businessUnit.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            businessUnit,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "BusinessUnit",
            businessUnit.Id,
            businessUnit.BusinessUnitName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BusinessUnitResponse.FromEntity(businessUnit));
    }

    private static async Task<IResult> DeleteBusinessUnitAsync(
        string id,
        IBusinessUnitRepository repository,
        IOrganizationRepository organizations,
        IDepartmentRepository departments,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid business unit id." });
        }

        var businessUnit = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (businessUnit is null)
        {
            return Results.NotFound();
        }

        if (await departments.AnyWithBusinessUnitAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Departments still belong to this business unit. Delete or move them first." });
        }

        if (await sites.AnyWithBusinessUnitAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Sites still belong to this business unit. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        // Drop the deleted unit from any organization that listed it.
        await organizations.RemoveBusinessUnitAsync(
            id,
            cancellationToken);

        await eventLogger.LogAsync(
            "BusinessUnit",
            businessUnit.Id,
            businessUnit.BusinessUnitName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
