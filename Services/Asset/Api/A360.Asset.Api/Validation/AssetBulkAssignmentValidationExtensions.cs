using System.Globalization;
using A360.Asset.Api.Contracts;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Validation;

public static class AssetBulkAssignmentValidationExtensions
{
    private const int TextMaxLength = 200;
    private const int RemarksMaxLength = 500;

    /// <summary>Step 4: the fields marked * on screen, plus allowed options, lengths and date order.</summary>
    public static Dictionary<string, string[]> Validate(this SaveAssetBulkAssignmentDetailsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var assignTo = AssetAssignmentTargets.Normalize(request.AssignTo);
        if (string.IsNullOrWhiteSpace(request.AssignTo))
        {
            errors["AssignTo"] = ["AssignTo is required"];
        }
        else if (assignTo is null)
        {
            errors["AssignTo"] = [$"AssignTo must be one of: {string.Join(", ", AssetAssignmentTargets.All)}"];
        }

        switch (assignTo)
        {
            case AssetAssignmentTargets.Department:
                Required(errors, "Department.DepartmentId", request.Department?.DepartmentId);
                break;

            case AssetAssignmentTargets.Location:
                if (string.IsNullOrWhiteSpace(request.Location?.SiteId))
                {
                    errors["Location.SiteId"] = ["Select a location / site"];
                }

                break;

            case AssetAssignmentTargets.OtherEntity:
                Required(errors, "OtherEntity.EntityName", request.OtherEntity?.EntityName);
                if (!string.IsNullOrWhiteSpace(request.OtherEntity?.EntityType) && !AssetAssignmentOtherEntityTypes.IsValid(request.OtherEntity.EntityType))
                {
                    errors["OtherEntity.EntityType"] = [$"EntityType must be one of: {string.Join(", ", AssetAssignmentOtherEntityTypes.All)}"];
                }

                break;
        }

        if (request.StartDate is null)
        {
            errors["StartDate"] = ["StartDate is required"];
        }

        if (request.IsOpenEnded && request.EndDate is not null)
        {
            errors["EndDate"] = ["Leave EndDate empty when Open Ended is ticked"];
        }
        else if (request.StartDate is not null && request.EndDate is not null && request.EndDate.Value.Date < request.StartDate.Value.Date)
        {
            errors["EndDate"] = ["EndDate cannot be before StartDate"];
        }

        Required(errors, "AssignmentReason", request.AssignmentReason);
        MaxLength(errors, "AssignmentReason", request.AssignmentReason, TextMaxLength);
        MaxLength(errors, "Reference", request.Reference, TextMaxLength);
        MaxLength(errors, "Notes", request.Notes, RemarksMaxLength);
        MaxLength(errors, "OtherEntity.EntityName", request.OtherEntity?.EntityName, TextMaxLength);

        return errors;
    }

    private static void Required(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field.Split('.').Last()} is required"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field.Split('.').Last()} cannot be longer than {maxLength} characters"];
        }
    }
}

/// <summary>The assets and active assignments loaded while checking, so confirming does not load them again.</summary>
/// <param name="Assets">By AssetRegistrationId.</param>
internal sealed record AssetBulkAssignmentCheck(
    IReadOnlyDictionary<string, AssetRegistration> Assets,
    IReadOnlyCollection<AssetAssignmentRecord> ActiveAssignments);

