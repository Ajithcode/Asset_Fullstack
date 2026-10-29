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

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
