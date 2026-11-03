using A360.Administration.Api.Contracts;
using A360.Administration.Api.Validation;
using A360.Administration.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Administration.Api.Endpoints;

public static class NotificationTemplateEndpoints
{
    private const string EntityType = "NotificationTemplate";

    public static RouteGroupBuilder MapNotificationTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        // Prefixed with /administration because the Inspection service already owns /api/notification-templates at the gateway.
        var group = routes.MapGroup("/api/administration/notification-templates").WithTags("Notification Templates");

        group.MapGet("", GetNotificationTemplatesAsync).WithName("GetNotificationTemplates");
        group.MapGet("/{templateId}", GetNotificationTemplateByIdAsync).WithName("GetNotificationTemplateById");
        group.MapPost("", CreateNotificationTemplateAsync).WithName("CreateNotificationTemplate");
        group.MapPut("/{templateId}", UpdateNotificationTemplateAsync).WithName("UpdateNotificationTemplate");
        group.MapDelete("/{templateId}", DeleteNotificationTemplateAsync).WithName("DeleteNotificationTemplate");

        return group;
    }

    private static async Task<IResult> GetNotificationTemplatesAsync(
        string? tenantId,
        string? clientId,
        string? category,
        string? status,
        INotificationTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetAsync(tenantId, clientId, category, status, cancellationToken);

        return Results.Ok(templates.Select(NotificationTemplateResponse.FromEntity));
    }

    private static async Task<IResult> GetNotificationTemplateByIdAsync(
        string templateId,
        INotificationTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByTemplateIdAsync(templateId, cancellationToken);

        return template is null ? NotFound(templateId) : Results.Ok(NotificationTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> CreateNotificationTemplateAsync(
        CreateNotificationTemplateRequest request,
        INotificationTemplateRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var template = request.ToEntity(currentUserAccessor.GetCurrentUser());

        if (await repository.TemplateCodeExistsAsync(template.TemplateCode, template.TenantId, null, cancellationToken))
        {
            return Results.Conflict(new { message = $"Template code '{template.TemplateCode}' already exists" });
        }

        template = await repository.CreateAsync(template, cancellationToken);

        await eventLogger.LogAsync(EntityType, template.Id, template.TemplateName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/notification-templates/{template.TemplateId}",
            NotificationTemplateResponse.FromEntity(template));
    }

    private static async Task<IResult> UpdateNotificationTemplateAsync(
        string templateId,
        UpdateNotificationTemplateRequest request,
        INotificationTemplateRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var template = await repository.GetByTemplateIdAsync(templateId, cancellationToken);
        if (template is null)
        {
            return NotFound(templateId);
        }

        request.ApplyTo(template, currentUserAccessor.GetCurrentUser());

        if (await repository.TemplateCodeExistsAsync(template.TemplateCode, template.TenantId, template.TemplateId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Template code '{template.TemplateCode}' already exists" });
        }

        if (!await repository.UpdateAsync(template.Id, template, cancellationToken))
        {
            return NotFound(templateId);
        }

        await eventLogger.LogAsync(EntityType, template.Id, template.TemplateName, EventAction.Updated, cancellationToken);

        return Results.Ok(NotificationTemplateResponse.FromEntity(template));
    }

    // Soft delete: the record is kept with IsDeleted = true so its history stays available.
    private static async Task<IResult> DeleteNotificationTemplateAsync(
        string templateId,
        INotificationTemplateRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByTemplateIdAsync(templateId, cancellationToken);
        if (template is null)
        {
            return NotFound(templateId);
        }

        template.IsDeleted = true;
        template.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        template.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(template.Id, template, cancellationToken))
        {
            return NotFound(templateId);
        }

        await eventLogger.LogAsync(EntityType, template.Id, template.TemplateName, EventAction.Deleted, cancellationToken);

        return Results.NoContent();
    }

    private static IResult NotFound(string templateId)
    {
        return Results.NotFound(new { message = $"Notification template '{templateId}' was not found" });
    }
}
