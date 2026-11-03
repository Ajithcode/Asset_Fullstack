
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class ComplianceInspectionEndpoints
{
    public static RouteGroupBuilder MapComplianceInspectionEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/compliance-inspections")
            .WithTags("ComplianceInspection");

        group.MapGet("", GetComplianceInspectionsAsync)
            .WithName("GetComplianceInspections");

        group.MapGet("/{id}", GetComplianceInspectionByIdAsync)
            .WithName("GetComplianceInspectionById");

        group.MapGet("/asset/{assetId}", GetComplianceInspectionsByAssetIdAsync)
            .WithName("GetComplianceInspectionsByAssetId");

        group.MapPost("", CreateComplianceInspectionAsync)
            .WithName("CreateComplianceInspection");

        group.MapPut("/{id}", UpdateComplianceInspectionAsync)
            .WithName("UpdateComplianceInspection");

        group.MapDelete("/{id}", DeleteComplianceInspectionAsync)
            .WithName("DeleteComplianceInspection");

        return group;
    }

    private static async Task<IResult> GetComplianceInspectionsAsync(
        IComplianceInspectionRepository repository,
        CancellationToken cancellationToken)
    {
        var complianceInspections = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            complianceInspections.Select(ComplianceInspectionResponse.FromEntity));
    }

    private static async Task<IResult> GetComplianceInspectionByIdAsync(
        string id,
        IComplianceInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid complianceinspection id." });
        }

        var complianceInspection = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (complianceInspection is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ComplianceInspection",
            complianceInspection.Id,
            complianceInspection.InspectionId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ComplianceInspectionResponse.FromEntity(complianceInspection));
    }

    private static async Task<IResult> GetComplianceInspectionsByAssetIdAsync(
        string assetId,
        IComplianceInspectionRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var complianceInspections = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            complianceInspections.Select(ComplianceInspectionResponse.FromEntity));
    }

    private static async Task<IResult> CreateComplianceInspectionAsync(
        CreateComplianceInspectionRequest request,
        IComplianceInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var complianceInspection = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "ComplianceInspection",
            complianceInspection.Id,
            complianceInspection.InspectionId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/compliance-inspections/{complianceInspection.Id}",
            ComplianceInspectionResponse.FromEntity(complianceInspection));
    }

    private static async Task<IResult> UpdateComplianceInspectionAsync(
        string id,
        UpdateComplianceInspectionRequest request,
        IComplianceInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid complianceinspection id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var complianceInspection = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (complianceInspection is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(complianceInspection);

        var updated = await repository.UpdateAsync(
            id,
            complianceInspection,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ComplianceInspection",
            complianceInspection.Id,
            complianceInspection.InspectionId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ComplianceInspectionResponse.FromEntity(complianceInspection));
    }

    private static async Task<IResult> DeleteComplianceInspectionAsync(
        string id,
        IComplianceInspectionRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid complianceinspection id." });
        }

        var complianceInspection = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (complianceInspection is null)
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
            "ComplianceInspection",
            complianceInspection.Id,
            complianceInspection.InspectionId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
