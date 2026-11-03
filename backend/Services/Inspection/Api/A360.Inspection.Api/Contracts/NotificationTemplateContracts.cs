using NotificationTemplateEntity = A360.Inspection.Domain.Entities.NotificationTemplate;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateNotificationTemplateRequest(
    string? TemplateName,
    string? Event,
    string? Channel,
    string? Subject,
    string? MessageBody,
    List<string>? Recipients,
    List<string>? CcRecipients,
    List<string>? EscalationRecipients,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public NotificationTemplateEntity ToEntity(string templateCode)
    {
        return new NotificationTemplateEntity
        {
            TemplateCode = templateCode,
            TemplateName = TemplateName ?? string.Empty,
            Event = Event ?? string.Empty,
            Channel = Channel ?? string.Empty,
            Subject = Subject ?? string.Empty,
            MessageBody = MessageBody ?? string.Empty,
            Recipients = Recipients ?? [],
            CcRecipients = CcRecipients ?? [],
            EscalationRecipients = EscalationRecipients ?? [],
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateNotificationTemplateRequest(
    string? TemplateName,
    string? Event,
    string? Channel,
    string? Subject,
    string? MessageBody,
    List<string>? Recipients,
    List<string>? CcRecipients,
    List<string>? EscalationRecipients,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(NotificationTemplateEntity template)
    {
        template.TemplateName = TemplateName ?? string.Empty;
        template.Event = Event ?? string.Empty;
        template.Channel = Channel ?? string.Empty;
        template.Subject = Subject ?? string.Empty;
        template.MessageBody = MessageBody ?? string.Empty;
        template.Recipients = Recipients ?? [];
        template.CcRecipients = CcRecipients ?? [];
        template.EscalationRecipients = EscalationRecipients ?? [];
        template.IsActive = IsActive ?? template.IsActive;
        template.Status = Status;
        template.UpdatedBy = UpdatedBy;
        template.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record NotificationTemplateResponse(
    string Id,
    string TemplateCode,
    string TemplateName,
    string Event,
    string Channel,
    string Subject,
    string MessageBody,
    List<string> Recipients,
    List<string> CcRecipients,
    List<string> EscalationRecipients,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static NotificationTemplateResponse FromEntity(NotificationTemplateEntity template)
    {
        return new NotificationTemplateResponse(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.Event,
            template.Channel,
            template.Subject,
            template.MessageBody,
            template.Recipients,
            template.CcRecipients,
            template.EscalationRecipients,
            template.IsActive,
            template.Status,
            template.CreatedBy,
            template.CreatedAt,
            template.UpdatedBy,
            template.UpdatedAt,
            template.ClientId,
            template.TenantId,
            template.IsDeleted);
    }
}
