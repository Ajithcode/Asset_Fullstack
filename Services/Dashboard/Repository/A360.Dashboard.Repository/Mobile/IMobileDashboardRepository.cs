namespace A360.Dashboard.Repository.Mobile;

/// <summary>Read-only access to the records behind the mobile dashboard screens. Nothing here writes.</summary>
public interface IMobileDashboardRepository
{
    /// <summary>Every registered (not deleted) asset.</summary>
    Task<IReadOnlyList<MobileAsset>> GetAssetsAsync(CancellationToken cancellationToken = default);

    /// <summary>Work orders still open, plus every work order scheduled, due, finished or created at or after <paramref name="sinceUtc"/>.</summary>
    Task<IReadOnlyList<MobileWorkOrder>> GetWorkOrdersAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Every (not deleted) inspection; the checklist is reduced to task titles and results.</summary>
    Task<IReadOnlyList<MobileInspection>> GetInspectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Approval tasks that are still waiting for a decision.</summary>
    Task<IReadOnlyList<MobileApprovalTask>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    /// <summary>IoT alerts raised at or after <paramref name="sinceUtc"/>, most recent first.</summary>
    Task<IReadOnlyList<MobileIoTAlert>> GetAlertsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Spare parts at or below their minimum stock level.</summary>
    Task<IReadOnlyList<MobileSparePart>> GetLowStockSparePartsAsync(CancellationToken cancellationToken = default);

    /// <summary>Preventive-maintenance schedules whose next due date is in [from, to).</summary>
    Task<IReadOnlyList<MobilePmSchedule>> GetPmSchedulesDueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// The names a user can appear under in assignee fields (user name, short name, email) — looked up by user id,
    /// falling back to the user name. Empty when the user is unknown.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserAssigneeNamesAsync(string? userId, string? userName, CancellationToken cancellationToken = default);

    /// <summary>Latest condition assessment per asset registration id (case-insensitive); assets never assessed are absent.</summary>
    Task<IReadOnlyDictionary<string, MobileAssetHealthScore>> GetLatestHealthScoresAsync(CancellationToken cancellationToken = default);
}
