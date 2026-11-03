
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class IssueReportEndpoints
{
    public static RouteGroupBuilder MapIssueReportEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/issue-reports")
            .WithTags("IssueReports");

        group.MapGet("", GetIssueReportsAsync)
            .WithName("GetIssueReports");

        group.MapGet("/{id}", GetIssueReportByIdAsync)
            .WithName("GetIssueReportById");

        group.MapGet("/asset/{assetId}", GetIssueReportsByAssetIdAsync)
            .WithName("GetIssueReportsByAssetId");

        group.MapPost("", CreateIssueReportAsync)
            .WithName("CreateIssueReport");

        group.MapPut("/{id}", UpdateIssueReportAsync)
            .WithName("UpdateIssueReport");

        group.MapDelete("/{id}", DeleteIssueReportAsync)
            .WithName("DeleteIssueReport");

        return group;
    }

    private static async Task<IResult> GetIssueReportsAsync(
        IIssueReportRepository repository,
        CancellationToken cancellationToken)
    {
        var issueReports = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            issueReports.Select(IssueReportResponse.FromEntity));
    }

    private static async Task<IResult> GetIssueReportByIdAsync(
        string id,
        IIssueReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issuereports id." });
        }

        var issueReport = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueReport is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "IssueReport",
            issueReport.Id,
            issueReport.IssueId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(IssueReportResponse.FromEntity(issueReport));
    }

    private static async Task<IResult> GetIssueReportsByAssetIdAsync(
        string assetId,
        IIssueReportRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var issueReports = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            issueReports.Select(IssueReportResponse.FromEntity));
    }

    private static async Task<IResult> CreateIssueReportAsync(
        CreateIssueReportRequest request,
        IIssueReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var issueReport = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "IssueReport",
            issueReport.Id,
            issueReport.IssueId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/issue-reports/{issueReport.Id}",
            IssueReportResponse.FromEntity(issueReport));
    }

    private static async Task<IResult> UpdateIssueReportAsync(
        string id,
        UpdateIssueReportRequest request,
        IIssueReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issuereports id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var issueReport = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueReport is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(issueReport);

        var updated = await repository.UpdateAsync(
            id,
            issueReport,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "IssueReport",
            issueReport.Id,
            issueReport.IssueId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(IssueReportResponse.FromEntity(issueReport));
    }

    private static async Task<IResult> DeleteIssueReportAsync(
        string id,
        IIssueReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid issuereports id." });
        }

        var issueReport = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (issueReport is null)
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
            "IssueReport",
            issueReport.Id,
            issueReport.IssueId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
