
using IssueReportEntity = A360.Maintenance.Domain.Entities.IssueReport;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateIssueReportRequest(
    string? IssueId,
    string? AssetId,
    string? AssetName,
    string? ReportedBy,
    string? IssueType,
    string? Severity,
    string? Description,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public IssueReportEntity ToEntity()
    {
        return new IssueReportEntity
        {
            IssueId = IssueId,
            AssetId = AssetId,
            AssetName = AssetName,
            ReportedBy = ReportedBy,
            IssueType = IssueType,
            Severity = Severity,
            Description = Description,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateIssueReportRequest(
    string? IssueId,
    string? AssetId,
    string? AssetName,
    string? ReportedBy,
    string? IssueType,
    string? Severity,
    string? Description,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(IssueReportEntity issueReport)
    {
        issueReport.IssueId = IssueId;
        issueReport.AssetId = AssetId;
        issueReport.AssetName = AssetName;
        issueReport.ReportedBy = ReportedBy;
        issueReport.IssueType = IssueType;
        issueReport.Severity = Severity;
        issueReport.Description = Description;

        issueReport.UpdatedBy = UpdatedBy;
        issueReport.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            issueReport.Status = Status;
        }
    }
}

public sealed record IssueReportResponse(
    string Id,
    string IssueId,
    string AssetId,
    string AssetName,
    string? ReportedBy,
    string IssueType,
    string Severity,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static IssueReportResponse FromEntity(IssueReportEntity issueReport)
    {
        return new IssueReportResponse(
            issueReport.Id ?? string.Empty,
            issueReport.IssueId ?? string.Empty,
            issueReport.AssetId ?? string.Empty,
            issueReport.AssetName ?? string.Empty,
            issueReport.ReportedBy,
            issueReport.IssueType ?? string.Empty,
            issueReport.Severity ?? string.Empty,
            issueReport.Description,
            issueReport.CreatedBy,
            issueReport.CreatedAt,
            issueReport.UpdatedBy,
            issueReport.UpdatedAt,
            issueReport.ClientId,
            issueReport.TenantId,
            issueReport.Status,
            issueReport.IsDeleted
        );
    }
}
