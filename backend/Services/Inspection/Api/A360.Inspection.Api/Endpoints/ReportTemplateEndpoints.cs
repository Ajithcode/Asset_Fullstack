using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class ReportTemplateEndpoints
{
    private const string SequenceName = "report_template";
    private const string ReportTemplateCodePrefix = "RT";

    public static RouteGroupBuilder MapReportTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/report-templates").WithTags("ReportTemplates");

        group.MapGet("", GetReportTemplatesAsync).WithName("GetReportTemplates");
        group.MapGet("/{id}", GetReportTemplateByIdAsync).WithName("GetReportTemplateById");
        group.MapPost("", CreateReportTemplateAsync).WithName("CreateReportTemplate");
        group.MapPut("/{id}", UpdateReportTemplateAsync).WithName("UpdateReportTemplate");
        group.MapDelete("/{id}", DeleteReportTemplateAsync).WithName("DeleteReportTemplate");

        return group;
    }

    private static async Task<IResult> GetReportTemplatesAsync(
        IReportTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(templates.Select(ReportTemplateResponse.FromEntity));
    }

    private static async Task<IResult> GetReportTemplateByIdAsync(
        string id,
        IReportTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid report template id." });
        }

        var template = await repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("ReportTemplate", template.ReportTemplateCode, template.ReportTitle, EventAction.Viewed, cancellationToken);
        return Results.Ok(ReportTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> CreateReportTemplateAsync(
        CreateReportTemplateRequest request,
        IReportTemplateRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var reportTemplateCode = $"{ReportTemplateCodePrefix}{nextSequence:D6}";

        var template = await repository.CreateAsync(request.ToEntity(reportTemplateCode), cancellationToken);

        await eventLogger.LogAsync("ReportTemplate", template.ReportTemplateCode, template.ReportTitle, EventAction.Created, cancellationToken);

        return Results.Created($"/api/report-templates/{template.Id}", ReportTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> UpdateReportTemplateAsync(
        string id,
        UpdateReportTemplateRequest request,
        IReportTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid report template id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var template = await repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(template);

        var updated = await repository.UpdateAsync(id, template, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("ReportTemplate", template.ReportTemplateCode, template.ReportTitle, EventAction.Updated, cancellationToken);
        return Results.Ok(ReportTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> DeleteReportTemplateAsync(
        string id,
        IReportTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid report template id." });
        }

        var template = await repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("ReportTemplate", template.ReportTemplateCode, template.ReportTitle, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
