using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Api.Contracts;

public sealed record CreateWidgetRequest(
    string? DashboardId,
    string? ModuleName,
    IReadOnlyList<string>? WidgetList,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record UpdateWidgetRequest(
    string? DashboardId,
    string? ModuleName,
    IReadOnlyList<string>? WidgetList,
    string? UpdatedBy);

public sealed record WidgetResponse(
    string WidgetId,
    string DashboardId,
    string ModuleName,
    IReadOnlyList<string> WidgetList,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static WidgetResponse FromEntity(WidgetEntity widget)
    {
        return new WidgetResponse(
            widget.WidgetId,
            widget.DashboardId,
            widget.ModuleName,
            widget.WidgetList,
            widget.CreatedBy,
            widget.CreatedAt,
            widget.UpdatedBy,
            widget.UpdatedAt,
            widget.TenantId,
            widget.ClientId);
    }
}
