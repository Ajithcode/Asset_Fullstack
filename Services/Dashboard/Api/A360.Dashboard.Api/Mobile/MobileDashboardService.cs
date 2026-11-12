using System.Globalization;
using A360.Dashboard.Api.Contracts;
using A360.Dashboard.Repository.Mobile;
using A360.Security;

namespace A360.Dashboard.Api.Mobile;

/// <summary>
/// Builds the mobile app dashboard screens from the records other services own (assets, work orders, inspections,
/// approvals, IoT alerts, spare parts, PM schedules, condition assessments). Read-only.
/// </summary>
public sealed class MobileDashboardService
{
    public const int DefaultMaintenanceMonths = 6;
    public const int DefaultAlertDays = 7;
    public const int DefaultUpcomingDays = 30;

    private const int HomeAlertCount = 3;
    private const int HomeMapPinCount = 50;
    private const int MaintenanceDueSoonDays = 3;
    private const int HealthIssueAlertDays = 30;

    private readonly IMobileDashboardRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly string _currency;

    public MobileDashboardService(IMobileDashboardRepository repository, ICurrentUserAccessor currentUser, IConfiguration configuration)
    {
        _repository = repository;
        _currentUser = currentUser;
        _currency = configuration["MobileDashboard:Currency"] is { Length: > 0 } currency ? currency : "AED";
    }

    // ================================================================ Home

    public async Task<MobileHomeResponse> GetHomeAsync(MobileClock clock, string? site, CancellationToken cancellationToken)
    {
        var maintenanceWindow = MonthWindow(clock.UtcNow, DefaultMaintenanceMonths);

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(maintenanceWindow.FromUtc, cancellationToken);
        var inspectionsTask = _repository.GetInspectionsAsync(cancellationToken);
        var approvalsTask = _repository.GetPendingApprovalsAsync(cancellationToken);
        var healthTask = _repository.GetLatestHealthScoresAsync(cancellationToken);
        var alertFeedTask = BuildAlertFeedAsync(clock.UtcNow, DefaultAlertDays, site, cancellationToken);

        await Task.WhenAll(assetsTask, workOrdersTask, inspectionsTask, approvalsTask, healthTask, alertFeedTask);

        var scope = new SiteScope(site, new AssetIndex(assetsTask.Result));
        var assets = assetsTask.Result.Where(scope.Includes).ToList();
        var workOrders = workOrdersTask.Result.Where(scope.Includes).ToList();
        var inspections = inspectionsTask.Result.Where(scope.Includes).ToList();
        var approvals = scope.Filter(approvalsTask.Result);
        var alerts = alertFeedTask.Result;

        var today = clock.Today;
        var openInspections = inspections.Where(inspection => !IsInspectionClosed(inspection.Status)).ToList();

        var quickActions = new MobileQuickActionCounts(
            workOrders.Count(order => !IsWorkOrderClosed(order.Status)),
            openInspections.Count,
            approvals.Count,
            alerts.Count(alert => alert.Severity == Severity.Critical && !IsAlertResolved(alert.Status)));

        var maintenance = ClassifyWorkOrders(workOrders, maintenanceWindow, clock.UtcNow);

        var upcoming = new List<MobileUpcomingActivitySummary>
        {
            new("WorkOrder", "Work Orders", workOrders.Count(order => !IsWorkOrderClosed(order.Status) && WorkOrderStart(order) is { } start && clock.ToLocalDate(start) == today), "Today"),
            new("Inspection", "Inspections", openInspections.Count(inspection => InspectionStart(inspection) is { } start && clock.ToLocalDate(start) == today), "Today"),
            new("Approval", "Approvals", approvals.Count, "Pending")
        };

        var located = LocatedAssets(assets).ToList();
        var health = AssessHealth(assets, healthTask.Result);
        var healthBreakdown = HealthBreakdown(health);

        return new MobileHomeResponse(
            BuildGreeting(clock),
            quickActions,
            new MobileAssetOverviewCard(assets.Count, StatusBreakdown(assets)),
            new MobileMaintenancePerformanceCard(maintenance.PlannedPercent, DefaultMaintenanceMonths, maintenance.Breakdown),
            upcoming,
            alerts.Take(HomeAlertCount).Select(alert => alert.ToResponse()).ToList(),
            new MobileAssetLocationCard(located.Count, Center(located), located.Take(HomeMapPinCount).Select(ToLocationItem).ToList()),
            new MobileAssetHealthCard(health.Count, Share(health.Count(item => item.Bucket == HealthBucket.Healthy), health.Count), healthBreakdown));
    }

    // ================================================================ Asset overview

    public async Task<MobileAssetOverviewResponse> GetAssetOverviewAsync(string? search, string? site, CancellationToken cancellationToken)
    {
        var all = await _repository.GetAssetsAsync(cancellationToken);
        var scope = new SiteScope(site, new AssetIndex(all));
        var assets = all.Where(scope.Includes).ToList();

        return new MobileAssetOverviewResponse(
            assets.Count,
            FilterGroups(StatusBreakdown(assets), search),
            FilterGroups(GroupBreakdown(assets, asset => asset.AssetType), search),
            FilterGroups(GroupBreakdown(assets, asset => asset.Location?.SiteName), search));
    }

    // ================================================================ Maintenance performance

    public async Task<MobileMaintenancePerformanceResponse> GetMaintenancePerformanceAsync(
        MobileClock clock, int months, string? assetType, string? site, CancellationToken cancellationToken)
    {
        var window = MonthWindow(clock.UtcNow, months);
        var previous = new Window(window.FromUtc - (window.ToUtc - window.FromUtc), window.FromUtc);

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(previous.FromUtc, cancellationToken);
        await Task.WhenAll(assetsTask, workOrdersTask);

        var index = new AssetIndex(assetsTask.Result);
        var scope = new SiteScope(site, index);
        var workOrders = workOrdersTask.Result
            .Where(order => Matches(index.TypeOf(order.AssetId), assetType))
            .Where(scope.Includes)
            .ToList();

        var current = ClassifyWorkOrders(workOrders, window, clock.UtcNow);
        var prior = ClassifyWorkOrders(workOrders, previous, clock.UtcNow);

        var trend = MonthsOf(window)
            .Select(month =>
            {
                var inMonth = current.Items.Where(item => item.Date.Year == month.Year && item.Date.Month == month.Month).ToList();
                return new MobileWorkOrderTrendPoint(
                    month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    month.ToString("MMM", CultureInfo.InvariantCulture),
                    inMonth.Count(item => item.Kind == WorkOrderKind.Planned),
                    inMonth.Count(item => item.Kind == WorkOrderKind.Unplanned),
                    inMonth.Count(item => item.Kind == WorkOrderKind.Overdue));
            })
            .ToList();

        var mttr = Mttr(current.Items);
        var previousMttr = Mttr(prior.Items);
        var mtbf = Mtbf(current.Items, window);
        var previousMtbf = Mtbf(prior.Items, previous);

        var byAssetType = current.Items
            .GroupBy(item => index.TypeOf(item.Order.AssetId) ?? "Others", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var planned = group.Count(item => item.Kind == WorkOrderKind.Planned);
                return new MobileAssetTypeMaintenance(
                    group.Key,
                    group.Count(),
                    planned,
                    group.Count(item => item.Kind == WorkOrderKind.Unplanned),
                    group.Count(item => item.Kind == WorkOrderKind.Overdue),
                    Share(planned, group.Count()));
            })
            .OrderByDescending(row => row.Total)
            .ToList();

