using A360.Security;
using DashboardEntity = A360.Dashboard.Domain.Entities.Dashboard;

namespace A360.Dashboard.Api.Contracts;

internal static class DashboardMappings
{
    public static DashboardEntity ToEntity(this CreateDashboardRequest request, CurrentUser? currentUser)
    {
        return new DashboardEntity
        {
            // Generated on the server so every dashboard gets a stable id independent of the Mongo _id.
            DashboardId = Guid.NewGuid().ToString(),
            DashboardName = request.DashboardName!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    public static void ApplyTo(this UpdateDashboardRequest request, DashboardEntity dashboard, CurrentUser? currentUser)
    {
        dashboard.DashboardName = request.DashboardName!.Trim();
        dashboard.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        dashboard.UpdatedAt = DateTime.UtcNow;
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
