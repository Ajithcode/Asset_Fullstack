using DashboardEntity = A360.Dashboard.Domain.Entities.Dashboard;

namespace A360.Dashboard.Api.Contracts;

public sealed record CreateDashboardRequest(
    string? DashboardName,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record UpdateDashboardRequest(
    string? DashboardName,
    string? UpdatedBy);

public sealed record DashboardResponse(
    string DashboardId,
    string DashboardName,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static DashboardResponse FromEntity(DashboardEntity dashboard)
    {
        return new DashboardResponse(
            dashboard.DashboardId,
            dashboard.DashboardName,
            dashboard.CreatedBy,
            dashboard.CreatedAt,
            dashboard.UpdatedBy,
            dashboard.UpdatedAt,
            dashboard.TenantId,
            dashboard.ClientId);
    }
}
