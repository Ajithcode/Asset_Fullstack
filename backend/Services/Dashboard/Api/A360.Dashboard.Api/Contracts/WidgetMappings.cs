using A360.Security;
using WidgetEntity = A360.Dashboard.Domain.Entities.Widget;

namespace A360.Dashboard.Api.Contracts;

internal static class WidgetMappings
{
    public static WidgetEntity ToEntity(this CreateWidgetRequest request, CurrentUser? currentUser)
    {
        return new WidgetEntity
        {
            // Generated on the server so every widget gets a stable id independent of the Mongo _id.
            WidgetId = Guid.NewGuid().ToString(),
            DashboardId = request.DashboardId!.Trim(),
            ModuleName = request.ModuleName!.Trim(),
            WidgetList = CleanList(request.WidgetList),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    public static void ApplyTo(this UpdateWidgetRequest request, WidgetEntity widget, CurrentUser? currentUser)
    {
        widget.DashboardId = request.DashboardId!.Trim();
        widget.ModuleName = request.ModuleName!.Trim();
        widget.WidgetList = CleanList(request.WidgetList);
        widget.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        widget.UpdatedAt = DateTime.UtcNow;
    }

    private static List<string> CleanList(IReadOnlyList<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct()
            .ToList() ?? [];
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
