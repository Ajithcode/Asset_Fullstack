
using JobMasterEntity = A360.Wip.Domain.Entities.JobMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateJobMasterRequest(
    string? JobId,
    string? JobName,
    string? Description,
    string? AssetId,
    string? AssetCategory,
    string? LocationId,
    string? DepartmentId,
    string? WorkType,
    string? Priority,
    DateTime? PlannedStartDate,
    DateTime? PlannedEndDate,
    string? AssignedTo,
    string? SupervisorId,
    double? ProgressPercentage,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public JobMasterEntity ToEntity()
    {
        return new JobMasterEntity
        {
            JobId = JobId,
            JobName = JobName,
            Description = Description,

            AssetId = AssetId,
            AssetCategory = AssetCategory,
            LocationId = LocationId,
            DepartmentId = DepartmentId,

            WorkType = WorkType,
            Priority = Priority,

            PlannedStartDate = PlannedStartDate,
            PlannedEndDate = PlannedEndDate,

            AssignedTo = AssignedTo,
            SupervisorId = SupervisorId,
            ProgressPercentage = ProgressPercentage,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateJobMasterRequest(
    string? JobName,
    string? Description,
    string? AssetId,
    string? AssetCategory,
    string? LocationId,
    string? DepartmentId,
    string? WorkType,
    string? Priority,
    DateTime? PlannedStartDate,
    DateTime? PlannedEndDate,
    string? AssignedTo,
    string? SupervisorId,
    double? ProgressPercentage,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(JobMasterEntity jobMaster)
    {
        jobMaster.JobName = JobName;
        jobMaster.Description = Description;

        jobMaster.AssetId = AssetId;
        jobMaster.AssetCategory = AssetCategory;
        jobMaster.LocationId = LocationId;
        jobMaster.DepartmentId = DepartmentId;

        jobMaster.WorkType = WorkType;
        jobMaster.Priority = Priority;

        jobMaster.PlannedStartDate = PlannedStartDate;
        jobMaster.PlannedEndDate = PlannedEndDate;

        jobMaster.AssignedTo = AssignedTo;
        jobMaster.SupervisorId = SupervisorId;
        jobMaster.ProgressPercentage = ProgressPercentage;

        jobMaster.UpdatedBy = UpdatedBy;
        jobMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            jobMaster.Status = Status;
        }
    }
}

public sealed record JobMasterResponse(
    string Id,
    string JobId,
    string JobName,
    string? Description,
    string AssetId,
    string? AssetCategory,
    string? LocationId,
    string? DepartmentId,
    string? WorkType,
    string? Priority,
    DateTime? PlannedStartDate,
    DateTime? PlannedEndDate,
    string? AssignedTo,
    string? SupervisorId,
    double? ProgressPercentage,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static JobMasterResponse FromEntity(JobMasterEntity jobMaster)
    {
        return new JobMasterResponse(
            jobMaster.Id ?? string.Empty,
            jobMaster.JobId ?? string.Empty,
            jobMaster.JobName ?? string.Empty,
            jobMaster.Description,

            jobMaster.AssetId ?? string.Empty,
            jobMaster.AssetCategory,
            jobMaster.LocationId,
            jobMaster.DepartmentId,

            jobMaster.WorkType,
            jobMaster.Priority,

            jobMaster.PlannedStartDate,
            jobMaster.PlannedEndDate,

            jobMaster.AssignedTo,
            jobMaster.SupervisorId,
            jobMaster.ProgressPercentage,

            jobMaster.CreatedBy,
            jobMaster.CreatedAt,
            jobMaster.UpdatedBy,
            jobMaster.UpdatedAt,

            jobMaster.ClientId,
            jobMaster.TenantId,
            jobMaster.Status,
            jobMaster.IsDeleted
        );
    }
}
