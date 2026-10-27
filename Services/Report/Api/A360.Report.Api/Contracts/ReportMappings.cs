using A360.Security;
using ReportEntity = A360.Report.Domain.Entities.Report;

namespace A360.Report.Api.Contracts;

internal static class ReportMappings
{
    public static ReportEntity ToEntity(this CreateReportRequest request, CurrentUser? currentUser)
    {
        return new ReportEntity
        {
            // Generated on the server so every report gets a stable id independent of the Mongo _id.
            ReportId = Guid.NewGuid().ToString(),
            ReportName = request.ReportName!.Trim(),
            ChooseFormat = request.ChooseFormat!.Trim(),
            Recurrence = TrimOrNull(request.Recurrence),
            TimeRange = TrimOrNull(request.TimeRange),
            SelectModule = request.SelectModule!.Trim(),
            SelectTemplate = TrimOrNull(request.SelectTemplate),
            ShareWith = (request.ShareWith ?? [])
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Select(recipient => recipient.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
