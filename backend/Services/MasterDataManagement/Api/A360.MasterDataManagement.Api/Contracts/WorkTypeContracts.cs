using WorkTypeEntity = A360.MasterDataManagement.Domain.Entities.WorkType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateWorkTypeRequest(
    string? WorkTypeName,
    string? Code,
    MasterDataReferenceDto? MaintenanceType,
    string? Description,
    double? EstimatedDuration,
    string? DefaultPriority,
    string? RequiredSkill,
    string? ChecklistCode,
    bool? PermitRequired,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public WorkTypeEntity ToEntity()
    {
        return new WorkTypeEntity
        {
            WorkTypeName = WorkTypeName!.Trim(),
            Code = Code!.Trim(),
            MaintenanceType = MaintenanceType?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            EstimatedDuration = EstimatedDuration,
            DefaultPriority = string.IsNullOrWhiteSpace(DefaultPriority) ? null : DefaultPriority.Trim(),
            RequiredSkill = string.IsNullOrWhiteSpace(RequiredSkill) ? null : RequiredSkill.Trim(),
            ChecklistCode = string.IsNullOrWhiteSpace(ChecklistCode) ? null : ChecklistCode.Trim(),
            PermitRequired = PermitRequired ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkTypeRequest(
    string? WorkTypeName,
    string? Code,
    MasterDataReferenceDto? MaintenanceType,
    string? Description,
    double? EstimatedDuration,
    string? DefaultPriority,
    string? RequiredSkill,
    string? ChecklistCode,
    bool? PermitRequired,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(WorkTypeEntity workType)
    {
        workType.WorkTypeName = WorkTypeName!.Trim();
        workType.Code = Code!.Trim();
        workType.MaintenanceType = MaintenanceType?.ToReference();
        workType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        workType.EstimatedDuration = EstimatedDuration;
        workType.DefaultPriority = string.IsNullOrWhiteSpace(DefaultPriority) ? null : DefaultPriority.Trim();
        workType.RequiredSkill = string.IsNullOrWhiteSpace(RequiredSkill) ? null : RequiredSkill.Trim();
        workType.ChecklistCode = string.IsNullOrWhiteSpace(ChecklistCode) ? null : ChecklistCode.Trim();
        workType.PermitRequired = PermitRequired ?? false;

        workType.UpdatedBy = UpdatedBy;
        workType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workType.Status = Status;
        }
    }
}

public sealed record UpdateWorkTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record WorkTypeResponse(
    string Id,
    string WorkTypeName,
    string Code,
    MasterDataReferenceDto? MaintenanceType,
    string? Description,
    double? EstimatedDuration,
    string? DefaultPriority,
    string? RequiredSkill,
    string? ChecklistCode,
    bool PermitRequired,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static WorkTypeResponse FromEntity(WorkTypeEntity workType)
    {
        return new WorkTypeResponse(
            workType.Id ?? string.Empty,
            workType.WorkTypeName ?? string.Empty,
            workType.Code ?? string.Empty,
            workType.MaintenanceType is null
                ? null
                : MasterDataReferenceDto.FromReference(workType.MaintenanceType),
            workType.Description,
            workType.EstimatedDuration,
            workType.DefaultPriority,
            workType.RequiredSkill,
            workType.ChecklistCode,
            workType.PermitRequired,
            workType.CreatedBy,
            workType.CreatedAt,
            workType.UpdatedBy,
            workType.UpdatedAt,
            workType.ClientId,
            workType.TenantId,
            workType.Status,
            workType.IsDeleted
        );
    }
}