        return new MobileMaintenancePerformanceResponse(
            new MobilePeriod($"Last {months} Months", window.FromUtc, window.ToUtc),
            current.PlannedPercent,
            current.Counts,
            current.Breakdown,
            trend,
            new MobileMetricValue(Round(mttr ?? 0), "hrs", Change(mttr, previousMttr), Improved(mttr, previousMttr, lowerIsBetter: true)),
            new MobileMetricValue(Round(mtbf ?? 0), "days", Change(mtbf, previousMtbf), Improved(mtbf, previousMtbf, lowerIsBetter: false)),
            byAssetType);
    }

    // ================================================================ Upcoming activities

    public async Task<MobileUpcomingActivitiesResponse> GetUpcomingActivitiesAsync(
        MobileClock clock, DateOnly? from, DateOnly? to, string? type, string? site, CancellationToken cancellationToken)
    {
        var fromDate = from ?? clock.Today;
        var toDate = to ?? fromDate.AddDays(DefaultUpcomingDays - 1);
        var fromUtc = clock.ToUtc(fromDate);

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(fromUtc, cancellationToken);
        var inspectionsTask = _repository.GetInspectionsAsync(cancellationToken);
        var approvalsTask = _repository.GetPendingApprovalsAsync(cancellationToken);
        await Task.WhenAll(assetsTask, workOrdersTask, inspectionsTask, approvalsTask);

        var index = new AssetIndex(assetsTask.Result);
        var scope = new SiteScope(site, index);
        var items = new List<MobileActivityItem>();

        if (IncludesType(type, "workorder"))
        {
            foreach (var order in workOrdersTask.Result.Where(order => !IsWorkOrderClosed(order.Status) && scope.Includes(order)))
            {
                if (WorkOrderStart(order) is not { } start)
                {
                    continue;
                }

                var asset = index.Find(order.AssetId);
                items.Add(new MobileActivityItem(
                    Text(order.Id) ?? order.WorkOrderId, "WorkOrder", order.WorkOrderId, order.Title ?? "Work Order",
                    asset?.AssetTag ?? order.AssetId, order.AssetName ?? asset?.AssetName,
                    JoinLocation(order.Building, order.Area) ?? LocationLabel(asset),
                    start, order.PlannedEndDate, clock.ToLocalDate(start),
                    order.Status ?? "Open", order.Priority, order.AssignedTechnician));
            }
        }

        if (IncludesType(type, "inspection"))
        {
            foreach (var inspection in inspectionsTask.Result.Where(inspection => !IsInspectionClosed(inspection.Status) && scope.Includes(inspection)))
            {
                if (InspectionStart(inspection) is not { } start)
                {
                    continue;
                }

                var asset = index.Find(inspection.AssetCode);
                items.Add(new MobileActivityItem(
                    Text(inspection.Id) ?? inspection.InspectionCode, "Inspection", inspection.InspectionCode,
                    inspection.Title ?? inspection.InspectionTypeName ?? "Inspection",
                    asset?.AssetTag ?? inspection.AssetCode, inspection.AssetName ?? asset?.AssetName,
                    inspection.Location ?? inspection.SiteName,
                    start, ParseDate(inspection.ScheduledEnd), clock.ToLocalDate(start),
                    inspection.Status ?? "Scheduled", inspection.Priority, inspection.AssignedTo));
            }
        }

        if (IncludesType(type, "approval"))
        {
            foreach (var approval in scope.Filter(approvalsTask.Result))
            {
                if ((approval.DueDate ?? approval.CreatedAt) is not { } start)
                {
                    continue;
                }

                var id = Text(approval.Id) ?? string.Empty;
                items.Add(new MobileActivityItem(
                    id, "Approval", id, approval.WorkflowName ?? "Approval",
                    null, approval.AssetRequest, null,
                    start, null, clock.ToLocalDate(start),
                    approval.Status ?? "Pending", approval.Priority, approval.AssignedTo));
            }
        }

        var inRange = items
            .Where(item => item.Date >= fromDate && item.Date <= toDate)
            .OrderBy(item => item.StartUtc)
            .ToList();

        var days = inRange
            .GroupBy(item => item.Date)
            .OrderBy(group => group.Key)
            .Select(group => new MobileActivityDay(
                group.Key,
                group.Count(item => item.Type == "WorkOrder"),
                group.Count(item => item.Type == "Inspection"),
                group.Count(item => item.Type == "Approval"),
                group.Count()))
            .ToList();

        return new MobileUpcomingActivitiesResponse(fromDate, toDate, inRange.Count, days, inRange);
    }

    // ================================================================ My tasks

    /// <summary>
    /// Open work orders (assigned technician), inspections (assigned to) and approvals (assigned to) whose assignee is
    /// the signed-in user under any of their names. Null when the request has no signed-in user.
    /// </summary>
    public async Task<MobileMyTasksResponse?> GetMyTasksAsync(MobileClock clock, string? type, string? site, CancellationToken cancellationToken)
    {
        var user = _currentUser.GetCurrentUser();
        if (user is null)
        {
            return null;
        }

        var names = await _repository.GetUserAssigneeNamesAsync(user.UserId, user.UserName, cancellationToken);
        bool Mine(string? assignee) =>
            !string.IsNullOrWhiteSpace(assignee) && names.Any(name => name.Equals(assignee.Trim(), StringComparison.OrdinalIgnoreCase));

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(clock.UtcNow, cancellationToken);
        var inspectionsTask = _repository.GetInspectionsAsync(cancellationToken);
        var approvalsTask = _repository.GetPendingApprovalsAsync(cancellationToken);
        await Task.WhenAll(assetsTask, workOrdersTask, inspectionsTask, approvalsTask);

        var index = new AssetIndex(assetsTask.Result);
        var scope = new SiteScope(site, index);
        var items = new List<MobileTaskItem>();

        MobileTaskItem Item(string id, string kind, string reference, string title, string? assetId, string? assetName, string? location, DateTime? due, string status, string? priority) =>
            new(id, kind, reference, title, assetId, assetName, location, due, due is null ? null : clock.ToLocalDate(due.Value),
                due is not null && due.Value < clock.UtcNow, status, priority);

        if (IncludesType(type, "workorder"))
        {
            foreach (var order in workOrdersTask.Result.Where(order => !IsWorkOrderClosed(order.Status) && Mine(order.AssignedTechnician) && scope.Includes(order)))
            {
                var asset = index.Find(order.AssetId);
                items.Add(Item(Text(order.Id) ?? order.WorkOrderId, "WorkOrder", order.WorkOrderId, order.Title ?? "Work Order",
                    asset?.AssetTag ?? order.AssetId, order.AssetName ?? asset?.AssetName,
                    JoinLocation(order.Building, order.Area) ?? LocationLabel(asset),
                    order.DueDate ?? WorkOrderStart(order), order.Status ?? "Open", order.Priority));
            }
        }

        if (IncludesType(type, "inspection"))
        {
            foreach (var inspection in inspectionsTask.Result.Where(inspection => !IsInspectionClosed(inspection.Status) && Mine(inspection.AssignedTo) && scope.Includes(inspection)))
            {
                var asset = index.Find(inspection.AssetCode);
                items.Add(Item(Text(inspection.Id) ?? inspection.InspectionCode, "Inspection", inspection.InspectionCode,
                    inspection.Title ?? inspection.InspectionTypeName ?? "Inspection",
                    asset?.AssetTag ?? inspection.AssetCode, inspection.AssetName ?? asset?.AssetName,
                    inspection.Location ?? inspection.SiteName,
                    ParseDate(inspection.DueDate) ?? InspectionStart(inspection), inspection.Status ?? "Scheduled", inspection.Priority));
            }
        }

        if (IncludesType(type, "approval"))
        {
            foreach (var approval in scope.Filter(approvalsTask.Result).Where(approval => Mine(approval.AssignedTo)))
            {
                var id = Text(approval.Id) ?? string.Empty;
                items.Add(Item(id, "Approval", id, approval.WorkflowName ?? "Approval", null, approval.AssetRequest, null,
                    approval.DueDate, approval.Status ?? "Pending", approval.Priority));
            }
        }

        // Overdue first, then by due date; undated tasks last.
        var ordered = items
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.DueUtc ?? DateTime.MaxValue)
            .ToList();

        var counts = new MobileMyTaskCounts(
            ordered.Count,
            ordered.Count(item => item.Type == "WorkOrder"),
            ordered.Count(item => item.Type == "Inspection"),
            ordered.Count(item => item.Type == "Approval"),
            ordered.Count(item => item.IsOverdue),
            ordered.Count(item => item.DueDate == clock.Today));

        return new MobileMyTasksResponse(user.UserName, names, counts, ordered);
    }

    // ================================================================ Recent alerts

    public async Task<MobileRecentAlertsResponse> GetRecentAlertsAsync(
        MobileClock clock, int days, string? severity, string? site, int page, int pageSize, CancellationToken cancellationToken)
    {
        var alerts = await BuildAlertFeedAsync(clock.UtcNow, days, site, cancellationToken);

        var counts = new MobileAlertCounts(
            alerts.Count,
            alerts.Count(alert => alert.Severity == Severity.Critical),
            alerts.Count(alert => alert.Severity == Severity.Warning),
            alerts.Count(alert => alert.Severity == Severity.Info));

        var filtered = string.IsNullOrWhiteSpace(severity) || severity.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? alerts
            : alerts.Where(alert => alert.Severity.Equals(severity.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        return new MobileRecentAlertsResponse(
            counts,
            page,
            pageSize,
            filtered.Count,
            filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(alert => alert.ToResponse()).ToList());
    }

    // ================================================================ Asset location

    public async Task<MobileAssetLocationResponse> GetAssetLocationsAsync(string? status, string? search, string? site, CancellationToken cancellationToken)
    {
        var assets = await _repository.GetAssetsAsync(cancellationToken);
        var scope = new SiteScope(site, new AssetIndex(assets));
        var located = LocatedAssets(assets).Where(scope.Includes).Where(asset => MatchesSearch(asset, search)).ToList();
        var filtered = located.Where(asset => MatchesStatusGroup(asset, status)).ToList();

        return new MobileAssetLocationResponse(
            filtered.Count,
            Center(filtered.Count > 0 ? filtered : located),
            StatusBreakdown(located),
            filtered.Select(ToLocationItem).ToList());
    }

    // ================================================================ Asset health

    public async Task<MobileAssetHealthResponse> GetAssetHealthAsync(MobileClock clock, int top, string? site, CancellationToken cancellationToken)
    {
        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var healthTask = _repository.GetLatestHealthScoresAsync(cancellationToken);
        var alertsTask = _repository.GetAlertsAsync(clock.UtcNow.AddDays(-HealthIssueAlertDays), cancellationToken);
        await Task.WhenAll(assetsTask, healthTask, alertsTask);

        var scope = new SiteScope(site, new AssetIndex(assetsTask.Result));
        var health = AssessHealth(assetsTask.Result.Where(scope.Includes).ToList(), healthTask.Result);

        // The newest unresolved alert per asset explains a poor score better than anything else we have.
        var latestAlert = alertsTask.Result
            .Where(alert => !IsAlertResolved(alert.Status) && !string.IsNullOrWhiteSpace(alert.AssetRegistrationId))
            .GroupBy(alert => alert.AssetRegistrationId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var critical = health
            .Where(item => item.Bucket == HealthBucket.Critical)
            .OrderBy(item => item.Score ?? decimal.MaxValue)
            .Take(top)
            .Select(item => new MobileCriticalAsset(
                item.Asset.AssetRegistrationId,
                item.Asset.AssetTag,
                item.Asset.AssetName,
                item.Asset.AssetType,
                item.Score,
                IssueOf(item, latestAlert)))
            .ToList();

        return new MobileAssetHealthResponse(
            health.Count,
            Share(health.Count(item => item.Bucket == HealthBucket.Healthy), health.Count),
            HealthBreakdown(health),
            HealthGroups(health, item => item.Asset.AssetType),
            HealthGroups(health, item => item.Asset.Location?.SiteName),
            critical);
    }

    // ================================================================ Cost summary

    public async Task<MobileCostSummaryResponse> GetCostSummaryAsync(MobileClock clock, string? period, int? year, string? site, CancellationToken cancellationToken)
    {
        var (label, window) = CostWindow(clock.UtcNow, period, year);
        var previous = new Window(window.FromUtc.AddYears(-1), window.ToUtc.AddYears(-1));

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(previous.FromUtc, cancellationToken);
        await Task.WhenAll(assetsTask, workOrdersTask);

        var index = new AssetIndex(assetsTask.Result);
        var scope = new SiteScope(site, index);
        var costs = workOrdersTask.Result
            .Where(order => !IsCancelled(order.Status) && scope.Includes(order))
            .Select(order => new WorkOrderCost(order, CostDate(order), Labour(order), (decimal)order.AdditionalCost, Parts(order)))
            .Where(cost => cost.Date is not null && cost.Total > 0)
            .ToList();

        var current = costs.Where(cost => window.Contains(cost.Date!.Value)).ToList();
        var total = current.Sum(cost => cost.Total);
        var previousTotal = costs.Where(cost => previous.Contains(cost.Date!.Value)).Sum(cost => cost.Total);

        var maintenance = current.Sum(cost => cost.Maintenance);
        var operations = current.Sum(cost => cost.Operations);
        var spareParts = current.Sum(cost => cost.SpareParts);

        var categories = new List<MobileCostCategory>
        {
            new("maintenance", "Maintenance", Round(maintenance), Share(maintenance, total)),
            new("operations", "Operations", Round(operations), Share(operations, total)),
            new("spare-parts", "Spare Parts", Round(spareParts), Share(spareParts, total))
        };

        var trend = MonthsOf(window)
            .Select(month =>
            {
                var inMonth = current.Where(cost => cost.Date!.Value.Year == month.Year && cost.Date.Value.Month == month.Month).ToList();
                return new MobileCostTrendPoint(
                    month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    month.ToString("MMM", CultureInfo.InvariantCulture),
                    Round(inMonth.Sum(cost => cost.Maintenance)),
                    Round(inMonth.Sum(cost => cost.Operations)),
                    Round(inMonth.Sum(cost => cost.SpareParts)),
                    Round(inMonth.Sum(cost => cost.Total)));
            })
            .ToList();

        var byType = current
            .GroupBy(cost => index.TypeOf(cost.Order.AssetId) ?? "Others", StringComparer.OrdinalIgnoreCase)
            .Select(group => (Type: group.Key, Amount: group.Sum(cost => cost.Total)))
            .OrderByDescending(row => row.Amount)
            .ToList();

        // The chart lists the four biggest asset types and folds the rest into "Others".
        const int shownTypes = 4;
        var shown = byType.Where(row => !row.Type.Equals("Others", StringComparison.OrdinalIgnoreCase)).Take(shownTypes).ToList();
        var others = byType.Except(shown).Sum(row => row.Amount);
        var byAssetType = shown
            .Select(row => new MobileCostByAssetType(row.Type, Round(row.Amount), Share(row.Amount, total)))
            .ToList();
        if (others > 0)
        {
            byAssetType.Add(new MobileCostByAssetType("Others", Round(others), Share(others, total)));
        }

        return new MobileCostSummaryResponse(
            _currency,
            new MobilePeriod(label, window.FromUtc, window.ToUtc),
            Round(total),
            Round(previousTotal),
            Change(total, previousTotal),
            categories,
            trend,
            byAssetType);
    }

    // ================================================================ Recent assets

    public async Task<MobileRecentAssetsResponse> GetRecentAssetsAsync(
        string? status, string? search, string? assetType, string? site, int page, int pageSize, CancellationToken cancellationToken)
    {
        var assets = await _repository.GetAssetsAsync(cancellationToken);
        var searched = assets
            .Where(asset => MatchesSearch(asset, search))
            .Where(asset => Matches(asset.AssetType, assetType))
            .Where(asset => SiteScope.Matches(site, asset.Location?.SiteName, asset.Location?.SiteId))
            .ToList();

        var filtered = searched
            .Where(asset => MatchesStatusGroup(asset, status))
            .OrderByDescending(asset => asset.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(asset => asset.AssetRegistrationId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var counts = new List<MobileBreakdownItem> { new("all", "All", searched.Count, searched.Count == 0 ? 0 : 100) };
        counts.AddRange(StatusBreakdown(searched));

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(asset => new MobileRecentAssetItem(
                asset.AssetRegistrationId,
                asset.AssetTag,
                asset.AssetName,
                asset.BasicInformation?.Model,
                asset.BasicInformation?.Manufacturer,
                asset.Classification?.Category,
                asset.AssetType,
                asset.Status,
                StatusGroups.Of(asset.Status).Key,
                LocationLabel(asset),
                ImageOf(asset),
                asset.CreatedAt))
            .ToList();

        return new MobileRecentAssetsResponse(counts, page, pageSize, filtered.Count, items);
    }

    // ================================================================ Greeting

    private MobileGreeting BuildGreeting(MobileClock clock)
    {
        var hour = clock.LocalNow.Hour;
        var salutation = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening";
        var userName = _currentUser.GetCurrentUser()?.UserName;

        return new MobileGreeting(salutation, string.IsNullOrWhiteSpace(userName) ? null : userName, "Here's what's happening today", clock.UtcNow);
    }

    // ================================================================ Asset status groups

    private static IReadOnlyList<MobileBreakdownItem> StatusBreakdown(IReadOnlyCollection<MobileAsset> assets)
    {
        var groups = assets.GroupBy(asset => StatusGroups.Of(asset.Status).Key).ToDictionary(group => group.Key, group => group.Count());

        return StatusGroups.All
            .Where(group => group != StatusGroups.Other || groups.ContainsKey(group.Key))
            .Select(group =>
            {
                var count = groups.GetValueOrDefault(group.Key);
                return new MobileBreakdownItem(group.Key, group.Label, count, Share(count, assets.Count));
            })
            .ToList();
    }

    private static IReadOnlyList<MobileBreakdownItem> GroupBreakdown(IReadOnlyCollection<MobileAsset> assets, Func<MobileAsset, string?> keyOf)
    {
        return assets
            .GroupBy(asset => string.IsNullOrWhiteSpace(keyOf(asset)) ? "Others" : keyOf(asset)!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new MobileBreakdownItem(Slug(group.Key), group.Key, group.Count(), Share(group.Count(), assets.Count)))
            .OrderBy(item => item.Label.Equals("Others", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Count)
            .ToList();
    }

    private static IReadOnlyList<MobileBreakdownItem> FilterGroups(IReadOnlyList<MobileBreakdownItem> items, string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? items
            : items.Where(item => item.Label.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static bool MatchesStatusGroup(MobileAsset asset, string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            || status.Equals("all", StringComparison.OrdinalIgnoreCase)
            || StatusGroups.Of(asset.Status).Key.Equals(Slug(status), StringComparison.OrdinalIgnoreCase)
            || string.Equals(asset.Status?.Trim(), status.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(MobileAsset asset, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return new[]
            {
                asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.BasicInformation?.Model,
                asset.AssetType, asset.Classification?.Category, asset.Location?.SiteName, asset.Location?.BuildingName,
                asset.Location?.FloorName, asset.Location?.RoomName, asset.Location?.LocationPath
            }
            .Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
    }

    // ================================================================ Locations

    private static IEnumerable<MobileAsset> LocatedAssets(IEnumerable<MobileAsset> assets)
    {
        return assets.Where(asset =>
            asset.Location?.Latitude is { } latitude && asset.Location.Longitude is { } longitude
            && !(latitude == 0 && longitude == 0)
            && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180);
    }

    private static MobileGeoPoint? Center(IReadOnlyCollection<MobileAsset> located)
    {
        return located.Count == 0
            ? null
            : new MobileGeoPoint(
                Math.Round(located.Average(asset => asset.Location!.Latitude!.Value), 6),
                Math.Round(located.Average(asset => asset.Location!.Longitude!.Value), 6));
    }

    private static MobileAssetLocationItem ToLocationItem(MobileAsset asset)
    {
        return new MobileAssetLocationItem(
            asset.AssetRegistrationId,
            asset.AssetTag,
            asset.AssetName,
            asset.BasicInformation?.Model,
            asset.AssetType,
            asset.Status,
            StatusGroups.Of(asset.Status).Key,
            LocationLabel(asset),
            asset.Location!.Latitude!.Value,
            asset.Location.Longitude!.Value,
            ImageOf(asset));
    }

    /// <summary>"Building A - Floor 3"; falls back to room, zone, site or the full path.</summary>
    private static string? LocationLabel(MobileAsset? asset)
    {
        var location = asset?.Location;
        if (location is null)
        {
            return null;
        }

        return JoinLocation(location.BuildingName, location.FloorName)
            ?? JoinLocation(location.RoomName, location.ZoneName)
            ?? location.SiteName
            ?? location.LocationPath;
    }

    private static string? JoinLocation(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).ToList();
        return present.Count == 0 ? null : string.Join(" - ", present);
    }

    private static string? ImageOf(MobileAsset asset) =>
        asset.BasicInformation?.Images?.FirstOrDefault(image => !string.IsNullOrWhiteSpace(image));

    // ================================================================ Work orders

    private static WorkOrderClassification ClassifyWorkOrders(IEnumerable<MobileWorkOrder> workOrders, Window window, DateTime nowUtc)
    {
        var items = workOrders
            .Where(order => !IsCancelled(order.Status))
            .Select(order => (Order: order, Date: WorkOrderDate(order)))
            .Where(row => row.Date is not null && window.Contains(row.Date.Value))
            .Select(row => new ClassifiedWorkOrder(row.Order, row.Date!.Value, KindOf(row.Order, nowUtc)))
            .ToList();

        var planned = items.Count(item => item.Kind == WorkOrderKind.Planned);
        var unplanned = items.Count(item => item.Kind == WorkOrderKind.Unplanned);
        var overdue = items.Count(item => item.Kind == WorkOrderKind.Overdue);

        var breakdown = new List<MobileBreakdownItem>
        {
            new("planned", "Planned", planned, Share(planned, items.Count)),
            new("unplanned", "Unplanned", unplanned, Share(unplanned, items.Count)),
            new("overdue", "Overdue", overdue, Share(overdue, items.Count))
        };

        return new WorkOrderClassification(items, new MobileWorkOrderCounts(items.Count, planned, unplanned, overdue), breakdown, Share(planned, items.Count));
    }

    /// <summary>Overdue wins (open past its due date, or finished after it); otherwise corrective / breakdown work is unplanned.</summary>
    private static WorkOrderKind KindOf(MobileWorkOrder order, DateTime nowUtc)
    {
        var due = order.DueDate ?? order.PlannedEndDate;
        if (due is not null)
        {
            var closed = IsWorkOrderClosed(order.Status);
            if ((!closed && due.Value < nowUtc) || (closed && order.ActualEndDate is { } end && end > due.Value))
            {
                return WorkOrderKind.Overdue;
            }
        }

        return IsUnplanned(order) ? WorkOrderKind.Unplanned : WorkOrderKind.Planned;
    }

    private static bool IsUnplanned(MobileWorkOrder order)
    {
        return Is(order.WorkType, "Corrective", "Breakdown", "Emergency", "Reactive")
            || Is(order.Category, "Breakdown", "Emergency");
    }

    /// <summary>The date a work order counts against in charts: when it was scheduled, else due, else created.</summary>
    private static DateTime? WorkOrderDate(MobileWorkOrder order) =>
        order.ScheduledDate ?? order.PlannedStartDate ?? order.DueDate ?? order.CreatedAt;

    /// <summary>When an open work order is expected to start (calendar position).</summary>
    private static DateTime? WorkOrderStart(MobileWorkOrder order) =>
        order.PlannedStartDate ?? order.ScheduledDate ?? order.DueDate;

    /// <summary>Mean time to repair in hours, over finished unplanned work (all finished work when there is none).</summary>
    private static decimal? Mttr(IReadOnlyCollection<ClassifiedWorkOrder> items)
    {
        static List<double> Durations(IEnumerable<ClassifiedWorkOrder> source) => source
            .Where(item => item.Order.ActualStartDate is not null && item.Order.ActualEndDate is not null)
            .Select(item => (item.Order.ActualEndDate!.Value - item.Order.ActualStartDate!.Value).TotalHours)
            .Where(hours => hours >= 0)
            .ToList();

        var repairs = Durations(items.Where(item => IsUnplanned(item.Order)));
        if (repairs.Count == 0)
        {
            repairs = Durations(items);
        }

        return repairs.Count == 0 ? null : (decimal)repairs.Average();
    }

    /// <summary>
    /// Mean time between failures in days: the operating days of the assets that had work in the window divided by
    /// their unplanned work orders (failures). Null when nothing was worked on.
    /// </summary>
    private static decimal? Mtbf(IReadOnlyCollection<ClassifiedWorkOrder> items, Window window)
    {
        var assetCount = items
            .Select(item => item.Order.AssetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (assetCount == 0)
        {
            return null;
        }

        var failures = items.Count(item => IsUnplanned(item.Order));
        var operatingDays = (decimal)(window.ToUtc - window.FromUtc).TotalDays * assetCount;

        return failures == 0 ? operatingDays / assetCount : operatingDays / failures;
    }

    // ================================================================ Costs

    private static DateTime? CostDate(MobileWorkOrder order) =>
        order.ActualEndDate ?? order.ScheduledDate ?? order.DueDate ?? order.CreatedAt;

    private static decimal Labour(MobileWorkOrder order) =>
        (decimal)(order.Labour?.Sum(line => line.Hours * line.HourlyRate) ?? 0);

    private static decimal Parts(MobileWorkOrder order) =>
        (decimal)(order.Parts?.Sum(line => line.Quantity * line.UnitCost) ?? 0);

    private static (string Label, Window Window) CostWindow(DateTime nowUtc, string? period, int? year)
    {
        var key = Slug(period ?? "ytd");
        switch (key)
        {
            case "last-12-months":
            case "12m":
                return ("Last 12 Months", MonthWindow(nowUtc, 12));

            case "year":
                var fullYear = year ?? nowUtc.Year;
                var start = new DateTime(fullYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (fullYear.ToString(CultureInfo.InvariantCulture), new Window(start, start.AddYears(1)));

            default:
                var ytdYear = year ?? nowUtc.Year;
                var ytdStart = new DateTime(ytdYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var ytdEnd = ytdYear == nowUtc.Year ? nowUtc : ytdStart.AddYears(1);
                return ($"YTD {ytdYear}", new Window(ytdStart, ytdEnd));
        }
    }

    // ================================================================ Health

    private static List<AssetHealth> AssessHealth(IReadOnlyCollection<MobileAsset> assets, IReadOnlyDictionary<string, MobileAssetHealthScore> scores)
    {
        return assets
            .Where(asset => StatusGroups.Of(asset.Status) != StatusGroups.Retired)
            .Select(asset =>
            {
                scores.TryGetValue(asset.AssetRegistrationId, out var score);
                return new AssetHealth(asset, score?.HealthScore, score, BucketOf(asset, score));
            })
            .ToList();
    }

    /// <summary>
    /// The latest assessment score decides (≥70 healthy, ≥40 warning, else critical). Assets never assessed fall back
    /// to their recorded condition, then to their status (under maintenance → warning, out of service → critical).
    /// </summary>
    private static HealthBucket BucketOf(MobileAsset asset, MobileAssetHealthScore? score)
    {
        if (score is not null)
        {
            return score.HealthScore >= 70 ? HealthBucket.Healthy : score.HealthScore >= 40 ? HealthBucket.Warning : HealthBucket.Critical;
        }

        var condition = asset.BasicInformation?.Condition;
        if (Is(condition, "Excellent", "Very Good", "Good", "New"))
        {
            return HealthBucket.Healthy;
        }

        if (Is(condition, "Fair", "Average", "Moderate"))
        {
            return HealthBucket.Warning;
        }

        if (Is(condition, "Poor", "Very Poor", "Bad", "Critical", "Damaged", "Failed"))
        {
            return HealthBucket.Critical;
        }

        var group = StatusGroups.Of(asset.Status);
        return group == StatusGroups.UnderMaintenance ? HealthBucket.Warning
            : group == StatusGroups.OutOfService ? HealthBucket.Critical
            : HealthBucket.Healthy;
    }

    private static IReadOnlyList<MobileBreakdownItem> HealthBreakdown(IReadOnlyCollection<AssetHealth> health)
    {
        int Count(HealthBucket bucket) => health.Count(item => item.Bucket == bucket);

        return
        [
            new("healthy", "Healthy", Count(HealthBucket.Healthy), Share(Count(HealthBucket.Healthy), health.Count)),
            new("warning", "Warning", Count(HealthBucket.Warning), Share(Count(HealthBucket.Warning), health.Count)),
            new("critical", "Critical", Count(HealthBucket.Critical), Share(Count(HealthBucket.Critical), health.Count))
        ];
    }

    private static IReadOnlyList<MobileHealthGroup> HealthGroups(IReadOnlyCollection<AssetHealth> health, Func<AssetHealth, string?> keyOf)
    {
        return health
            .GroupBy(item => string.IsNullOrWhiteSpace(keyOf(item)) ? "Others" : keyOf(item)!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var healthy = group.Count(item => item.Bucket == HealthBucket.Healthy);
                return new MobileHealthGroup(
                    group.Key,
                    group.Count(),
                    healthy,
                    group.Count(item => item.Bucket == HealthBucket.Warning),
                    group.Count(item => item.Bucket == HealthBucket.Critical),
                    Share(healthy, group.Count()));
            })
            .OrderByDescending(group => group.Total)
            .ToList();
    }

    private static string IssueOf(AssetHealth item, IReadOnlyDictionary<string, MobileIoTAlert> latestAlert)
    {
        if (latestAlert.TryGetValue(item.Asset.AssetRegistrationId, out var alert))
        {
            var text = alert.EventType ?? alert.Message;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Assessment?.Remarks))
        {
            return item.Assessment.Remarks.Trim();
        }

        var condition = item.Assessment?.OverallCondition ?? item.Asset.BasicInformation?.Condition;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            return $"{condition.Trim()} condition";
        }

        return item.Asset.Status ?? "Low health score";
    }

    // ================================================================ Alert feed

    /// <summary>
    /// The Recent Alerts feed: IoT alerts raised in the last <paramref name="days"/> days plus the conditions that need
    /// attention now — overdue work orders, failed inspections, spare parts at or below minimum stock and preventive
    /// maintenance due within <see cref="MaintenanceDueSoonDays"/> days. Most recent first.
    /// </summary>
    private async Task<IReadOnlyList<FeedAlert>> BuildAlertFeedAsync(DateTime nowUtc, int days, string? site, CancellationToken cancellationToken)
    {
        var sinceUtc = nowUtc.AddDays(-days);

        var assetsTask = _repository.GetAssetsAsync(cancellationToken);
        var iotTask = _repository.GetAlertsAsync(sinceUtc, cancellationToken);
        var workOrdersTask = _repository.GetWorkOrdersAsync(nowUtc, cancellationToken);
        var inspectionsTask = _repository.GetInspectionsAsync(cancellationToken);
        var partsTask = _repository.GetLowStockSparePartsAsync(cancellationToken);
        var pmTask = _repository.GetPmSchedulesDueAsync(nowUtc, nowUtc.AddDays(MaintenanceDueSoonDays), cancellationToken);
        await Task.WhenAll(assetsTask, iotTask, workOrdersTask, inspectionsTask, partsTask, pmTask);

        var index = new AssetIndex(assetsTask.Result);
        var scope = new SiteScope(site, index);
        var feed = new List<FeedAlert>();

        foreach (var alert in iotTask.Result.Where(scope.Includes))
        {
            var asset = index.Find(alert.AssetRegistrationId);
            feed.Add(new FeedAlert(
                Text(alert.Id) ?? alert.AlertId, "IoT",
                alert.EventType ?? alert.Message ?? "Alert",
                JoinLocation(asset?.AssetTag ?? alert.AssetRegistrationId, alert.AssetName ?? asset?.AssetName),
                asset?.AssetTag ?? alert.AssetRegistrationId, alert.AssetName ?? asset?.AssetName,
                NormalizeSeverity(alert.Severity), alert.Status, alert.RaisedAt));
        }

        foreach (var order in workOrdersTask.Result.Where(order => scope.Includes(order) && !IsWorkOrderClosed(order.Status) && order.DueDate is { } due && due < nowUtc))
        {
            var asset = index.Find(order.AssetId);
            feed.Add(new FeedAlert(
                Text(order.Id) ?? order.WorkOrderId, "WorkOrder", "Work Order Overdue", order.WorkOrderId,
                asset?.AssetTag ?? order.AssetId, order.AssetName ?? asset?.AssetName,
                Is(order.Priority, "Critical", "Emergency", "Urgent") ? Severity.Critical : Severity.Warning,
                order.Status, order.DueDate!.Value));
        }

        foreach (var inspection in inspectionsTask.Result.Where(scope.Includes))
        {
            var completed = ParseDate(inspection.CompletedAt);
            var failed = Is(inspection.Status, "Failed", "Fail")
                || inspection.Responses?.Any(response => Is(response.Result, "Fail", "Failed")) == true;
            var when = completed ?? inspection.CreatedAt;

            if (!failed || when is null || when.Value < sinceUtc)
            {
                continue;
            }

            var asset = index.Find(inspection.AssetCode);
            feed.Add(new FeedAlert(
                Text(inspection.Id) ?? inspection.InspectionCode, "Inspection", "Inspection Failed",
                JoinLocation(inspection.InspectionCode, inspection.Title),
                asset?.AssetTag ?? inspection.AssetCode, inspection.AssetName ?? asset?.AssetName,
                Severity.Critical, inspection.Status, when.Value));
        }

        foreach (var part in scope.Filter(partsTask.Result))
        {
            feed.Add(new FeedAlert(
                Text(part.Id) ?? part.PartId, "SparePart", "Spare Parts Low Stock",
                JoinLocation(part.PartName, part.PartId), null, null,
                part.QuantityInStock <= 0 ? Severity.Critical : Severity.Warning,
                $"{part.QuantityInStock} in stock (min {part.MinimumStockLevel})",
                part.UpdatedAt ?? part.CreatedAt ?? nowUtc));
        }

        foreach (var schedule in pmTask.Result.Where(scope.Includes))
        {
            var asset = index.Find(schedule.AssetId);
            feed.Add(new FeedAlert(
                Text(schedule.Id) ?? schedule.PmScheduleId, "Maintenance", "Maintenance Due Soon",
                JoinLocation(asset?.AssetTag ?? schedule.AssetId, schedule.AssetName ?? asset?.AssetName),
                asset?.AssetTag ?? schedule.AssetId, schedule.AssetName ?? asset?.AssetName,
                Severity.Warning, $"Due {schedule.NextDueDate:yyyy-MM-dd}",
                // Dated from when it entered the due-soon window, so it does not always sit above newer alerts.
                Min(schedule.NextDueDate!.Value.AddDays(-MaintenanceDueSoonDays), nowUtc)));
        }

        return feed.OrderByDescending(alert => alert.OccurredAtUtc).ToList();
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (Is(severity, "Critical", "High", "Emergency", "Major"))
        {
            return Severity.Critical;
        }

        return Is(severity, "Warning", "Medium", "Minor") ? Severity.Warning : Severity.Info;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static bool IsAlertResolved(string? status) => Is(status, "Resolved", "Closed", "Dismissed");

    // ================================================================ Status helpers

    private static bool IsWorkOrderClosed(string? status) => Is(status, MobileDashboardRepository.ClosedWorkOrderStatuses);

    private static bool IsCancelled(string? status) => Is(status, "Cancelled", "Canceled");

    private static bool IsInspectionClosed(string? status) => Is(status, "Completed", "Closed", "Cancelled", "Canceled", "Approved", "Rejected");

    private static DateTime? InspectionStart(MobileInspection inspection) =>
        ParseDate(inspection.ScheduledStart) ?? ParseDate(inspection.DueDate);

    private static bool IncludesType(string? type, string wanted) =>
        string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase)
        || type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => Slug(part).Replace("-", string.Empty).Equals(wanted, StringComparison.OrdinalIgnoreCase));

    private static bool Is(string? value, params string[] options) =>
        value is not null && options.Any(option => option.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string? value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(value?.Trim(), filter.Trim(), StringComparison.OrdinalIgnoreCase);

    // ================================================================ Maths / dates

    private static decimal Share(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);

    private static decimal Round(decimal value) => Math.Round(value, 2);

    private static decimal? Change(decimal? current, decimal? previous) =>
        current is null || previous is null || previous == 0 ? null : Math.Round((current.Value - previous.Value) * 100m / previous.Value, 1);

    private static bool? Improved(decimal? current, decimal? previous, bool lowerIsBetter) =>
        current is null || previous is null || current == previous ? null : lowerIsBetter ? current < previous : current > previous;

    /// <summary>The last <paramref name="months"/> calendar months, the current one included (to now).</summary>
    private static Window MonthWindow(DateTime nowUtc, int months)
    {
        var start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        return new Window(start, nowUtc);
    }

    private static IEnumerable<DateTime> MonthsOf(Window window)
    {
        for (var month = new DateTime(window.FromUtc.Year, window.FromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
             month < window.ToUtc;
             month = month.AddMonths(1))
        {
            yield return month;
        }
    }

    private static DateTime? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static string? Text(object? id) => id?.ToString() is { Length: > 0 } text ? text : null;

    private static string Slug(string value) =>
        string.Join('-', value.Trim().ToLowerInvariant().Split(new[] { ' ', '_', '/' }, StringSplitOptions.RemoveEmptyEntries));

    // ================================================================ Private types

    private sealed record Window(DateTime FromUtc, DateTime ToUtc)
    {
        public bool Contains(DateTime value) => value >= FromUtc && value < ToUtc;
    }

    private enum WorkOrderKind { Planned, Unplanned, Overdue }

    private sealed record ClassifiedWorkOrder(MobileWorkOrder Order, DateTime Date, WorkOrderKind Kind);

    private sealed record WorkOrderClassification(
        IReadOnlyList<ClassifiedWorkOrder> Items,
        MobileWorkOrderCounts Counts,
        IReadOnlyList<MobileBreakdownItem> Breakdown,
        decimal PlannedPercent);

    /// <summary>Maintenance = labour, Operations = additional costs (contractor, rental, freight), Spare Parts = parts.</summary>
    private sealed record WorkOrderCost(MobileWorkOrder Order, DateTime? Date, decimal Maintenance, decimal Operations, decimal SpareParts)
    {
        public decimal Total => Maintenance + Operations + SpareParts;
    }

    private enum HealthBucket { Healthy, Warning, Critical }

    private sealed record AssetHealth(MobileAsset Asset, decimal? Score, MobileAssetHealthScore? Assessment, HealthBucket Bucket);

    private static class Severity
    {
        public const string Critical = "Critical";
        public const string Warning = "Warning";
        public const string Info = "Info";
    }

    private sealed record FeedAlert(
        string Id, string Source, string Title, string? Reference, string? AssetId, string? AssetName,
        string Severity, string? Status, DateTime OccurredAtUtc)
    {
        public MobileAlertItem ToResponse() =>
            new(Id, Source, Title, Reference, AssetId, AssetName, Severity, Status, DateTime.SpecifyKind(OccurredAtUtc, DateTimeKind.Utc));
    }

    /// <summary>
    /// The optional <c>site</c> filter (site name or site id, ignoring case). Records are tied to a site through their
    /// own site field or their asset's site. Approvals and spare parts have no site, so they are left out when a site is set.
    /// </summary>
    private sealed class SiteScope
    {
        private readonly string? _site;
        private readonly AssetIndex _index;

        public SiteScope(string? site, AssetIndex index)
        {
            _site = string.IsNullOrWhiteSpace(site) ? null : site.Trim();
            _index = index;
        }

        public IReadOnlyList<TRecord> Filter<TRecord>(IReadOnlyList<TRecord> siteless) => _site is null ? siteless : [];

        public bool Includes(MobileAsset asset) => Matches(_site, asset.Location?.SiteName, asset.Location?.SiteId);

        public bool Includes(MobileWorkOrder order) => Matches(_site, [order.Site, .. SitesOf(order.AssetId)]);

        public bool Includes(MobileInspection inspection) => Matches(_site, [inspection.SiteName, .. SitesOf(inspection.AssetCode)]);

        public bool Includes(MobileIoTAlert alert) => Matches(_site, [alert.SiteName, alert.SiteId, .. SitesOf(alert.AssetRegistrationId)]);

        public bool Includes(MobilePmSchedule schedule) => Matches(_site, [schedule.Location, .. SitesOf(schedule.AssetId)]);

        public static bool Matches(string? site, params string?[] candidates) =>
            string.IsNullOrWhiteSpace(site)
            || candidates.Any(candidate => string.Equals(candidate?.Trim(), site.Trim(), StringComparison.OrdinalIgnoreCase));

        private string?[] SitesOf(string? assetKey) =>
            _index.Find(assetKey)?.Location is { } location ? [location.SiteName, location.SiteId] : [];
    }

    /// <summary>Finds an asset by registration id or tag (work orders and inspections use either), ignoring case.</summary>
    private sealed class AssetIndex
    {
        private readonly Dictionary<string, MobileAsset> _byKey = new(StringComparer.OrdinalIgnoreCase);

        public AssetIndex(IEnumerable<MobileAsset> assets)
        {
            foreach (var asset in assets)
            {
                if (!string.IsNullOrWhiteSpace(asset.AssetRegistrationId))
                {
                    _byKey.TryAdd(asset.AssetRegistrationId.Trim(), asset);
                }

                if (!string.IsNullOrWhiteSpace(asset.AssetTag))
                {
                    _byKey.TryAdd(asset.AssetTag.Trim(), asset);
                }
            }
        }

        public MobileAsset? Find(string? key) =>
            string.IsNullOrWhiteSpace(key) ? null : _byKey.GetValueOrDefault(key.Trim());

        public string? TypeOf(string? key) => Find(key)?.AssetType is { Length: > 0 } type ? type.Trim() : null;
    }
}

/// <summary>The four asset status groups the mobile app shows, mapped from the registration statuses.</summary>
public sealed record StatusGroup(string Key, string Label, string[] Statuses);

public static class StatusGroups
{
    public static readonly StatusGroup Operational = new("operational", "Operational", ["Active", "Operational", "In Use", "Installed", "Commissioned"]);
    public static readonly StatusGroup UnderMaintenance = new("under-maintenance", "Under Maintenance", ["Under Maintenance", "In Repair", "Maintenance"]);
    public static readonly StatusGroup OutOfService = new("out-of-service", "Out of Service", ["Out of Service", "Inactive", "In Storage", "Idle", "Faulty"]);
    public static readonly StatusGroup Retired = new("retired", "Retired", ["Retired", "Disposed", "Scrapped"]);
    public static readonly StatusGroup Other = new("other", "Other", []);

    public static readonly IReadOnlyList<StatusGroup> All = [Operational, UnderMaintenance, OutOfService, Retired, Other];

    public static StatusGroup Of(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Other;
        }

        var trimmed = status.Trim();
        return All.FirstOrDefault(group => group.Statuses.Any(value => value.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) ?? Other;
    }
}

/// <summary>Request time and the caller's UTC offset, so "today" and the greeting follow the phone's local time.</summary>
public sealed record MobileClock(DateTime UtcNow, TimeSpan Offset)
{
    public DateTime LocalNow => UtcNow + Offset;

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + Offset);

    public DateTime ToUtc(DateOnly localDate) =>
        DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);
}
