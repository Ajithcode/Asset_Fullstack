using A360.Inspection.Domain.Entities;
using TaskCategoryEntity = A360.Inspection.Domain.Entities.TaskCategory;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for TaskCategory.</summary>
public abstract record TaskCategoryFields
{
    public string? CategoryName { get; init; }

    public string? Description { get; init; }

    public int? DisplayOrder { get; init; }

    public string? AssetId { get; init; }

    public string? AssetName { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(TaskCategoryEntity taskCategory)
    {
        taskCategory.CategoryName = CategoryName ?? taskCategory.CategoryName;
        taskCategory.Description = Description ?? taskCategory.Description;
        taskCategory.DisplayOrder = DisplayOrder ?? taskCategory.DisplayOrder;
        taskCategory.AssetId = AssetId ?? taskCategory.AssetId;
        taskCategory.AssetName = AssetName ?? taskCategory.AssetName;
    }
}

public sealed record CreateTaskCategoryRequest : TaskCategoryFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public TaskCategoryEntity ToEntity(string categoryCode)
    {
        var taskCategory = new TaskCategoryEntity
        {
            CategoryCode = categoryCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(taskCategory);
        return taskCategory;
    }
}

public sealed record UpdateTaskCategoryRequest : TaskCategoryFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(TaskCategoryEntity taskCategory)
    {
        ApplyFields(taskCategory);
        taskCategory.Status = Status ?? taskCategory.Status;
        taskCategory.UpdatedBy = UpdatedBy;
        taskCategory.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record TaskCategoryResponse : AuditResponse
{
    private TaskCategoryResponse(TaskCategoryEntity taskCategory)
        : base(taskCategory)
    {
    }

    public string CategoryCode { get; init; } = string.Empty;

    public string CategoryName { get; init; } = default!;

    public string Description { get; init; } = default!;

    public int DisplayOrder { get; init; }

    public string AssetId { get; init; } = default!;

    public string AssetName { get; init; } = default!;

    public static TaskCategoryResponse FromEntity(TaskCategoryEntity taskCategory)
    {
        return new TaskCategoryResponse(taskCategory)
        {
            CategoryCode = taskCategory.CategoryCode,
            CategoryName = taskCategory.CategoryName,
            Description = taskCategory.Description,
            DisplayOrder = taskCategory.DisplayOrder,
            AssetId = taskCategory.AssetId,
            AssetName = taskCategory.AssetName
        };
    }
}
