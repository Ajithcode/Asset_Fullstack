
using ChecklistItemEntity = A360.Wip.Domain.Entities.ChecklistItem;

namespace A360.Wip.Api.Contracts;

public sealed record CreateChecklistItemRequest(
    string? ItemId,
    string? AssetId,
    string? AssetName,
    string? ChecklistId,
    string? ItemDescription,
    string? ResponseType,
    double? ThresholdValue,
    bool? IsCritical,
    int? SequenceOrder,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public ChecklistItemEntity ToEntity()
    {
        return new ChecklistItemEntity
        {
            ItemId = ItemId,
            AssetId = AssetId,
            AssetName = AssetName,
            ChecklistId = ChecklistId,

            ItemDescription = ItemDescription,
            ResponseType = ResponseType,
            ThresholdValue = ThresholdValue,
            IsCritical = IsCritical ?? false,
            SequenceOrder = SequenceOrder,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateChecklistItemRequest(
    string? AssetId,
    string? AssetName,
    string? ChecklistId,
    string? ItemDescription,
    string? ResponseType,
    double? ThresholdValue,
    bool? IsCritical,
    int? SequenceOrder,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(ChecklistItemEntity checklistItem)
    {
        checklistItem.AssetId = AssetId;
        checklistItem.AssetName = AssetName;
        checklistItem.ChecklistId = ChecklistId;

        checklistItem.ItemDescription = ItemDescription;
        checklistItem.ResponseType = ResponseType;
        checklistItem.ThresholdValue = ThresholdValue;
        checklistItem.IsCritical = IsCritical ?? checklistItem.IsCritical;
        checklistItem.SequenceOrder = SequenceOrder;

        checklistItem.UpdatedBy = UpdatedBy;
        checklistItem.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            checklistItem.Status = Status;
        }
    }
}

public sealed record ChecklistItemResponse(
    string Id,
    string ItemId,
    string? AssetId,
    string? AssetName,
    string? ChecklistId,
    string ItemDescription,
    string? ResponseType,
    double? ThresholdValue,
    bool IsCritical,
    int? SequenceOrder,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ChecklistItemResponse FromEntity(ChecklistItemEntity checklistItem)
    {
        return new ChecklistItemResponse(
            checklistItem.Id ?? string.Empty,
            checklistItem.ItemId ?? string.Empty,
            checklistItem.AssetId,
            checklistItem.AssetName,
            checklistItem.ChecklistId,

            checklistItem.ItemDescription ?? string.Empty,
            checklistItem.ResponseType,
            checklistItem.ThresholdValue,
            checklistItem.IsCritical,
            checklistItem.SequenceOrder,

            checklistItem.CreatedBy,
            checklistItem.CreatedAt,
            checklistItem.UpdatedBy,
            checklistItem.UpdatedAt,

            checklistItem.ClientId,
            checklistItem.TenantId,
            checklistItem.Status,
            checklistItem.IsDeleted
        );
    }
}
