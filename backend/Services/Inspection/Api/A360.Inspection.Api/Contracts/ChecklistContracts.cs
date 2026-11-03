using ChecklistEntity = A360.Inspection.Domain.Entities.Checklist;
using ChecklistSectionEntity = A360.Inspection.Domain.Entities.ChecklistSection;
using ChecklistTaskItemEntity = A360.Inspection.Domain.Entities.ChecklistTaskItem;

namespace A360.Inspection.Api.Contracts;

public sealed record ChecklistTaskItemRequest(
    string? TaskId,
    string? TaskTitle,
    bool? IsMandatory,
    int? Order)
{
    public ChecklistTaskItemEntity ToEntity()
    {
        return new ChecklistTaskItemEntity
        {
            TaskId = TaskId ?? string.Empty,
            TaskTitle = TaskTitle ?? string.Empty,
            IsMandatory = IsMandatory ?? false,
            Order = Order ?? 0
        };
    }
}

public sealed record ChecklistSectionRequest(
    string? SectionName,
    int? Order,
    List<ChecklistTaskItemRequest>? Tasks)
{
    public ChecklistSectionEntity ToEntity()
    {
        return new ChecklistSectionEntity
        {
            SectionName = SectionName ?? string.Empty,
            Order = Order ?? 0,
            Tasks = Tasks?.Select(task => task.ToEntity()).ToList() ?? []
        };
    }
}

public sealed record ChecklistTaskItemResponse(
    string TaskId,
    string TaskTitle,
    bool IsMandatory,
    int Order)
{
    public static ChecklistTaskItemResponse FromEntity(ChecklistTaskItemEntity task)
    {
        return new ChecklistTaskItemResponse(task.TaskId, task.TaskTitle, task.IsMandatory, task.Order);
    }
}

public sealed record ChecklistSectionResponse(
    string SectionName,
    int Order,
    List<ChecklistTaskItemResponse> Tasks)
{
    public static ChecklistSectionResponse FromEntity(ChecklistSectionEntity section)
    {
        return new ChecklistSectionResponse(
            section.SectionName,
            section.Order,
            section.Tasks.Select(ChecklistTaskItemResponse.FromEntity).ToList());
    }
}

public sealed record CreateChecklistRequest(
    string? TemplateName,
    string? InspectionTypeId,
    string? InspectionTypeName,
    string? AssetCategory,
    string? AssetType,
    List<ChecklistSectionRequest>? Sections,
    int? Version,
    DateTime? EffectiveDate,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ChecklistEntity ToEntity(string checklistCode)
    {
        return new ChecklistEntity
        {
            ChecklistCode = checklistCode,
            TemplateName = TemplateName ?? string.Empty,
            InspectionTypeId = InspectionTypeId ?? string.Empty,
            InspectionTypeName = InspectionTypeName ?? string.Empty,
            AssetCategory = AssetCategory ?? string.Empty,
            AssetType = AssetType ?? string.Empty,
            Sections = Sections?.Select(section => section.ToEntity()).ToList() ?? [],
            Version = Version ?? 1,
            EffectiveDate = EffectiveDate,
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

public sealed record UpdateChecklistRequest(
    string? TemplateName,
    string? InspectionTypeId,
    string? InspectionTypeName,
    string? AssetCategory,
    string? AssetType,
    List<ChecklistSectionRequest>? Sections,
    int? Version,
    DateTime? EffectiveDate,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ChecklistEntity checklist)
    {
        checklist.TemplateName = TemplateName ?? string.Empty;
        checklist.InspectionTypeId = InspectionTypeId ?? string.Empty;
        checklist.InspectionTypeName = InspectionTypeName ?? string.Empty;
        checklist.AssetCategory = AssetCategory ?? string.Empty;
        checklist.AssetType = AssetType ?? string.Empty;
        checklist.Sections = Sections?.Select(section => section.ToEntity()).ToList() ?? checklist.Sections;
        checklist.Version = Version ?? checklist.Version;
        checklist.EffectiveDate = EffectiveDate ?? checklist.EffectiveDate;
        checklist.IsActive = IsActive ?? checklist.IsActive;
        checklist.Status = Status;
        checklist.UpdatedBy = UpdatedBy;
        checklist.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record ChecklistResponse(
    string Id,
    string ChecklistCode,
    string TemplateName,
    string InspectionTypeId,
    string InspectionTypeName,
    string AssetCategory,
    string AssetType,
    List<ChecklistSectionResponse> Sections,
    int Version,
    DateTime? EffectiveDate,
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
    public static ChecklistResponse FromEntity(ChecklistEntity checklist)
    {
        return new ChecklistResponse(
            checklist.Id,
            checklist.ChecklistCode,
            checklist.TemplateName,
            checklist.InspectionTypeId,
            checklist.InspectionTypeName,
            checklist.AssetCategory,
            checklist.AssetType,
            checklist.Sections.Select(ChecklistSectionResponse.FromEntity).ToList(),
            checklist.Version,
            checklist.EffectiveDate,
            checklist.IsActive,
            checklist.Status,
            checklist.CreatedBy,
            checklist.CreatedAt,
            checklist.UpdatedBy,
            checklist.UpdatedAt,
            checklist.ClientId,
            checklist.TenantId,
            checklist.IsDeleted);
    }
}
