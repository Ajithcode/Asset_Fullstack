using ConditionEntity = A360.MasterDataManagement.Domain.Entities.Condition;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateConditionRequest(
    string? ConditionName,
    string? Code,
    string? Type,
    string? Color,
    bool? DefaultCondition,
    string? Description,
    int? HealthScore,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ConditionEntity ToEntity()
    {
        return new ConditionEntity
        {
            ConditionName = ConditionName!.Trim(),
            Code = Code!.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            DefaultCondition = DefaultCondition ?? false,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            HealthScore = HealthScore,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateConditionRequest(
    string? ConditionName,
    string? Code,
    string? Type,
    string? Color,
    bool? DefaultCondition,
    string? Description,
    int? HealthScore,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ConditionEntity condition)
    {
        condition.ConditionName = ConditionName!.Trim();
        condition.Code = Code!.Trim();
        condition.Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();
        condition.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        condition.DefaultCondition = DefaultCondition ?? false;
        condition.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        condition.HealthScore = HealthScore;

        condition.UpdatedBy = UpdatedBy;
        condition.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            condition.Status = Status;
        }
    }
}

public sealed record UpdateConditionStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ConditionResponse(
    string Id,
    string ConditionName,
    string Code,
    string? Type,
    string? Color,
    bool DefaultCondition,
    string? Description,
    int? HealthScore,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ConditionResponse FromEntity(ConditionEntity condition)
    {
        return new ConditionResponse(
            condition.Id ?? string.Empty,
            condition.ConditionName ?? string.Empty,
            condition.Code ?? string.Empty,
            condition.Type,
            condition.Color,
            condition.DefaultCondition,
            condition.Description,
            condition.HealthScore,
            condition.CreatedBy,
            condition.CreatedAt,
            condition.UpdatedBy,
            condition.UpdatedAt,
            condition.ClientId,
            condition.TenantId,
            condition.Status,
            condition.IsDeleted
        );
    }
}
