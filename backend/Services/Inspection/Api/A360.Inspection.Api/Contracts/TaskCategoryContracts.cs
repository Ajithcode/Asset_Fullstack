using TaskCategoryEntity = A360.Inspection.Domain.Entities.TaskCategory;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateTaskCategoryRequest(
    string? AssetId,
    string? AssetName,
    string? CategoryName,
    string? Description,
    int? DisplayOrder,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TaskCategoryEntity ToEntity(string categoryCode)
    {
        return new TaskCategoryEntity
        {
            CategoryCode = categoryCode,
            AssetId = AssetId ?? string.Empty,
            AssetName = AssetName ?? string.Empty,
            CategoryName = CategoryName ?? string.Empty,
            Description = Description ?? string.Empty,
            DisplayOrder = DisplayOrder ?? 0,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTaskCategoryRequest(
    string? AssetId,
    string? AssetName,
    string? CategoryName,
    string? Description,
    int? DisplayOrder,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(TaskCategoryEntity taskCategory)
    {
        taskCategory.AssetId = AssetId ?? string.Empty;
        taskCategory.AssetName = AssetName ?? string.Empty;
        taskCategory.CategoryName = CategoryName ?? string.Empty;
        taskCategory.Description = Description ?? string.Empty;
        taskCategory.DisplayOrder = DisplayOrder ?? taskCategory.DisplayOrder;
        taskCategory.Status = Status;
        taskCategory.UpdatedBy = UpdatedBy;
        taskCategory.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record TaskCategoryResponse(
    string Id,
    string CategoryCode,
    string AssetId,
    string AssetName,
    string CategoryName,
    string Description,
    int DisplayOrder,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static TaskCategoryResponse FromEntity(TaskCategoryEntity taskCategory)
    {
        return new TaskCategoryResponse(
            taskCategory.Id,
            taskCategory.CategoryCode,
            taskCategory.AssetId,
            taskCategory.AssetName,
            taskCategory.CategoryName,
            taskCategory.Description,
            taskCategory.DisplayOrder,
            taskCategory.Status,
            taskCategory.CreatedBy,
            taskCategory.CreatedAt,
            taskCategory.UpdatedBy,
            taskCategory.UpdatedAt,
            taskCategory.ClientId,
            taskCategory.TenantId,
            taskCategory.IsDeleted);
    }
}
