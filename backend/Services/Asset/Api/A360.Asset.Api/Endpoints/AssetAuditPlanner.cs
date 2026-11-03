using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Endpoints;

/// <summary>Builds the Audit Plan from the assets, the team and the schedule.</summary>
/// <remarks>
/// Assets are grouped by the schedule's Location Sequence (by location, by category, or in the order picked)
/// and spread evenly over the working days. Each group goes to one auditor, taking Lead Auditors and Auditors
/// in turn, so a location is visited by the same person; a group too big for one day carries on the next day.
/// </remarks>
internal static class AssetAuditPlanner
{
    private const string NoLocation = "No location";
    private const string NoCategory = "No category";

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>The working dates from StartDate to EndDate (both included); empty when the dates are missing.</summary>
    public static List<DateTime> WorkingDates(AssetAuditSchedule schedule)
    {
        if (schedule.StartDate is not { } start || schedule.EndDate is not { } end || end < start)
        {
            return [];
        }

        var days = schedule.WorkingDays switch
        {
            AssetAuditOptions.AllDays => Enum.GetValues<DayOfWeek>(),
            AssetAuditOptions.CustomDays => schedule.CustomDays
                .Select(day => Enum.TryParse<DayOfWeek>(day, ignoreCase: true, out var parsed) ? parsed : (DayOfWeek?)null)
                .OfType<DayOfWeek>()
                .ToArray(),
            _ => Weekdays
        };

        var dates = new List<DateTime>();
        for (var date = start.Date; date <= end.Date && dates.Count <= AssetAuditValidationExtensions.MaxScheduleDays; date = date.AddDays(1))
        {
            if (days.Contains(date.DayOfWeek))
            {
                dates.Add(DateTime.SpecifyKind(date, DateTimeKind.Utc));
            }
        }

        return dates;
    }

    /// <summary>Empty when there are no assets, no working days or nobody to assign.</summary>
    public static List<AssetAuditPlanEntry> Generate(
        IReadOnlyList<AssetAuditAsset> assets,
        IReadOnlyCollection<AssetAuditTeamMember> team,
        AssetAuditSchedule schedule)
    {
        var dates = WorkingDates(schedule);
        var auditors = team.Where(member => AssetAuditOptions.PlannedRoles.IsValid(member.Role)).ToList();
        if (auditors.Count == 0)
        {
            auditors = team.ToList();
        }

        if (assets.Count == 0 || dates.Count == 0 || auditors.Count == 0)
        {
            return [];
        }

        var perDay = (int)Math.Ceiling(assets.Count / (double)dates.Count);
        var plan = new List<AssetAuditPlanEntry>();
        var dayIndex = 0;
        var usedToday = 0;
        var groupIndex = 0;

        foreach (var (label, groupAssets) in Groups(assets, schedule.LocationSequence))
        {
            var auditor = auditors[groupIndex++ % auditors.Count];
            var remaining = new Queue<AssetAuditAsset>(groupAssets);

            while (remaining.Count > 0)
            {
                if (usedToday >= perDay && dayIndex < dates.Count - 1)
                {
                    dayIndex++;
                    usedToday = 0;
                }

                // The last day takes whatever is left.
                var take = dayIndex == dates.Count - 1 ? remaining.Count : Math.Min(perDay - usedToday, remaining.Count);
                var ids = Enumerable.Range(0, take).Select(_ => remaining.Dequeue().AssetRegistrationId).ToList();

                plan.Add(new AssetAuditPlanEntry
                {
                    Date = dates[dayIndex],
                    Location = label,
                    AssetRegistrationIds = ids,
                    AssignedToUserId = auditor.UserId,
                    AssignedToName = auditor.Name,
                    Status = AssetAuditRepository.ScheduledStatus
                });

                usedToday += take;
            }
        }

        return plan;
    }

    /// <summary>Sets each member's Assigned Assets from the plan.</summary>
    public static void CountAssignedAssets(IEnumerable<AssetAuditTeamMember> team, IReadOnlyCollection<AssetAuditPlanEntry> plan)
    {
        foreach (var member in team)
        {
            member.AssignedAssets = plan
                .Where(entry => entry.AssignedToUserId == member.UserId)
                .Sum(entry => entry.AssetRegistrationIds.Count);
        }
    }

    private static IEnumerable<(string Label, List<AssetAuditAsset> Assets)> Groups(IReadOnlyList<AssetAuditAsset> assets, string? sequence)
    {
        switch (sequence)
        {
            case AssetAuditOptions.ByAssetCategory:
                return assets
                    .GroupBy(asset => asset.Category ?? NoCategory, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => (group.Key, group.ToList()));

            case AssetAuditOptions.CustomOrder:
                // Keeps the picked order; consecutive assets at the same location stay together.
                var groups = new List<(string Label, List<AssetAuditAsset> Assets)>();
                foreach (var asset in assets)
                {
                    var label = asset.Location ?? NoLocation;
                    if (groups.Count > 0 && string.Equals(groups[^1].Label, label, StringComparison.OrdinalIgnoreCase))
                    {
                        groups[^1].Assets.Add(asset);
                    }
                    else
                    {
                        groups.Add((label, [asset]));
                    }
                }

                return groups;

            default:
                return assets
                    .GroupBy(asset => asset.Location ?? NoLocation, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => (group.Key, group.ToList()));
        }
    }
}
