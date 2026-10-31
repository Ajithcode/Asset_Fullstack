using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class InspectionFindingEndpoints
{
    private const string SequencePrefix = InspectionCodes.FindingSequencePrefix;
    private const string FindingCodePrefix = "FND";

    public static RouteGroupBuilder MapInspectionFindingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inspection-findings").WithTags("InspectionFindings");

        group.MapGet("", GetInspectionFindingsAsync).WithName("GetInspectionFindings");
        group.MapGet("/{id}", GetInspectionFindingByIdAsync).WithName("GetInspectionFindingById");
        group.MapPost("", CreateInspectionFindingAsync).WithName("CreateInspectionFinding");
        group.MapPut("/{id}", UpdateInspectionFindingAsync).WithName("UpdateInspectionFinding");
        group.MapDelete("/{id}", DeleteInspectionFindingAsync).WithName("DeleteInspectionFinding");

        return group;
    }

    private static async Task<IResult> GetInspectionFindingsAsync(
        IInspectionFindingRepository repository,
        CancellationToken cancellationToken)
    {
        var findings = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(findings.Select(InspectionFindingResponse.FromEntity));
    }

    private static async Task<IResult> GetInspectionFindingByIdAsync(
        string id,
        IInspectionFindingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection finding id." });
        }

        var finding = await repository.GetByIdAsync(id, cancellationToken);
        if (finding is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionFinding", finding.FindingCode, finding.Title, EventAction.Viewed, cancellationToken);
        return Results.Ok(InspectionFindingResponse.FromEntity(finding));
    }

    private static async Task<IResult> CreateInspectionFindingAsync(
        CreateInspectionFindingRequest request,
        IInspectionFindingRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var findingCode = await InspectionCodes.NextYearlyAsync(sequenceGenerator, SequencePrefix, FindingCodePrefix, cancellationToken);

        var finding = await repository.CreateAsync(request.ToEntity(findingCode), cancellationToken);

        await eventLogger.LogAsync("InspectionFinding", finding.FindingCode, finding.Title, EventAction.Created, cancellationToken);

        return Results.Created($"/api/inspection-findings/{finding.Id}", InspectionFindingResponse.FromEntity(finding));
    }

    private static async Task<IResult> UpdateInspectionFindingAsync(
        string id,
        UpdateInspectionFindingRequest request,
        IInspectionFindingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection finding id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var finding = await repository.GetByIdAsync(id, cancellationToken);
        if (finding is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(finding);

        var updated = await repository.UpdateAsync(id, finding, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionFinding", finding.FindingCode, finding.Title, EventAction.Updated, cancellationToken);
        return Results.Ok(InspectionFindingResponse.FromEntity(finding));
    }

    private static async Task<IResult> DeleteInspectionFindingAsync(
        string id,
        IInspectionFindingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection finding id." });
        }

        var finding = await repository.GetByIdAsync(id, cancellationToken);
        if (finding is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionFinding", finding.FindingCode, finding.Title, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
