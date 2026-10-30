using SiteEntity = A360.MasterDataManagement.Domain.Entities.Site;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class SiteEndpoints
{
    public static RouteGroupBuilder MapSiteEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/sites")
            .WithTags("Sites");

        group.MapGet("", GetSitesAsync)
            .WithName("GetSites");

        group.MapGet("/{id}", GetSiteByIdAsync)
            .WithName("GetSiteById");

        group.MapPost("", CreateSiteAsync)
            .WithName("CreateSite");

        group.MapPut("/{id}", UpdateSiteAsync)
            .WithName("UpdateSite");

        group.MapPatch("/{id}/status", UpdateSiteStatusAsync)
            .WithName("UpdateSiteStatus");

        group.MapDelete("/{id}", DeleteSiteAsync)
            .WithName("DeleteSite");

        return group;
    }

    private static async Task<IResult> GetSitesAsync(
        string? search,
        string? status,
        string? siteType,
        string? city,
        string? country,
        string? businessUnitId,
        string? departmentId,
        string? clientId,
        ISiteRepository repository,
        CancellationToken cancellationToken)
    {
        var sites = await repository.SearchAsync(
            new SiteFilter(search, status, siteType, city, country, businessUnitId, departmentId, clientId),
            cancellationToken);

        return Results.Ok(
            sites.Select(SiteResponse.FromEntity));
    }

    private static async Task<IResult> GetSiteByIdAsync(
        string id,
        ISiteRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid site id." });
        }

        var site = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (site is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Site",
            site.Id,
            site.SiteName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SiteResponse.FromEntity(site));
    }

    private static async Task<IResult> CreateSiteAsync(
        CreateSiteRequest request,
        ISiteRepository repository,
        IBusinessUnitRepository businessUnits,
        IDepartmentRepository departments,
        IOrganizationRepository organizations,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var site = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            site.Code,
            site.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A site with code '{site.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            site,
            businessUnits,
            departments,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var structureError = await ResolveStructureAsync(
            site,
            repository,
            organizations,
            cancellationToken);

        if (structureError is not null)
        {
            return structureError;
        }

        site = await repository.CreateAsync(
            site,
            cancellationToken);

        await eventLogger.LogAsync(
            "Site",
            site.Id,
            site.SiteName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/sites/{site.Id}",
            SiteResponse.FromEntity(site));
    }

    private static async Task<IResult> UpdateSiteAsync(
        string id,
        UpdateSiteRequest request,
        ISiteRepository repository,
        IBusinessUnitRepository businessUnits,
        IDepartmentRepository departments,
        IOrganizationRepository organizations,
        IBuildingRepository buildings,
        IFloorRepository floors,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid site id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var site = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (site is null)
        {
            return Results.NotFound();
        }

        var previousName = site.SiteName;

        request.ApplyTo(site);

        var existing = await repository.GetByCodeAsync(
            site.Code,
            site.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A site with code '{site.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            site,
            businessUnits,
            departments,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var structureError = await ResolveStructureAsync(
            site,
            repository,
            organizations,
            cancellationToken);

        if (structureError is not null)
        {
            return structureError;
        }

        var updated = await repository.UpdateAsync(
            id,
            site,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != site.SiteName)
        {
            await businessUnits.UpdateSiteNameAsync(
                id,
                site.SiteName,
                cancellationToken);

            await organizations.UpdateSiteNameAsync(
                id,
                site.SiteName,
                cancellationToken);

            await buildings.UpdateSiteNameAsync(
                id,
                site.SiteName,
                cancellationToken);

            await floors.UpdateSiteNameAsync(
                id,
                site.SiteName,
                cancellationToken);

            await zones.UpdateSiteNameAsync(
                id,
                site.SiteName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Site",
            site.Id,
            site.SiteName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SiteResponse.FromEntity(site));
    }

    private static async Task<IResult> UpdateSiteStatusAsync(
        string id,
        UpdateSiteStatusRequest request,
        ISiteRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid site id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var site = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (site is null)
        {
            return Results.NotFound();
        }

        site.Status = request.Status;
        site.UpdatedBy = request.UpdatedBy;
        site.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            site,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Site",
            site.Id,
            site.SiteName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SiteResponse.FromEntity(site));
    }

    private static async Task<IResult> DeleteSiteAsync(
        string id,
        ISiteRepository repository,
        IBusinessUnitRepository businessUnits,
        IOrganizationRepository organizations,
        IBuildingRepository buildings,
        IFloorRepository floors,
        IZoneRepository zones,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid site id." });
        }

        var site = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (site is null)
        {
            return Results.NotFound();
        }

        if (await buildings.AnyWithSiteAsync(id, cancellationToken)
            || await floors.AnyWithSiteAsync(id, cancellationToken)
            || await zones.AnyWithSiteAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Buildings, floors or zones are still on this site. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        // Drop the deleted site from any business unit or organization that listed it.
        await businessUnits.RemoveSiteAsync(
            id,
            cancellationToken);

        await organizations.RemoveSiteAsync(
            id,
            cancellationToken);

        await eventLogger.LogAsync(
            "Site",
            site.Id,
            site.SiteName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the business unit and department exist, copies their current names onto the site, and
    /// makes sure a department given together with a business unit actually belongs to it.
    /// Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        SiteEntity site,
        IBusinessUnitRepository businessUnits,
        IDepartmentRepository departments,
        CancellationToken cancellationToken)
    {
        var businessUnitError = await MasterDataReferenceResolver.ResolveBusinessUnitAsync(
            site.BusinessUnit,
            businessUnits,
            cancellationToken);

        if (businessUnitError is not null)
        {
            return businessUnitError;
        }

        if (site.Department is null)
        {
            return null;
        }

        var department = MongoObjectId.IsValid(site.Department.Id!)
            ? await departments.GetByIdAsync(site.Department.Id!, cancellationToken)
            : null;

        if (department is null)
        {
            return Results.BadRequest(
                new { message = $"Department '{site.Department.Id}' was not found." });
        }

        if (site.BusinessUnit is not null
            && department.BusinessUnit?.Id is not null
            && department.BusinessUnit.Id != site.BusinessUnit.Id)
        {
            return Results.BadRequest(
                new { message = $"Department '{department.DepartmentName}' does not belong to business unit '{site.BusinessUnit.Name}'." });
        }

        site.Department.Name = department.DepartmentName;
        return null;
    }

    /// <summary>
    /// Checks the owning organization and the parent site (exists, not the site itself or one under it)
    /// and copies their current names. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveStructureAsync(
        SiteEntity site,
        ISiteRepository repository,
        IOrganizationRepository organizations,
        CancellationToken cancellationToken)
    {
        var organizationError = await MasterDataReferenceResolver.ResolveOrganizationAsync(
            site.Organization,
            organizations,
            cancellationToken);

        if (organizationError is not null)
        {
            return organizationError;
        }

        var parentId = site.ParentSite?.Id;

        if (parentId is null)
        {
            return null;
        }

        if (parentId == site.Id)
        {
            return Results.BadRequest(
                new { message = "A site cannot be its own parent." });
        }

        var parentError = await MasterDataReferenceResolver.ResolveSiteAsync(
            site.ParentSite,
            repository,
            cancellationToken);

        if (parentError is not null || string.IsNullOrEmpty(site.Id))
        {
            return parentError;
        }

        var ancestor = await repository.GetByIdAsync(parentId, cancellationToken);

        for (var depth = 0; ancestor?.ParentSite?.Id is not null && depth < 100; depth++)
        {
            if (ancestor.ParentSite.Id == site.Id)
            {
                return Results.BadRequest(
                    new { message = "Parent site cannot be one of this site's own sub-sites." });
            }

            ancestor = await repository.GetByIdAsync(ancestor.ParentSite.Id, cancellationToken);
        }

        return null;
    }
}
