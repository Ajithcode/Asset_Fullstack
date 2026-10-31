using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class NotificationTemplateEndpoints
{
    private const string SequenceName = "notification_template";
    private const string TemplateCodePrefix = "NT";

    public static RouteGroupBuilder MapNotificationTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notification-templates").WithTags("NotificationTemplates");

        group.MapGet("", GetNotificationTemplatesAsync).WithName("GetNotificationTemplates");
        group.MapGet("/{id}", GetNotificationTemplateByIdAsync).WithName("GetNotificationTemplateById");
        group.MapPost("", CreateNotificationTemplateAsync).WithName("CreateNotificationTemplate");
        group.MapPut("/{id}", UpdateNotificationTemplateAsync).WithName("UpdateNotificationTemplate");
        group.MapDelete("/{id}", DeleteNotificationTemplateAsync).WithName("DeleteNotificationTemplate");

        return group;
    }

    private static async Task<IResult> GetNotificationTemplatesAsync(
        INotificationTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(templates.Select(NotificationTemplateResponse.FromEntity));
    }

    private static async Task<IResult> GetNotificationTemplateByIdAsync(
        string id,
        INotificationTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid notification template id." });
        }

        var template = await repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("NotificationTemplate", template.TemplateCode, template.TemplateName, EventAction.Viewed, cancellationToken);
        return Results.Ok(NotificationTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> CreateNotificationTemplateAsync(
        CreateNotificationTemplateRequest request,
        INotificationTemplateRepository repository,
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
        var templateCode = $"{TemplateCodePrefix}{nextSequence:D6}";

        var template = await repository.CreateAsync(request.ToEntity(templateCode), cancellationToken);

        await eventLogger.LogAsync("NotificationTemplate", template.TemplateCode, template.TemplateName, EventAction.Created, cancellationToken);

        return Results.Created($"/api/notification-templates/{template.Id}", NotificationTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> UpdateNotificationTemplateAsync(
        string id,
        UpdateNotificationTemplateRequest request,
        INotificationTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid notification template id." });
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

        await eventLogger.LogAsync("NotificationTemplate", template.TemplateCode, template.TemplateName, EventAction.Updated, cancellationToken);
        return Results.Ok(NotificationTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> DeleteNotificationTemplateAsync(
        string id,
        INotificationTemplateRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid notification template id." });
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

        await eventLogger.LogAsync("NotificationTemplate", template.TemplateCode, template.TemplateName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
