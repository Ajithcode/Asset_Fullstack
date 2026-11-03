
using IssueMasterEntity = A360.Wip.Domain.Entities.IssueMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateIssueMasterRequest(
    string? IssueId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? IssueType,
    string? Description,
    string? ReportedBy,
    DateTime? ReportedDate,
    string? Severity,
    string? ResolutionRemarks,
    DateTime? ClosedDate,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public IssueMasterEntity ToEntity()
    {
        return new IssueMasterEntity
        {
            IssueId = IssueId,
            AssetId = AssetId,
            AssetName = AssetName,
            JobId = JobId,
            TaskId = TaskId,

            IssueType = IssueType,
            Description = Description,
            ReportedBy = ReportedBy,
            ReportedDate = ReportedDate,

            Severity = Severity,
            ResolutionRemarks = ResolutionRemarks,
            ClosedDate = ClosedDate,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateIssueMasterRequest(
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? IssueType,
    string? Description,
    string? ReportedBy,
    DateTime? ReportedDate,
    string? Severity,
    string? ResolutionRemarks,
    DateTime? ClosedDate,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(IssueMasterEntity issueMaster)
    {
        issueMaster.AssetId = AssetId;
        issueMaster.AssetName = AssetName;
        issueMaster.JobId = JobId;
        issueMaster.TaskId = TaskId;

        issueMaster.IssueType = IssueType;
        issueMaster.Description = Description;
        issueMaster.ReportedBy = ReportedBy;
        issueMaster.ReportedDate = ReportedDate;

        issueMaster.Severity = Severity;
        issueMaster.ResolutionRemarks = ResolutionRemarks;
        issueMaster.ClosedDate = ClosedDate;

        issueMaster.UpdatedBy = UpdatedBy;
        issueMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            issueMaster.Status = Status;
        }
    }
}

public sealed record IssueMasterResponse(
    string Id,
    string IssueId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskId,
    string? IssueType,
    string? Description,
    string? ReportedBy,
    DateTime? ReportedDate,
    string? Severity,
    string? ResolutionRemarks,
    DateTime? ClosedDate,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static IssueMasterResponse FromEntity(IssueMasterEntity issueMaster)
    {
        return new IssueMasterResponse(
            issueMaster.Id ?? string.Empty,
            issueMaster.IssueId ?? string.Empty,
            issueMaster.AssetId,
            issueMaster.AssetName,
            issueMaster.JobId,
            issueMaster.TaskId,

            issueMaster.IssueType,
            issueMaster.Description,
            issueMaster.ReportedBy,
            issueMaster.ReportedDate,

            issueMaster.Severity,
            issueMaster.ResolutionRemarks,
            issueMaster.ClosedDate,

            issueMaster.CreatedBy,
            issueMaster.CreatedAt,
            issueMaster.UpdatedBy,
            issueMaster.UpdatedAt,

            issueMaster.ClientId,
            issueMaster.TenantId,
            issueMaster.Status,
            issueMaster.IsDeleted
        );
    }
}
