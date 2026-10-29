using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class OrganizationEndpoints
{
    // Guards the parent-chain walk against corrupt data that already contains a loop.
    private const int MaxHierarchyDepth = 100;

    public static RouteGroupBuilder MapOrganizationEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/organizations")
            .WithTags("Organizations");

        group.MapGet("", GetOrganizationsAsync)
            .WithName("GetOrganizations");

        group.MapGet("/{id}", GetOrganizationByIdAsync)
            .WithName("GetOrganizationById");

        group.MapGet("/{id}/children", GetChildOrganizationsAsync)
            .WithName("GetChildOrganizations");

        group.MapPost("", CreateOrganizationAsync)
            .WithName("CreateOrganization");

        group.MapPut("/{id}", UpdateOrganizationAsync)
            .WithName("UpdateOrganization");

        group.MapPatch("/{id}/status", UpdateOrganizationStatusAsync)
            .WithName("UpdateOrganizationStatus");

        group.MapDelete("/{id}", DeleteOrganizationAsync)
            .WithName("DeleteOrganization");

        return group;
    }

    private static async Task<IResult> GetOrganizationsAsync(
        string? search,
        string? type,
        string? status,
        string? parentOrganizationId,
        string? businessUnit,
        string? site,
        string? clientId,
        IOrganizationRepository repository,
        CancellationToken cancellationToken)
    {
        var organizations = await repository.SearchAsync(
            new OrganizationFilter(search, type, status, parentOrganizationId, businessUnit, site, clientId),
            cancellationToken);

        return Results.Ok(
            organizations.Select(OrganizationResponse.FromEntity));
    }

    private static async Task<IResult> GetOrganizationByIdAsync(
        string id,
        IOrganizationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid organization id." });
        }

        var organization = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Organization",
            organization.Id,
            organization.OrganizationName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(OrganizationResponse.FromEntity(organization));
    }

    private static async Task<IResult> GetChildOrganizationsAsync(
        string id,
        IOrganizationRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid organization id." });
        }

        var children = await repository.GetChildrenAsync(
            id,
            cancellationToken);

        return Results.Ok(
            children.Select(OrganizationResponse.FromEntity));
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        IOrganizationRepository repository,
        IBusinessUnitRepository businessUnits,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var organization = request.ToEntity();

        var existing = await repository.GetByNameAsync(
            organization.OrganizationName,
            organization.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An organization named '{organization.OrganizationName}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            organization,
            repository,
            businessUnits,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        organization = await repository.CreateAsync(
            organization,
            cancellationToken);

        await eventLogger.LogAsync(
            "Organization",
            organization.Id,
            organization.OrganizationName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/organizations/{organization.Id}",
            OrganizationResponse.FromEntity(organization));
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        string id,
        UpdateOrganizationRequest request,
        IOrganizationRepository repository,
        IBusinessUnitRepository businessUnits,
        ISiteRepository sites,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid organization id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var organization = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        var previousName = organization.OrganizationName;

        request.ApplyTo(organization);

        var existing = await repository.GetByNameAsync(
            organization.OrganizationName,
            organization.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An organization named '{organization.OrganizationName}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            organization,
            repository,
            businessUnits,
            sites,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            organization,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != organization.OrganizationName)
        {
            await repository.UpdateParentOrganizationNameAsync(
                id,
                organization.OrganizationName,
                cancellationToken);

            await businessUnits.UpdateParentOrganizationNameAsync(
                id,
                organization.OrganizationName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Organization",
            organization.Id,
            organization.OrganizationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(OrganizationResponse.FromEntity(organization));
    }

    private static async Task<IResult> UpdateOrganizationStatusAsync(
        string id,
        UpdateOrganizationStatusRequest request,
        IOrganizationRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid organization id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var organization = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        organization.Status = request.Status;
        organization.UpdatedBy = request.UpdatedBy;
        organization.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            organization,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Organization",
            organization.Id,
            organization.OrganizationName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(OrganizationResponse.FromEntity(organization));
    }

    private static async Task<IResult> DeleteOrganizationAsync(
        string id,
        IOrganizationRepository repository,
        IBusinessUnitRepository businessUnits,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid organization id." });
        }

        var organization = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        if (await repository.HasChildrenAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This organization has child organizations. Delete or move them first." });
        }

        if (await businessUnits.AnyWithParentOrganizationAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Business units still belong to this organization. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Organization",
            organization.Id,
            organization.OrganizationName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the parent organization (exists, no loop) and any business units and sites given by id,
    /// copying their current names onto the organization. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        OrganizationEntity organization,
        IOrganizationRepository repository,
        IBusinessUnitRepository businessUnits,
        ISiteRepository sites,
        CancellationToken cancellationToken)
    {
        var parentId = organization.ParentOrganization?.Id;

        if (parentId is not null)
        {
            if (parentId == organization.Id)
            {
                return Results.BadRequest(
                    new { message = "An organization cannot be its own parent." });
            }

            var parentError = await MasterDataReferenceResolver.ResolveOrganizationAsync(
                organization.ParentOrganization,
                repository,
                cancellationToken);

            if (parentError is not null)
            {
                return parentError;
            }

            // New organizations have no id yet, so they cannot appear in any existing chain.
            if (!string.IsNullOrEmpty(organization.Id))
            {
                var ancestor = await repository.GetByIdAsync(parentId, cancellationToken);

                for (var depth = 0; ancestor?.ParentOrganization?.Id is not null && depth < MaxHierarchyDepth; depth++)
                {
                    if (ancestor.ParentOrganization.Id == organization.Id)
                    {
                        return Results.BadRequest(
                            new { message = "Parent organization cannot be one of this organization's own children." });
                    }

                    ancestor = await repository.GetByIdAsync(
                        ancestor.ParentOrganization.Id,
                        cancellationToken);
                }
            }
        }

        var businessUnitError = await MasterDataReferenceResolver.ResolveBusinessUnitsAsync(
            organization.BusinessUnits,
            businessUnits,
            cancellationToken);

        if (businessUnitError is not null)
        {
            return businessUnitError;
        }

        return await MasterDataReferenceResolver.ResolveSitesAsync(
            organization.Sites,
            sites,
            cancellationToken);
    }
}
