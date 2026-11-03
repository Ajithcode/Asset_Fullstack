using A360.MasterDataManagement.Domain.Entities;
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
    string? Icon,
    string? Image,
    int? DisplayOrder,
    bool? ShowInMobile,
    bool? ShowInReports,
    ConditionCharacteristicsDto? Characteristics,
    List<string>? CanMoveFrom,
    List<string>? CanMoveTo,
    string? TriggerCriteria,
    bool? AutoUpdate,
    bool? PhotoRequired,
    bool? CommentRequired,
    bool? NotifyOwner,
    bool? BlocksAssignment,
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
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            DisplayOrder = DisplayOrder,
            ShowInMobile = ShowInMobile ?? true,
            ShowInReports = ShowInReports ?? true,
            Characteristics = Characteristics?.ToEntity() ?? new ConditionCharacteristics(),
            CanMoveFrom = ConditionCodes.Clean(CanMoveFrom),
            CanMoveTo = ConditionCodes.Clean(CanMoveTo),
            TriggerCriteria = string.IsNullOrWhiteSpace(TriggerCriteria) ? null : TriggerCriteria.Trim(),
            AutoUpdate = AutoUpdate ?? false,
            PhotoRequired = PhotoRequired ?? false,
            CommentRequired = CommentRequired ?? false,
            NotifyOwner = NotifyOwner ?? false,
            BlocksAssignment = BlocksAssignment ?? false,

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
    string? Icon,
    string? Image,
    int? DisplayOrder,
    bool? ShowInMobile,
    bool? ShowInReports,
    ConditionCharacteristicsDto? Characteristics,
    List<string>? CanMoveFrom,
    List<string>? CanMoveTo,
    string? TriggerCriteria,
    bool? AutoUpdate,
    bool? PhotoRequired,
    bool? CommentRequired,
    bool? NotifyOwner,
    bool? BlocksAssignment,
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
        condition.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        condition.Image = string.IsNullOrWhiteSpace(Image) ? null : Image;
        condition.DisplayOrder = DisplayOrder;
        condition.ShowInMobile = ShowInMobile ?? true;
        condition.ShowInReports = ShowInReports ?? true;
        condition.Characteristics = Characteristics?.ToEntity() ?? new ConditionCharacteristics();
        condition.CanMoveFrom = ConditionCodes.Clean(CanMoveFrom);
        condition.CanMoveTo = ConditionCodes.Clean(CanMoveTo);
        condition.TriggerCriteria = string.IsNullOrWhiteSpace(TriggerCriteria) ? null : TriggerCriteria.Trim();
        condition.AutoUpdate = AutoUpdate ?? false;
        condition.PhotoRequired = PhotoRequired ?? false;
        condition.CommentRequired = CommentRequired ?? false;
        condition.NotifyOwner = NotifyOwner ?? false;
        condition.BlocksAssignment = BlocksAssignment ?? false;

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

public sealed record ConditionCharacteristicsDto(
    string? PhysicalState,
    string? Functionality,
    string? Appearance,
    string? MaintenanceNeed,
    string? ExpectedRemainingLife)
{
    public ConditionCharacteristics ToEntity()
    {
        return new ConditionCharacteristics
        {
            PhysicalState = string.IsNullOrWhiteSpace(PhysicalState) ? null : PhysicalState.Trim(),
            Functionality = string.IsNullOrWhiteSpace(Functionality) ? null : Functionality.Trim(),
            Appearance = string.IsNullOrWhiteSpace(Appearance) ? null : Appearance.Trim(),
            MaintenanceNeed = string.IsNullOrWhiteSpace(MaintenanceNeed) ? null : MaintenanceNeed.Trim(),
            ExpectedRemainingLife = string.IsNullOrWhiteSpace(ExpectedRemainingLife) ? null : ExpectedRemainingLife.Trim()
        };
    }

    public static ConditionCharacteristicsDto FromEntity(ConditionCharacteristics? characteristics)
    {
        return new ConditionCharacteristicsDto(
            characteristics?.PhysicalState,
            characteristics?.Functionality,
            characteristics?.Appearance,
            characteristics?.MaintenanceNeed,
            characteristics?.ExpectedRemainingLife);
    }
}

public sealed record ConditionResponse(
    string Id,
    string ConditionName,
    string Code,
    string? Type,
    string? Color,
    bool DefaultCondition,
    string? Description,
    int? HealthScore,
    string? Icon,
    string? Image,
    int? DisplayOrder,
    bool ShowInMobile,
    bool ShowInReports,
    ConditionCharacteristicsDto Characteristics,
    IReadOnlyList<string> CanMoveFrom,
    IReadOnlyList<string> CanMoveTo,
    string? TriggerCriteria,
    bool AutoUpdate,
    bool PhotoRequired,
    bool CommentRequired,
    bool NotifyOwner,
    bool BlocksAssignment,
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
            condition.Icon,
            condition.Image,
            condition.DisplayOrder,
            condition.ShowInMobile,
            condition.ShowInReports,
            ConditionCharacteristicsDto.FromEntity(condition.Characteristics),
            condition.CanMoveFrom ?? [],
            condition.CanMoveTo ?? [],
            condition.TriggerCriteria,
            condition.AutoUpdate,
            condition.PhotoRequired,
            condition.CommentRequired,
            condition.NotifyOwner,
            condition.BlocksAssignment,
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

internal static class ConditionCodes
{
    // Trimmed, without blanks or repeats.
    public static List<string> Clean(List<string>? codes)
    {
        return codes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
    }
}
