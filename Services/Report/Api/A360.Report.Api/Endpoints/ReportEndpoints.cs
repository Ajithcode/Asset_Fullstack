using A360.Domain.Entities;
using A360.Report.Api.Contracts;
using A360.Report.Api.Validation;
using A360.Report.Repository.Repositories;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Report.Api.Endpoints;

public static class ReportEndpoints
{
    private const string EntityType = "Report";

    public static RouteGroupBuilder MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("", GetReportsAsync).WithName("GetReports");
        group.MapGet("/{reportId}", GetReportByIdAsync).WithName("GetReportById");
        group.MapPost("", CreateReportAsync).WithName("CreateReport");
        group.MapDelete("/{reportId}", DeleteReportAsync).WithName("DeleteReport");

        return group;
    }

    private static async Task<IResult> GetReportsAsync(
        string? tenantId,
        string? clientId,
        IReportRepository repository,
        CancellationToken cancellationToken)
    {
        var reports = await repository.GetAsync(tenantId, clientId, cancellationToken);

        return Results.Ok(reports.Select(ReportResponse.FromEntity));
    }

    private static async Task<IResult> GetReportByIdAsync(
        string reportId,
        IReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var report = await repository.GetByReportIdAsync(reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, report.Id, report.ReportName, EventAction.Viewed, cancellationToken);

        return Results.Ok(ReportResponse.FromEntity(report));
    }

    private static async Task<IResult> CreateReportAsync(
        CreateReportRequest request,
        IReportRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var report = await repository.CreateAsync(
            request.ToEntity(currentUserAccessor.GetCurrentUser()),
            cancellationToken);

        await eventLogger.LogAsync(EntityType, report.Id, report.ReportName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/reports/{report.ReportId}", ReportResponse.FromEntity(report));
    }

    private static async Task<IResult> DeleteReportAsync(
        string reportId,
        IReportRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var report = await repository.GetByReportIdAsync(reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(report.Id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(EntityType, report.Id, report.ReportName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }
}
