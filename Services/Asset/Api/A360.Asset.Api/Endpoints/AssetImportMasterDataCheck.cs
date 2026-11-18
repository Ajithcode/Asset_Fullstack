using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Bulk import, Validate step: checks the values of a row against the master lists (categories, asset types, criticality,
/// condition, departments, business units, sites, buildings, floors, zones and employees), the way the registration
/// screens only offer listed values.
/// </summary>
/// <remarks>
/// Only the fields the row gives are checked, so an update does not fail on an older value it leaves alone.
/// A list that has not been set up (no records) is not checked. Matched values are rewritten with the listed spelling,
/// and location levels and the assignee get their master record ids, as if picked on screen.
/// </remarks>
internal sealed class AssetImportMasterDataCheck(AssetImportMasterData data, IReadOnlyCollection<AssignmentEmployeeLookup> employees)
{
    private const int SuggestionCount = 10;

    /// <param name="FieldKey">The import field the error is about, e.g. "subCategory".</param>
    public delegate void Report(string FieldKey, string Message);

    /// <summary>Reports the error with the listed values to use, e.g. "Site 'X' is not configured. Use one of: A, B".</summary>
    private static void Error(Report report, string key, string message, string? suggestion) =>
        report(key, suggestion is null ? message : $"{message}. {suggestion}");

    private static readonly string[] LocationKeys = ["site", "building", "floor", "zone"];

    public void Check(AssetRegistration asset, IReadOnlySet<string> provided, Report report)
    {
        CheckClassification(asset.Classification, provided, report);

        Simple("criticality", "Criticality", data.Criticalities, asset.Classification.Criticality, provided, report,
            name => { asset.Classification.Criticality = name; asset.BasicInformation.Criticality = name; });
        Simple("condition", "Condition", data.Conditions, asset.BasicInformation.Condition, provided, report,
            name => asset.BasicInformation.Condition = name);
        Simple("department", "Department", data.Departments, asset.Classification.Department, provided, report,
            name => asset.Classification.Department = name);
        Simple("custodianDepartment", "Custodian Department", data.Departments, asset.LocationAssignment.Assignment.CustodianDepartment, provided, report,
            name => asset.LocationAssignment.Assignment.CustodianDepartment = name);
        Simple("businessUnit", "Business Unit", data.BusinessUnits, asset.Classification.BusinessUnit, provided, report,
            name => asset.Classification.BusinessUnit = name);

        if (LocationKeys.Any(provided.Contains))
        {
            CheckLocation(asset.LocationAssignment.Location, provided, report);
        }

        if (provided.Contains("assignedTo"))
        {
            CheckAssignee(asset.LocationAssignment.Assignment);
        }
    }

    private void CheckClassification(AssetClassification classification, IReadOnlySet<string> provided, Report report)
    {
        var touched = provided.Contains("category") || provided.Contains("subCategory") || provided.Contains("assetType");
        if (!touched)
        {
            return;
        }

        var topLevel = data.Categories.Where(item => item.ParentId is null).ToList();
        var subLevel = data.Categories.Where(item => item.ParentId is not null).ToList();

        AssetImportMasterItem? category = null;
        if (data.Categories.Count > 0 && classification.Category is { } categoryText)
        {
            category = Match(topLevel, categoryText);
            if (category is null)
            {
                if (provided.Contains("category"))
                {
                    var asSub = Match(subLevel, categoryText);
                    Error(report, "category",
                        asSub is null
                            ? $"Category '{categoryText}' is not configured"
                            : $"'{asSub.Name}' is a sub category of '{asSub.ParentName}'; put it in Sub Category and '{asSub.ParentName}' in Category",
                        Suggest(topLevel));
                }
            }
            else if (Inactive(category, "category", "Category", provided, report))
            {
                category = null;
            }
            else
            {
                classification.Category = category.Name;
            }
        }

        AssetImportMasterItem? subCategory = null;
        if (subLevel.Count > 0 && classification.SubCategory is { } subText && (provided.Contains("subCategory") || provided.Contains("category")))
        {
            var candidates = category is null ? subLevel : subLevel.Where(item => item.ParentId == category.Id).ToList();
            subCategory = Match(candidates, subText);
            if (subCategory is null)
            {
                var elsewhere = Match(subLevel, subText);
                Error(report, "subCategory",
                    elsewhere is not null
                        ? $"Sub Category '{elsewhere.Name}' belongs to category '{elsewhere.ParentName}', not '{classification.Category}'"
                        : category is null
                            ? $"Sub Category '{subText}' is not configured"
                            : $"Sub Category '{subText}' is not configured under '{category.Name}'",
                    Suggest(candidates));
            }
            else if (Inactive(subCategory, "subCategory", "Sub Category", provided, report, force: true))
            {
                subCategory = null;
            }
            else
            {
                classification.SubCategory = subCategory.Name;
            }
        }

        if (data.AssetTypes.Count > 0 && classification.AssetType is { } typeText)
        {
            // An asset type is set up under a category or a sub category; it fits the row when it is in the row's category tree
            // and, if the row has a sub category, under that sub category or directly under the category.
            var rowTop = category ?? (subCategory?.ParentId is { } parentId ? ById(parentId) : null);
            bool Fits(AssetImportMasterItem item)
            {
                var typeCategory = item.ParentId is null ? null : ById(item.ParentId);
                if (typeCategory is null || rowTop is null)
                {
                    return true;
                }

                var typeTop = typeCategory.ParentId is null ? typeCategory : ById(typeCategory.ParentId) ?? typeCategory;
                return typeTop.Id == rowTop.Id && (subCategory is null || typeCategory.ParentId is null || typeCategory.Id == subCategory.Id);
            }

            var type = Match(data.AssetTypes, typeText);
            if (type is null)
            {
                Error(report, "assetType", $"Asset Type '{typeText}' is not configured",
                    Suggest(data.AssetTypes.Where(Fits).ToList()));
            }
            else if (!Fits(type))
            {
                Error(report, "assetType",
                    $"Asset Type '{type.Name}' belongs to category '{type.ParentName}', not '{subCategory?.Name ?? rowTop?.Name}'",
                    Suggest(data.AssetTypes.Where(Fits).ToList()));
            }
            else if (!Inactive(type, "assetType", "Asset Type", provided, report, force: true))
            {
                classification.AssetType = type.Name;
            }
        }
    }

    private AssetImportMasterItem? ById(string id) => data.Categories.FirstOrDefault(item => item.Id == id);

    /// <summary>Site, then building in that site, floor in that building and zone on that floor; each level gets its master id.</summary>
    private void CheckLocation(AssetLocation location, IReadOnlySet<string> provided, Report report)
    {
        AssetImportMasterItem? site = null;
        if (data.Sites.Count > 0 && location.SiteName is { } siteText)
        {
            site = Match(data.Sites, siteText);
            if (site is null)
            {
                Error(report, "site", $"Site '{siteText}' is not configured", Suggest(data.Sites));
            }
            else if (!Inactive(site, "site", "Site", provided, report, force: true))
            {
                location.SiteName = site.Name;
                location.SiteId = site.Id;

                // A blank country is taken from the site.
                if (site.Country is not null && location.CountryName is null)
                {
                    location.CountryName = site.Country;
                    location.CountryId ??= site.Country;
                }
            }
        }

        var building = Level("building", "Building", data.Buildings, site, location.BuildingName, report,
            (id, name) => { location.BuildingId = id; location.BuildingName = name; });
        var floor = Level("floor", "Floor", data.Floors, building, location.FloorName, report,
            (id, name) => { location.FloorId = id; location.FloorName = name; });
        Level("zone", "Zone", data.Zones, floor, location.ZoneName, report,
            (id, name) => { location.ZoneId = id; location.ZoneName = name; });
    }

    private static AssetImportMasterItem? Level(
        string key,
        string label,
        IReadOnlyList<AssetImportMasterItem> list,
        AssetImportMasterItem? parent,
        string? text,
        Report report,
        Action<string, string> set)
    {
        if (list.Count == 0 || text is null)
        {
            return null;
        }

        var candidates = parent is null ? list : list.Where(item => item.ParentId == parent.Id).ToList();
        var match = Match(candidates, text);
        if (match is null)
        {
            var elsewhere = parent is null ? null : Match(list, text);
            Error(report, key,
                elsewhere is not null
                    ? $"{label} '{elsewhere.Name}' is not in '{parent!.Name}'"
                    : parent is null
                        ? $"{label} '{text}' is not configured"
                        : $"{label} '{text}' is not configured in '{parent.Name}'",
                Suggest(candidates));
            return null;
        }

        if (match.Inactive)
        {
            Error(report, key, $"{label} '{match.Name}' is inactive", Suggest(candidates));
            return null;
        }

        set(match.Id, match.Name);
        return match;
    }

    /// <summary>An assignee found in the employee list (by name or employee ID) gets the employee's id; otherwise the name is kept as typed.</summary>
    private void CheckAssignee(AssetAssignment assignment)
    {
        if (assignment.AssignedToName is not { } name)
        {
            return;
        }

        var matches = employees
            .Where(employee => Same(employee.EmployeeName, name) || Same(employee.EmployeeCode, name))
            .ToList();
        if (matches.Count == 1)
        {
            assignment.AssignedToName = matches[0].EmployeeName ?? name;
            assignment.AssignedToUserId = matches[0].Id;
        }
    }

    private static void Simple(
        string key,
        string label,
        IReadOnlyList<AssetImportMasterItem> list,
        string? text,
        IReadOnlySet<string> provided,
        Report report,
        Action<string> set)
    {
        if (list.Count == 0 || text is null || !provided.Contains(key))
        {
            return;
        }

        var match = Match(list, text);
        if (match is null)
        {
            Error(report, key, $"{label} '{text}' is not configured", Suggest(list));
        }
        else if (match.Inactive)
        {
            Error(report, key, $"{label} '{match.Name}' is inactive", Suggest(list));
        }
        else
        {
            set(match.Name);
        }
    }

    private static bool Inactive(AssetImportMasterItem item, string key, string label, IReadOnlySet<string> provided, Report report, bool force = false)
    {
        if (!item.Inactive)
        {
            return false;
        }

        if (force || provided.Contains(key))
        {
            Error(report, key, $"{label} '{item.Name}' is inactive", null);
        }

        return true;
    }

    /// <summary>By name first, then by code, ignoring case.</summary>
    private static AssetImportMasterItem? Match(IReadOnlyList<AssetImportMasterItem> list, string text) =>
        list.FirstOrDefault(item => Same(item.Name, text)) ?? list.FirstOrDefault(item => Same(item.Code, text));

    private static string? Suggest(IReadOnlyList<AssetImportMasterItem> list)
    {
        var names = list.Where(item => !item.Inactive).Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
        if (names.Count == 0)
        {
            return null;
        }

        var shown = string.Join(", ", names.Take(SuggestionCount));
        return names.Count > SuggestionCount ? $"Use one of: {shown}, …" : $"Use one of: {shown}";
    }

    private static bool Same(string? left, string? right) =>
        left is not null && right is not null && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
