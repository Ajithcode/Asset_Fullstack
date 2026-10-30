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

        group.MapPost("", CreateNotificationTemplateAsync).WithName("CreateNotificationTemplate");

        return group;
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

        if (await repository.TemplateCodeExistsAsync(template.TemplateCode, template.TenantId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Template code '{template.TemplateCode}' already exists" });
        }

        template = await repository.CreateAsync(template, cancellationToken);

        await eventLogger.LogAsync(EntityType, template.Id, template.TemplateName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/administration/notification-templates/{template.TemplateId}",
            NotificationTemplateResponse.FromEntity(template));
    }
}
