using A360.Administration.Api.Validation;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class ChecklistItemMappings
{
    public static ChecklistItem ToEntity(this CreateChecklistItemRequest request, CurrentUser? currentUser)
    {
        return new ChecklistItem
        {
            // Generated on the server so every item gets a stable id independent of the Mongo _id.
            ItemId = Guid.NewGuid().ToString(),
            ItemText = request.ItemText!.Trim(),
            Section = request.Section!.Trim(),
            ResponseType = ChecklistItemValidationExtensions.NormalizeResponseType(request.ResponseType)!,
            IsRequired = request.IsRequired ?? false,
            IsCritical = request.IsCritical ?? false,
            HelpText = Optional(request.HelpText),
            ExpectedValue = Optional(request.ExpectedValue),
            AttachmentRequired = request.AttachmentRequired ?? false,
            ScoreValue = request.ScoreValue!.Value,
            FailureAction = Optional(request.FailureAction),
            EscalateIssue = request.EscalateIssue ?? false,
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    public static void ApplyTo(this UpdateChecklistItemRequest request, ChecklistItem item, CurrentUser? currentUser)
    {
        item.ItemText = request.ItemText!.Trim();
        item.Section = request.Section!.Trim();
        item.ResponseType = ChecklistItemValidationExtensions.NormalizeResponseType(request.ResponseType)!;
        item.IsRequired = request.IsRequired ?? false;
        item.IsCritical = request.IsCritical ?? false;
        item.HelpText = Optional(request.HelpText);
        item.ExpectedValue = Optional(request.ExpectedValue);
        item.AttachmentRequired = request.AttachmentRequired ?? false;
        item.ScoreValue = request.ScoreValue!.Value;
        item.FailureAction = Optional(request.FailureAction);
        item.EscalateIssue = request.EscalateIssue ?? false;
        item.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        item.UpdatedAt = DateTime.UtcNow;
    }

    public static void MarkDeleted(this ChecklistItem item, CurrentUser? currentUser)
    {
        item.IsDeleted = true;
        item.UpdatedBy = currentUser?.UserName ?? item.UpdatedBy;
        item.UpdatedAt = DateTime.UtcNow;
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