/// <summary>
/// Checks every row of a bulk assignment against the system (the Validation Rules panel) and fills in
/// each row's Status, Issues and Resolved values. Blank values are taken from the common details (step 4).
/// </summary>
/// <remarks>
/// Errors stop a row from being assigned: asset not found, duplicate asset, invalid assignment type,
/// receiver / department / location not found, missing or invalid dates, end before start.
/// Warnings do not: inactive employee or department, dates in the past, name or type differing from the
/// registered asset, location levels kept as text, and asset already assigned (skipped on confirm unless
/// Override existing assignment is ticked).
/// </remarks>
internal static class AssetBulkAssignmentRowChecker
{
    private const int TextMaxLength = 200;
    private const int RemarksMaxLength = 500;
    private const string ActiveStatus = "Active";

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "MM/dd/yyyy",
        "dd MMM yyyy", "d MMM yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMMM yyyy", "d MMMM yyyy"
    ];

    // Splits "Head Office - Floor 2" or "Head Office > Block A > Floor 2" into levels.
    private static readonly string[] LocationSeparators = [" - ", ">", " / ", ","];

    public static async Task<AssetBulkAssignmentCheck> CheckAsync(
        AssetBulkAssignment batch,
        IAssetRegistrationRepository assetRepository,
        IAssetAssignmentRepository assignmentRepository,
        IAssignmentMasterDataRepository masterDataRepository,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var common = batch.CommonDetails;
        var rows = batch.Rows;

        var assets = await assetRepository.FindByTagsOrSerialsAsync(Distinct(rows.Select(row => row.Values.AssetTag)), cancellationToken);
        var assetsByTag = IndexBy(assets, asset => asset.BasicInformation.AssetTag);
        var assetsBySerial = IndexBy(assets, asset => asset.BasicInformation.SerialNumber);

        var activeAssignments = await assignmentRepository.GetActiveByAssetIdsAsync(
            assets.Select(asset => asset.AssetRegistrationId).ToList(),
            today,
            cancellationToken);
        var currentAssignments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assignment in activeAssignments)
        {
            foreach (var asset in assignment.Assets)
            {
                currentAssignments.TryAdd(asset.AssetRegistrationId, assignment.AssignmentNumber);
            }
        }

        // Every name the rows refer to is looked up once.
        var targets = rows.ToDictionary(row => row, row => TargetOf(row.Values, common));
        var employeeTexts = rows
            .Where(row => targets[row] == AssetAssignmentTargets.Employee)
            .Select(row => row.Values.AssignTo);
        var departmentTexts = rows
            .Select(row => row.Values.Department)
            .Concat(rows.Where(row => targets[row] == AssetAssignmentTargets.Department).Select(row => row.Values.AssignTo));
        var placeTexts = rows
            .Select(row => row.Values.Location)
            .Concat(rows.Where(row => targets[row] == AssetAssignmentTargets.Location).Select(row => row.Values.AssignTo))
            .Where(text => text is not null)
            .SelectMany(text => SplitLocation(text!).Append(text!));

        var places = Distinct(placeTexts);
        var lookups = new Lookups(
            await masterDataRepository.FindEmployeesAsync(Distinct(employeeTexts), cancellationToken),
            await masterDataRepository.FindDepartmentsAsync(Distinct(departmentTexts), cancellationToken),
            await masterDataRepository.FindSitesAsync(places, cancellationToken),
            await masterDataRepository.FindBuildingsAsync(places, cancellationToken),
            await masterDataRepository.FindFloorsAsync(places, cancellationToken),
            await masterDataRepository.FindLocationsAsync(places, cancellationToken));

        var firstRowOfAsset = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows.OrderBy(row => row.RowNumber))
        {
            var check = new RowCheck(row);
            var values = row.Values;
            var resolved = check.Resolved;

            CheckAsset(check, values, assetsByTag, assetsBySerial, currentAssignments, firstRowOfAsset, common);

            var target = targets[row];
            if (values.AssignmentType is not null && target is null)
            {
                check.Error(AssetBulkAssignmentFields.AssignmentType, "Invalid assignment type",
                    $"'{values.AssignmentType}' is not one of: {string.Join(", ", AssetAssignmentTargets.All)}.");
            }
            else if (target is null)
            {
                check.Error(AssetBulkAssignmentFields.AssignmentType, "Assignment Type is required",
                    "Enter it in the file or choose it in Assignment Details.");
            }

            resolved.AssignTo = target;

            switch (target)
            {
                case AssetAssignmentTargets.Employee:
                    CheckEmployee(check, values, common, lookups);
                    break;
                case AssetAssignmentTargets.Department:
                    CheckDepartmentTarget(check, values, common, lookups);
                    break;
                case AssetAssignmentTargets.Location:
                    CheckLocationTarget(check, values, common, lookups);
                    break;
                case AssetAssignmentTargets.OtherEntity:
                    CheckOtherEntity(check, values, common, lookups);
                    break;
            }

            CheckDates(check, values, common, today);

            resolved.Purpose = values.Purpose;
            resolved.CostCenter = values.CostCenter;
            resolved.Notes = values.Notes ?? common?.Notes;

            MaxLength(check, AssetBulkAssignmentFields.AssignTo, values.AssignTo, TextMaxLength);
            MaxLength(check, AssetBulkAssignmentFields.Purpose, values.Purpose, RemarksMaxLength);
            MaxLength(check, AssetBulkAssignmentFields.CostCenter, values.CostCenter, TextMaxLength);
            MaxLength(check, AssetBulkAssignmentFields.Notes, values.Notes, RemarksMaxLength);

            row.Issues = check.Issues;
            row.Resolved = resolved;
            row.Status = check.Issues.Any(issue => issue.Severity == AssetBulkAssignmentSeverities.Error)
                ? AssetBulkAssignmentRowStatuses.Error
                : check.Issues.Count > 0
                    ? AssetBulkAssignmentRowStatuses.Warning
                    : AssetBulkAssignmentRowStatuses.Valid;
        }

        return new AssetBulkAssignmentCheck(
            assets.GroupBy(asset => asset.AssetRegistrationId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal),
            activeAssignments);
    }

    /// <summary>"Employee", "Department", "Location" or "Other Entity"; the common detail when the file leaves it blank; null when not recognised.</summary>
    private static string? TargetOf(AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common) =>
        values.AssignmentType is null ? common?.AssignTo : AssetBulkAssignmentTargets.Normalize(values.AssignmentType);

    private static void CheckAsset(
        RowCheck check,
        AssetBulkAssignmentValues values,
        IReadOnlyDictionary<string, AssetRegistration> assetsByTag,
        IReadOnlyDictionary<string, AssetRegistration> assetsBySerial,
        IReadOnlyDictionary<string, string> currentAssignments,
        Dictionary<string, int> firstRowOfAsset,
        AssetBulkAssignmentCommonDetails? common)
    {
        if (values.AssetTag is null)
        {
            check.Error(AssetBulkAssignmentFields.AssetTag, "Asset Tag / Serial is required", "Enter the asset tag or serial number.");
            return;
        }

        var asset = assetsByTag.GetValueOrDefault(values.AssetTag) ?? assetsBySerial.GetValueOrDefault(values.AssetTag);
        if (asset is null)
        {
            check.Error(AssetBulkAssignmentFields.AssetTag, "Asset not found", $"Asset tag or serial '{values.AssetTag}' does not exist in the system.");
            return;
        }

        var resolved = check.Resolved;
        resolved.AssetRegistrationId = asset.AssetRegistrationId;
        resolved.AssetName = asset.BasicInformation.AssetName;
        resolved.AssetTag = asset.BasicInformation.AssetTag ?? values.AssetTag;
        resolved.AssetType = asset.Classification.AssetType;

        if (!firstRowOfAsset.TryAdd(asset.AssetRegistrationId, check.RowNumber))
        {
            check.Error(AssetBulkAssignmentFields.AssetTag, "Duplicate asset", $"The same asset is also in row {firstRowOfAsset[asset.AssetRegistrationId]}.");
        }

        if (values.AssetName is not null && asset.BasicInformation.AssetName is not null
            && !string.Equals(values.AssetName, asset.BasicInformation.AssetName, StringComparison.OrdinalIgnoreCase))
        {
            check.Warning(AssetBulkAssignmentFields.AssetName, "Asset name differs",
                $"The file has '{values.AssetName}'; the asset is registered as '{asset.BasicInformation.AssetName}'.");
        }

        if (values.AssetType is not null && asset.Classification.AssetType is not null
            && !string.Equals(values.AssetType, asset.Classification.AssetType, StringComparison.OrdinalIgnoreCase))
        {
            check.Warning(AssetBulkAssignmentFields.AssetType, "Asset type differs",
                $"The file has '{values.AssetType}'; the asset is registered as '{asset.Classification.AssetType}'.");
        }

        if (currentAssignments.TryGetValue(asset.AssetRegistrationId, out var assignmentNumber))
        {
            resolved.CurrentAssignmentNumber = assignmentNumber;
            check.Warning(AssetBulkAssignmentFields.AssetTag, "Asset already assigned",
                common?.OverrideExistingAssignment == true
                    ? $"Asset is in {assignmentNumber}; it will be released from it and reassigned."
                    : $"Asset is in {assignmentNumber}; it will be skipped unless Override existing assignment is ticked.");
        }
    }

    private static void CheckEmployee(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups)
    {
        var resolved = check.Resolved;

        if (values.AssignTo is null)
        {
            if (!string.IsNullOrWhiteSpace(common?.Employee?.EmployeeId))
            {
                resolved.Employee = Copy(common.Employee);
            }
            else
            {
                check.Error(AssetBulkAssignmentFields.AssignTo, "Assign To (Employee) is required", "Enter the employee name or ID.");
            }
        }
        else
        {
            var byCode = lookups.Employees.Where(employee => Matches(employee.EmployeeCode, values.AssignTo)).ToList();
            var candidates = byCode.Count > 0
                ? byCode
                : lookups.Employees.Where(employee => Matches(employee.EmployeeName, values.AssignTo)).ToList();

            if (candidates.Count == 0)
            {
                check.Error(AssetBulkAssignmentFields.AssignTo, "Employee not found", $"Employee '{values.AssignTo}' does not exist in the system.");
            }
            else if (candidates.Count > 1)
            {
                check.Error(AssetBulkAssignmentFields.AssignTo, "Employee is not unique", $"{candidates.Count} employees match '{values.AssignTo}'; use the employee ID.");
            }
            else
            {
                var employee = candidates[0];
                resolved.Employee = new AssetAssignmentEmployee
                {
                    EmployeeId = employee.Id,
                    EmployeeCode = employee.EmployeeCode,
                    EmployeeName = employee.EmployeeName,
                    Designation = employee.Designation,
                    DepartmentId = employee.Department?.Id,
                    DepartmentName = employee.Department?.Name
                };

                if (!IsActive(employee.Status))
                {
                    check.Warning(AssetBulkAssignmentFields.AssignTo, "Employee not active", "Assigned employee is inactive in the system.");
                }
            }
        }

        // The Department column wins over the employee's own department; the common department fills a gap.
        if (values.Department is not null)
        {
            var department = FindDepartment(check, AssetBulkAssignmentFields.Department, values.Department, lookups);
            if (department is not null && resolved.Employee is not null)
            {
                resolved.Employee.DepartmentId = department.Id;
                resolved.Employee.DepartmentName = department.DepartmentName;
            }
        }
        else if (resolved.Employee is not null && resolved.Employee.DepartmentId is null && common?.Department is not null)
        {
            resolved.Employee.DepartmentId = common.Department.DepartmentId;
            resolved.Employee.DepartmentName = common.Department.DepartmentName;
        }

        resolved.Location = OptionalLocation(check, values, common, lookups);
    }

    private static void CheckDepartmentTarget(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups)
    {
        var resolved = check.Resolved;
        var field = values.AssignTo is not null ? AssetBulkAssignmentFields.AssignTo : AssetBulkAssignmentFields.Department;
        var text = values.AssignTo ?? values.Department;

        if (text is null)
        {
            if (common?.Department is not null)
            {
                resolved.Department = Copy(common.Department);
            }
            else
            {
                check.Error(AssetBulkAssignmentFields.Department, "Department is required",
                    "Enter the department in the file or choose it in Assignment Details.");
            }
        }
        else if (FindDepartment(check, field, text, lookups) is { } department)
        {
            resolved.Department = ToDepartment(department, common);
        }

        resolved.Location = OptionalLocation(check, values, common, lookups);
    }

    private static void CheckLocationTarget(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups)
    {
        var resolved = check.Resolved;

        // The Location / Site column (or the common site) gives the site; Assign To names the place within it.
        var location = values.Location is not null
            ? ResolveLocationText(check, AssetBulkAssignmentFields.Location, values.Location, lookups)
            : Copy(common?.Location);

        if (values.AssignTo is not null)
        {
            location ??= new AssetAssignmentLocation();
            var unmatched = ResolveInto(location, [values.AssignTo], lookups);
            if (unmatched.Count > 0)
            {
                unmatched = ResolveInto(location, SplitLocation(values.AssignTo), lookups);
            }

            if (unmatched.Count > 0)
            {
                var within = location.SiteName is null ? string.Empty : $" at {location.SiteName}";
                check.Error(AssetBulkAssignmentFields.AssignTo, "Location not found", $"Location '{values.AssignTo}'{within} was not found.");
            }
        }

        if (location is null || (location.SiteId ?? location.BuildingId ?? location.FloorId ?? location.RoomId) is null)
        {
            if (!check.HasError(AssetBulkAssignmentFields.Location) && !check.HasError(AssetBulkAssignmentFields.AssignTo))
            {
                check.Error(AssetBulkAssignmentFields.Location, "Location / Site is required",
                    "Enter the location in the file or choose it in Assignment Details.");
            }
        }

        resolved.Location = location;
        resolved.Department = OptionalDepartment(check, values, common, lookups);
    }

    private static void CheckOtherEntity(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups)
    {
        var resolved = check.Resolved;
        resolved.OtherEntityName = values.AssignTo ?? common?.OtherEntity?.EntityName;
        if (resolved.OtherEntityName is null)
        {
            check.Error(AssetBulkAssignmentFields.AssignTo, "Assign To is required", "Enter the vendor, contractor or other entity name.");
        }

        resolved.Department = OptionalDepartment(check, values, common, lookups);
        resolved.Location = OptionalLocation(check, values, common, lookups);
    }

    private static void CheckDates(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, DateTime today)
    {
        var resolved = check.Resolved;

        var startDate = values.StartDate is null
            ? common?.StartDate
            : ParseDate(check, AssetBulkAssignmentFields.StartDate, values.StartDate);
        if (values.StartDate is null && startDate is null)
        {
            check.Error(AssetBulkAssignmentFields.StartDate, "Start Date is required", "Enter it in the file or in Assignment Details.");
        }

        var endDate = values.EndDate is null
            ? common is { IsOpenEnded: false } ? common.EndDate : null
            : ParseDate(check, AssetBulkAssignmentFields.EndDate, values.EndDate);

        resolved.StartDate = startDate;
        resolved.EndDate = endDate;

        if (startDate < today)
        {
            check.Warning(AssetBulkAssignmentFields.StartDate, "Start date in the past", $"Start date ({Display(startDate)}) is earlier than today.");
        }

        if (endDate < startDate)
        {
            check.Error(AssetBulkAssignmentFields.EndDate, "End date before start date",
                $"End date ({Display(endDate)}) is earlier than start date ({Display(startDate)}).");
        }
        else if (endDate < today)
        {
            check.Warning(AssetBulkAssignmentFields.EndDate, "End date in the past", $"End date ({Display(endDate)}) is earlier than today.");
        }
    }

    /// <summary>For Employee and Other Entity rows: the Location / Site column if given, else the common location.</summary>
    private static AssetAssignmentLocation? OptionalLocation(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups) =>
        values.Location is not null
            ? ResolveLocationText(check, AssetBulkAssignmentFields.Location, values.Location, lookups)
            : Copy(common?.Location);

    /// <summary>For Location and Other Entity rows: the Department column if given, else the common department.</summary>
    private static AssetAssignmentDepartment? OptionalDepartment(RowCheck check, AssetBulkAssignmentValues values, AssetBulkAssignmentCommonDetails? common, Lookups lookups)
    {
        if (values.Department is null)
        {
            return Copy(common?.Department);
        }

        return FindDepartment(check, AssetBulkAssignmentFields.Department, values.Department, lookups) is { } department
            ? ToDepartment(department, common)
            : null;
    }

    private static AssignmentDepartmentLookup? FindDepartment(RowCheck check, string field, string text, Lookups lookups)
    {
        var department = lookups.Departments.FirstOrDefault(item => Matches(item.Code, text))
            ?? lookups.Departments.FirstOrDefault(item => Matches(item.DepartmentName, text));

        if (department is null)
        {
            check.Error(field, "Invalid department", $"Department '{text}' not found.");
            return null;
        }

        if (!IsActive(department.Status))
        {
            check.Warning(field, "Department not active", $"Department '{department.DepartmentName}' is inactive in the system.");
        }

        return department;
    }

    /// <summary>The department, with the manager chosen in Assignment Details when it is the same department, else its head.</summary>
    private static AssetAssignmentDepartment ToDepartment(AssignmentDepartmentLookup department, AssetBulkAssignmentCommonDetails? common)
    {
        var manager = common?.Department?.DepartmentId == department.Id && common.Department.Manager is not null
            ? common.Department.Manager
            : department.DepartmentHead?.Name is null
                ? null
                : new AssetAssignmentPerson { Id = department.DepartmentHead.Id, Name = department.DepartmentHead.Name };

        return new AssetAssignmentDepartment
        {
            DepartmentId = department.Id,
            DepartmentName = department.DepartmentName,
            Manager = manager
        };
    }

    /// <summary>
    /// Reads e.g. "Head Office - Floor 2". The first level must be found; later levels that are not found
    /// are kept as text with a warning.
    /// </summary>
    private static AssetAssignmentLocation? ResolveLocationText(RowCheck check, string field, string text, Lookups lookups)
    {
        var location = new AssetAssignmentLocation();
        var unmatched = ResolveInto(location, [text], lookups);
        if (unmatched.Count > 0)
        {
            location = new AssetAssignmentLocation();
            var segments = SplitLocation(text);
            unmatched = ResolveInto(location, segments, lookups);

            if (unmatched.Count == segments.Count)
            {
                check.Error(field, "Location not found", $"Location '{text}' was not found.");
                return null;
            }
        }

        if (unmatched.Count > 0)
        {
            check.Warning(field, "Location partly found",
                $"'{string.Join(" - ", unmatched)}' was not found{(location.SiteName is null ? string.Empty : $" at {location.SiteName}")}; it is kept as text.");
            location.RoomName ??= string.Join(" - ", unmatched);
            location.LocationPath = PathOf(location);
        }

        return location;
    }

    /// <summary>Fills the levels of <paramref name="location"/> that are still empty from the given names; returns the names not found.</summary>
    private static List<string> ResolveInto(AssetAssignmentLocation location, IReadOnlyCollection<string> segments, Lookups lookups)
    {
        var unmatched = new List<string>();
        foreach (var segment in segments)
        {
            if (location.SiteId is null
                && lookups.Sites.FirstOrDefault(site => Matches(site.SiteName, segment) || Matches(site.Code, segment)) is { } site)
            {
                location.SiteId = site.Id;
                location.SiteCode = site.Code;
                location.SiteName = site.SiteName;
                continue;
            }

            if (location.BuildingId is null
                && lookups.Buildings.FirstOrDefault(building =>
                    (Matches(building.Name, segment) || Matches(building.Code, segment))
                    && (location.SiteId is null || building.Site?.Id == location.SiteId)) is { } building)
            {
                location.BuildingId = building.Id;
                location.BuildingName = building.Name;
                location.SiteId ??= building.Site?.Id;
                location.SiteName ??= building.Site?.Name;
                continue;
            }

            if (location.FloorId is null
                && lookups.Floors.FirstOrDefault(floor =>
                    (Matches(floor.Name, segment) || Matches(floor.Code, segment))
                    && (location.SiteId is null || floor.Site?.Id == location.SiteId)
                    && (location.BuildingId is null || floor.BuildingId == location.BuildingId)) is { } floor)
            {
                location.FloorId = floor.Id;
                location.FloorName = floor.Name;
                location.BuildingId ??= floor.BuildingId;
                location.BuildingName ??= floor.BuildingName;
                location.SiteId ??= floor.Site?.Id;
                location.SiteName ??= floor.Site?.Name;
                continue;
            }

            if (location.RoomId is null
                && lookups.Locations.FirstOrDefault(place => Matches(place.LocationName, segment) || Matches(place.Code, segment)) is { } place)
            {
                location.RoomId = place.Id;
                location.RoomName = place.LocationName;
                continue;
            }

            unmatched.Add(segment);
        }

        location.LocationPath = PathOf(location);
        return unmatched;
    }

    private static List<string> SplitLocation(string text) =>
        text.Split(LocationSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? PathOf(AssetAssignmentLocation location)
    {
        var levels = new[] { location.SiteName, location.BuildingName, location.FloorName, location.RoomName }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return levels.Count == 0 ? null : string.Join(" > ", levels);
    }

    private static DateTime? ParseDate(RowCheck check, string field, string text)
    {
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return ContractText.DateOnly(date);
        }

        check.Error(field, "Invalid date", $"'{text}' is not a valid date; use e.g. 2025-01-15.");
        return null;
    }

    private static string Display(DateTime? date) => date?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static void MaxLength(RowCheck check, string field, string? value, int maxLength)
    {
        if (value?.Length > maxLength)
        {
            check.Error(field, "Too long", $"{AssetBulkAssignmentFields.LabelFor(field)} cannot be longer than {maxLength} characters.");
        }
    }

    // Master data without a status is treated as active.
    private static bool IsActive(string? status) =>
        string.IsNullOrWhiteSpace(status) || string.Equals(status.Trim(), ActiveStatus, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? value, string text) =>
        value is not null && string.Equals(value.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase);

    private static List<string> Distinct(IEnumerable<string?> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static Dictionary<string, AssetRegistration> IndexBy(IEnumerable<AssetRegistration> assets, Func<AssetRegistration, string?> key)
    {
        var index = new Dictionary<string, AssetRegistration>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (key(asset) is { Length: > 0 } value)
            {
                index.TryAdd(value.Trim(), asset);
            }
        }

        return index;
    }

    private static AssetAssignmentEmployee Copy(AssetAssignmentEmployee employee) => new()
    {
        EmployeeId = employee.EmployeeId,
        EmployeeCode = employee.EmployeeCode,
        EmployeeName = employee.EmployeeName,
        Designation = employee.Designation,
        DepartmentId = employee.DepartmentId,
        DepartmentName = employee.DepartmentName,
        Manager = employee.Manager
    };

    private static AssetAssignmentDepartment? Copy(AssetAssignmentDepartment? department) => department is null
        ? null
        : new AssetAssignmentDepartment
        {
            DepartmentId = department.DepartmentId,
            DepartmentName = department.DepartmentName,
            Manager = department.Manager
        };

    private static AssetAssignmentLocation? Copy(AssetAssignmentLocation? location) => location is null
        ? null
        : new AssetAssignmentLocation
        {
            SiteId = location.SiteId,
            SiteCode = location.SiteCode,
            SiteName = location.SiteName,
            BuildingId = location.BuildingId,
            BuildingName = location.BuildingName,
            FloorId = location.FloorId,
            FloorName = location.FloorName,
            RoomId = location.RoomId,
            RoomName = location.RoomName,
            LocationPath = location.LocationPath
        };

    private sealed record Lookups(
        IReadOnlyCollection<AssignmentEmployeeLookup> Employees,
        IReadOnlyCollection<AssignmentDepartmentLookup> Departments,
        IReadOnlyCollection<AssignmentSiteLookup> Sites,
        IReadOnlyCollection<AssignmentBuildingLookup> Buildings,
        IReadOnlyCollection<AssignmentFloorLookup> Floors,
        IReadOnlyCollection<AssignmentLocationLookup> Locations);

    /// <summary>The issues and resolved values collected for one row.</summary>
    private sealed class RowCheck(AssetBulkAssignmentRow row)
    {
        public int RowNumber => row.RowNumber;

        public List<AssetBulkAssignmentIssue> Issues { get; } = [];

        public AssetBulkAssignmentResolved Resolved { get; } = new();

        public void Error(string field, string message, string details) => Add(field, AssetBulkAssignmentSeverities.Error, message, details);

        public void Warning(string field, string message, string details) => Add(field, AssetBulkAssignmentSeverities.Warning, message, details);

        public bool HasError(string field) =>
            Issues.Any(issue => issue.Field == field && issue.Severity == AssetBulkAssignmentSeverities.Error);

        private void Add(string field, string severity, string message, string details) =>
            Issues.Add(new AssetBulkAssignmentIssue { Field = field, Severity = severity, Message = message, Details = details });
    }
}
