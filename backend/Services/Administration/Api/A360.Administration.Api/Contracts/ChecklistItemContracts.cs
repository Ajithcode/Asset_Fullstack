using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateChecklistItemRequest(
    string? ItemText,
    string? Section,
    string? ResponseType,
    bool? IsRequired,
    bool? IsCritical,
    string? HelpText,
    string? ExpectedValue,
    bool? AttachmentRequired,
    int? ScoreValue,
    string? FailureAction,
    bool? EscalateIssue,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

// Tenant/client and creation audit are immutable, so they are not part of an update.
public sealed record UpdateChecklistItemRequest(
    string? ItemText,
    string? Section,
    string? ResponseType,
    bool? IsRequired,
    bool? IsCritical,
    string? HelpText,
    string? ExpectedValue,
    bool? AttachmentRequired,
    int? ScoreValue,
    string? FailureAction,
    bool? EscalateIssue,
    string? UpdatedBy);

public sealed record ChecklistItemResponse(
    string ItemId,
    string ItemText,
    string Section,
    string ResponseType,
    bool IsRequired,
    bool IsCritical,
    string? HelpText,
    string? ExpectedValue,
    bool AttachmentRequired,
    int ScoreValue,
    string? FailureAction,
    bool EscalateIssue,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static ChecklistItemResponse FromEntity(ChecklistItem item)
    {
        return new ChecklistItemResponse(
            item.ItemId,
            item.ItemText,
            item.Section,
            item.ResponseType,
            item.IsRequired,
            item.IsCritical,
            item.HelpText,
            item.ExpectedValue,
            item.AttachmentRequired,
            item.ScoreValue,
            item.FailureAction,
            item.EscalateIssue,
            item.CreatedBy,
            item.CreatedAt,
            item.UpdatedBy,
            item.UpdatedAt,
            item.TenantId,
            item.ClientId);
    }
}
