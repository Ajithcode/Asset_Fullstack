using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateNotificationTemplateRequest(
    string? TemplateName,
    string? TemplateCode,
    string? Category,
    string? TriggerEvent,
    List<string>? DeliveryChannels,
    string? Language,
    string? Priority,
    string? DeliveryTiming,
    string? RetryPolicy,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record NotificationTemplateResponse(
    string TemplateId,
    string TemplateName,
    string TemplateCode,
    string Category,
    string TriggerEvent,
    IReadOnlyCollection<string> DeliveryChannels,
    string Language,
    string? Priority,
    string? DeliveryTiming,
    string? RetryPolicy,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static NotificationTemplateResponse FromEntity(NotificationTemplate template)
    {
        return new NotificationTemplateResponse(
            template.TemplateId,
            template.TemplateName,
            template.TemplateCode,
            template.Category,
            template.TriggerEvent,
            template.DeliveryChannels,
            template.Language,
            template.Priority,
            template.DeliveryTiming,
            template.RetryPolicy,
            template.Status,
            template.CreatedBy,
            template.CreatedAt,
            template.UpdatedBy,
            template.UpdatedAt,
            template.TenantId,
            template.ClientId);
    }
}
