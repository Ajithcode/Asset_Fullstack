using ReportEntity = A360.Report.Domain.Entities.Report;

namespace A360.Report.Api.Contracts;

public sealed record CreateReportRequest(
    string? ReportName,
    string? ChooseFormat,
    string? Recurrence,
    string? TimeRange,
    string? SelectModule,
    string? SelectTemplate,
    List<string>? ShareWith,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record ReportResponse(
    string ReportId,
    string ReportName,
    string ChooseFormat,
    string? Recurrence,
    string? TimeRange,
    string SelectModule,
    string? SelectTemplate,
    IReadOnlyCollection<string> ShareWith,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static ReportResponse FromEntity(ReportEntity report)
    {
        return new ReportResponse(
            report.ReportId,
            report.ReportName,
            report.ChooseFormat,
            report.Recurrence,
            report.TimeRange,
            report.SelectModule,
            report.SelectTemplate,
            report.ShareWith,
            report.CreatedBy,
            report.CreatedAt,
            report.UpdatedBy,
            report.UpdatedAt,
            report.TenantId,
            report.ClientId);
    }
}
