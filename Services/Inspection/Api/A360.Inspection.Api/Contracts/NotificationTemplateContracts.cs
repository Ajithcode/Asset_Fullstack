using A360.Inspection.Domain.Entities;
using NotificationTemplateEntity = A360.Inspection.Domain.Entities.NotificationTemplate;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for NotificationTemplate.</summary>
public abstract record NotificationTemplateFields
{
    public string? TemplateName { get; init; }

    public string? Event { get; init; }

    public List<string>? Channels { get; init; }

    public string? Subject { get; init; }

    public string? MessageBody { get; init; }

    public List<string>? Recipients { get; init; }

    public List<string>? CcRecipients { get; init; }

    public string? ButtonLabel { get; init; }

    public string? Timing { get; init; }

    public bool? RespectUserLocale { get; init; }

    public bool? PreventDuplicates { get; init; }

    public bool? IsActive { get; init; }

    public string? Channel { get; init; }

    public List<string>? EscalationRecipients { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(NotificationTemplateEntity template)
    {
        template.TemplateName = TemplateName ?? template.TemplateName;
        template.Event = Event ?? template.Event;
        template.Channels = Channels ?? template.Channels;
        template.Subject = Subject ?? template.Subject;
        template.MessageBody = MessageBody ?? template.MessageBody;
        template.Recipients = Recipients ?? template.Recipients;
        template.CcRecipients = CcRecipients ?? template.CcRecipients;
        template.ButtonLabel = ButtonLabel ?? template.ButtonLabel;
        template.Timing = Timing ?? template.Timing;
        template.RespectUserLocale = RespectUserLocale ?? template.RespectUserLocale;
        template.PreventDuplicates = PreventDuplicates ?? template.PreventDuplicates;
        template.IsActive = IsActive ?? template.IsActive;
        template.Channel = Channel ?? template.Channel;
        template.EscalationRecipients = EscalationRecipients ?? template.EscalationRecipients;

        // Older records stored a single comma-separated `channel`; keep both shapes in step.
        if (Channels is not null)
        {
            template.Channel = string.Join(", ", template.Channels);
        }
        else if (template.Channels.Count == 0 && !string.IsNullOrWhiteSpace(template.Channel))
        {
            template.Channels = NotificationChannels.Split(template.Channel);
        }
    }
}

public sealed record CreateNotificationTemplateRequest : NotificationTemplateFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public NotificationTemplateEntity ToEntity(string templateCode)
    {
        var template = new NotificationTemplateEntity
        {
            TemplateCode = templateCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(template);
        return template;
    }
}

public sealed record UpdateNotificationTemplateRequest : NotificationTemplateFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(NotificationTemplateEntity template)
    {
        ApplyFields(template);
        template.Status = Status ?? template.Status;
        template.UpdatedBy = UpdatedBy;
        template.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record NotificationTemplateResponse : AuditResponse
{
    private NotificationTemplateResponse(NotificationTemplateEntity template)
        : base(template)
    {
    }

    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = default!;

    public string Event { get; init; } = default!;

    public List<string> Channels { get; init; } = default!;

    public string Subject { get; init; } = default!;

    public string MessageBody { get; init; } = default!;

    public List<string> Recipients { get; init; } = default!;

    public List<string> CcRecipients { get; init; } = default!;

    public string ButtonLabel { get; init; } = default!;

    public string Timing { get; init; } = default!;

    public bool RespectUserLocale { get; init; }

    public bool PreventDuplicates { get; init; }

    public bool IsActive { get; init; }

    public string Channel { get; init; } = default!;

    public List<string> EscalationRecipients { get; init; } = default!;

    public static NotificationTemplateResponse FromEntity(NotificationTemplateEntity template)
    {
        return new NotificationTemplateResponse(template)
        {
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            Event = template.Event,
            Channels = template.Channels.Count > 0 ? template.Channels : NotificationChannels.Split(template.Channel),
            Subject = template.Subject,
            MessageBody = template.MessageBody,
            Recipients = template.Recipients,
            CcRecipients = template.CcRecipients,
            ButtonLabel = template.ButtonLabel,
            Timing = template.Timing,
            RespectUserLocale = template.RespectUserLocale,
            PreventDuplicates = template.PreventDuplicates,
            IsActive = template.IsActive,
            Channel = template.Channel,
            EscalationRecipients = template.EscalationRecipients
        };
    }
}
