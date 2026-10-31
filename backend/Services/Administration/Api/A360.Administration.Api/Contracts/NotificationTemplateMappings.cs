using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class NotificationTemplateMappings
{
    public static NotificationTemplate ToEntity(this CreateNotificationTemplateRequest request, CurrentUser? currentUser)
    {
        return new NotificationTemplate
        {
            // Generated on the server so every template gets a stable id independent of the Mongo _id.
            TemplateId = Guid.NewGuid().ToString(),
            TemplateName = request.TemplateName!.Trim(),
            TemplateCode = request.TemplateCode!.Trim().ToUpperInvariant(),
            Category = request.Category!.Trim(),
            TriggerEvent = request.TriggerEvent!.Trim(),
            DeliveryChannels = request.DeliveryChannels!
                .Select(NotificationTemplateValidationExtensions.NormalizeChannel)
                .OfType<string>()
                .Distinct()
                .ToList(),
            Language = request.Language!.Trim(),
            Priority = Optional(request.Priority),
            DeliveryTiming = Optional(request.DeliveryTiming),
            RetryPolicy = Optional(request.RetryPolicy),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    // Tenant, client, creator and the template id are kept from the stored template.
    public static void ApplyTo(this UpdateNotificationTemplateRequest request, NotificationTemplate template, CurrentUser? currentUser)
    {
        template.TemplateName = request.TemplateName!.Trim();
        template.TemplateCode = request.TemplateCode!.Trim().ToUpperInvariant();
        template.Category = request.Category!.Trim();
        template.TriggerEvent = request.TriggerEvent!.Trim();
        template.DeliveryChannels = request.DeliveryChannels!
            .Select(NotificationTemplateValidationExtensions.NormalizeChannel)
            .OfType<string>()
            .Distinct()
            .ToList();
        template.Language = request.Language!.Trim();
        template.Priority = Optional(request.Priority);
        template.DeliveryTiming = Optional(request.DeliveryTiming);
        template.RetryPolicy = Optional(request.RetryPolicy);
        template.Status = request.Status!.Trim();
        template.UpdatedBy = currentUser?.UserName;
        template.UpdatedAt = DateTime.UtcNow;
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
