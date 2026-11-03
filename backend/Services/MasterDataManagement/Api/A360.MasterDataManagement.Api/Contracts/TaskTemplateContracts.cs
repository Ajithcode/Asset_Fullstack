using TaskTemplateEntity = A360.MasterDataManagement.Domain.Entities.TaskTemplate;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateTaskTemplateRequest(
    string? TemplateName,
    string? Code,
    MasterDataReferenceDto? MaintenanceType,
    double? EstimatedDuration,
    string? Description,
    List<MasterDataStepDto>? Tasks,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TaskTemplateEntity ToEntity()
    {
        return new TaskTemplateEntity
        {
            TemplateName = TemplateName!.Trim(),
            Code = Code!.Trim(),
            MaintenanceType = MaintenanceType?.ToReference(),
            EstimatedDuration = EstimatedDuration,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Tasks = Tasks?.Select(x => x.ToStep()).ToList() ?? [],

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTaskTemplateRequest(
    string? TemplateName,
    string? Code,
    MasterDataReferenceDto? MaintenanceType,
    double? EstimatedDuration,
    string? Description,
    List<MasterDataStepDto>? Tasks,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(TaskTemplateEntity taskTemplate)
    {
        taskTemplate.TemplateName = TemplateName!.Trim();
        taskTemplate.Code = Code!.Trim();
        taskTemplate.MaintenanceType = MaintenanceType?.ToReference();
        taskTemplate.EstimatedDuration = EstimatedDuration;
        taskTemplate.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        taskTemplate.Tasks = Tasks?.Select(x => x.ToStep()).ToList() ?? [];

        taskTemplate.UpdatedBy = UpdatedBy;
        taskTemplate.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            taskTemplate.Status = Status;
        }
    }
}

public sealed record UpdateTaskTemplateStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record TaskTemplateResponse(
    string Id,
    string TemplateName,
    string Code,
    MasterDataReferenceDto? MaintenanceType,
    double? EstimatedDuration,
    string? Description,
    IReadOnlyList<MasterDataStepDto> Tasks,
    int TaskCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static TaskTemplateResponse FromEntity(TaskTemplateEntity taskTemplate)
    {
        var tasks = (taskTemplate.Tasks ?? []).Select(MasterDataStepDto.FromStep).ToList();

        return new TaskTemplateResponse(
            taskTemplate.Id ?? string.Empty,
            taskTemplate.TemplateName ?? string.Empty,
            taskTemplate.Code ?? string.Empty,
            taskTemplate.MaintenanceType is null
                ? null
                : MasterDataReferenceDto.FromReference(taskTemplate.MaintenanceType),
            taskTemplate.EstimatedDuration,
            taskTemplate.Description,
            tasks,
            tasks.Count,
            taskTemplate.CreatedBy,
            taskTemplate.CreatedAt,
            taskTemplate.UpdatedBy,
            taskTemplate.UpdatedAt,
            taskTemplate.ClientId,
            taskTemplate.TenantId,
            taskTemplate.Status,
            taskTemplate.IsDeleted
        );
    }
}
