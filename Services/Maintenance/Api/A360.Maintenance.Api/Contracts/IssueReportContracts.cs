using IssueReportEntity = A360.Maintenance.Domain.Entities.IssueReport;

namespace A360.Maintenance.Api.Contracts;

/// <summary>Editable service request fields shared by create and update.</summary>
public abstract record IssueReportFields
{
    public string? IssueId { get; init; }
    public string? AssetId { get; init; }
    public string? AssetName { get; init; }
    public string? ReportedBy { get; init; }
    public string? IssueType { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Location { get; init; }
    public DateTime? RequestedDate { get; init; }
    public DateTime? TargetDate { get; init; }
    public string? AssignedTo { get; init; }
    public string? WorkOrderId { get; init; }
    public DateTime? ResolvedDate { get; init; }
    public string? ResolutionNotes { get; init; }
    public string? Status { get; init; }

    protected void CopyTo(IssueReportEntity issueReport)
    {
        issueReport.IssueId = IssueId!;
        issueReport.AssetId = AssetId!;
        issueReport.AssetName = AssetName ?? string.Empty;
        issueReport.ReportedBy = ReportedBy;
        issueReport.IssueType = IssueType!;
        issueReport.Severity = Severity!;
        issueReport.Description = Description;
        issueReport.Category = Category;
        issueReport.Location = Location;
        issueReport.RequestedDate = RequestedDate ?? issueReport.RequestedDate ?? DateTime.UtcNow;
        issueReport.TargetDate = TargetDate;
        issueReport.AssignedTo = AssignedTo;
        issueReport.WorkOrderId = WorkOrderId ?? issueReport.WorkOrderId;
        issueReport.ResolutionNotes = ResolutionNotes;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            issueReport.Status = Status;
        }

        // Resolving stamps the date once; reopening clears it.
        issueReport.ResolvedDate = issueReport.Status is "Resolved" or "Closed"
            ? ResolvedDate ?? issueReport.ResolvedDate ?? DateTime.UtcNow
            : null;
    }
}

public sealed record CreateIssueReportRequest : IssueReportFields
{
    public string? CreatedBy { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }

    public IssueReportEntity ToEntity()
    {
        var issueReport = new IssueReportEntity
        {
            Status = "Open",
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        CopyTo(issueReport);
        return issueReport;
    }
}

public sealed record UpdateIssueReportRequest : IssueReportFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(IssueReportEntity issueReport)
    {
        CopyTo(issueReport);
        issueReport.UpdatedBy = UpdatedBy;
        issueReport.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record IssueReportResponse
{
    public string Id { get; init; } = string.Empty;
    public string IssueId { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string? ReportedBy { get; init; }
    public string IssueType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Location { get; init; }
    public DateTime? RequestedDate { get; init; }
    public DateTime? TargetDate { get; init; }
    public string? AssignedTo { get; init; }
    public string? WorkOrderId { get; init; }
    public DateTime? ResolvedDate { get; init; }
    public string? ResolutionNotes { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }
    public string? Status { get; init; }
    public bool IsDeleted { get; init; }

    public static IssueReportResponse FromEntity(IssueReportEntity issueReport) => new()
    {
        Id = issueReport.Id ?? string.Empty,
        IssueId = issueReport.IssueId ?? string.Empty,
        AssetId = issueReport.AssetId ?? string.Empty,
        AssetName = issueReport.AssetName ?? string.Empty,
        ReportedBy = issueReport.ReportedBy,
        IssueType = issueReport.IssueType ?? string.Empty,
        Severity = issueReport.Severity ?? string.Empty,
        Description = issueReport.Description,
        Category = issueReport.Category,
        Location = issueReport.Location,
        RequestedDate = issueReport.RequestedDate ?? issueReport.CreatedAt,
        TargetDate = issueReport.TargetDate,
        AssignedTo = issueReport.AssignedTo,
        WorkOrderId = issueReport.WorkOrderId,
        ResolvedDate = issueReport.ResolvedDate,
        ResolutionNotes = issueReport.ResolutionNotes,
        CreatedBy = issueReport.CreatedBy,
        CreatedAt = issueReport.CreatedAt,
        UpdatedBy = issueReport.UpdatedBy,
        UpdatedAt = issueReport.UpdatedAt,
        ClientId = issueReport.ClientId,
        TenantId = issueReport.TenantId,
        Status = issueReport.Status,
        IsDeleted = issueReport.IsDeleted
    };
}
