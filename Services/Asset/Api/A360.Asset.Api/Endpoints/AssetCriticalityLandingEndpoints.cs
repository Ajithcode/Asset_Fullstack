using ClosedXML.Excel;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Services;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Endpoints;

/// <summary>Asset Criticality landing page (Assets › Asset Criticality): every registered asset with its approved criticality.</summary>
/// <remarks>All read-only; any signed-in user may call them (the group requires sign-in).</remarks>
public static class AssetCriticalityLandingEndpoints
{
    /// <summary>The export stops here so a single request cannot build an unbounded workbook.</summary>
    public const int ExportLimit = 10_000;

    public static RouteGroupBuilder MapAssetCriticalityLandingEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/assets", GetAssetsAsync).WithName("GetAssetCriticalityAssets");
        group.MapGet("/kpis", GetKpisAsync).WithName("GetAssetCriticalityKpis");
        group.MapGet("/distribution", GetDistributionAsync).WithName("GetAssetCriticalityDistribution");
        group.MapGet("/assessments", GetAssessmentsAsync).WithName("GetAssetCriticalityAssessments");
        group.MapGet("/filter-options", GetFilterOptionsAsync).WithName("GetAssetCriticalityFilterOptions");
        group.MapGet("/export", ExportAsync).WithName("ExportAssetCriticality");
        return group;
    }

    /// <summary>Query string of the landing page; every list, KPI, chart and export call takes the same filters.</summary>
    public sealed record LandingFilters(
        string? Search,
        string? Organization,
        string? Site,
        string? LocationId,
        string? Department,
        string? Category,
        string? AssetType,
        string? Level,
        string? ApprovalStatus,
        string? Assessor,
        DateTime? ReviewDueFrom,
        DateTime? ReviewDueTo,
        bool? Overdue,
        string? OperationalStatus,
        string? SortBy,
        string? SortDir,
        int? Page,
        int? PageSize)
    {
        public AssetCriticalityLandingQuery ToQuery() => new(
            Search, Organization, Site, LocationId, Department, Category, AssetType, Level, ApprovalStatus, Assessor,
            ReviewDueFrom, ReviewDueTo, Overdue == true, OperationalStatus, SortBy,
            string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase),
            Page ?? 1,
            PageSize ?? AssetCriticalityLandingRepository.DefaultPageSize);
    }

    private static async Task<IResult> GetAssetsAsync(
        [AsParameters] LandingFilters filters,
        IAssetCriticalityLandingRepository repository,
        CancellationToken cancellationToken)
    {
        var query = filters.ToQuery();
        var result = await repository.QueryAssetsAsync(query, cancellationToken);
        var pageSize = query.PageSize is < 1 or > AssetCriticalityLandingRepository.MaxPageSize ? AssetCriticalityLandingRepository.DefaultPageSize : query.PageSize;

        return Results.Ok(new CriticalityAssetPageResponse(
            result.Items.Select(CriticalityAssetRowResponse.FromRow).ToList(),
            result.TotalCount,
            Math.Max(query.Page, 1),
            pageSize));
    }

    private static async Task<IResult> GetKpisAsync(
        [AsParameters] LandingFilters filters,
        IAssetCriticalityLandingRepository repository,
        ICriticalityLevelClient levelClient,
        CancellationToken cancellationToken)
    {
        var counts = await repository.GetKpisAsync(filters.ToQuery(), cancellationToken);
        var levels = await levelClient.GetLevelsAsync(cancellationToken);

        var levelCounts = levels
            .Select(level => new CriticalityLevelCountResponse(level.Name, level.Code, level.Color, level.Rank, counts.LevelCounts.GetValueOrDefault(level.Name), level.MinScore, level.MaxScore))
            .ToList();
        // Levels on assessments that are no longer in the master still count.
        levelCounts.AddRange(counts.LevelCounts
            .Where(pair => !levels.Any(level => level.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => new CriticalityLevelCountResponse(pair.Key, null, null, Security.CriticalityAccess.LevelRank(pair.Key), pair.Value, null, null)));

        long Count(string name) => counts.LevelCounts.GetValueOrDefault(name);

        return Results.Ok(new CriticalityKpiResponse(
            counts.Total,
            Count(AssetCriticalityLevels.Critical),
            Count(AssetCriticalityLevels.High),
            Count(AssetCriticalityLevels.Medium),
            Count(AssetCriticalityLevels.Low),
            counts.Unassessed,
            counts.PendingApprovals,
            counts.ReassessmentOverdue,
            levelCounts));
    }

    private static async Task<IResult> GetDistributionAsync(
        [AsParameters] LandingFilters filters,
        IAssetCriticalityLandingRepository repository,
        CancellationToken cancellationToken)
    {
        var cells = await repository.GetDistributionAsync(filters.ToQuery(), cancellationToken);

        var byLevel = cells
            .GroupBy(cell => string.IsNullOrEmpty(cell.Level) ? AssetCriticalityApprovalStatuses.Unassessed : cell.Level, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CriticalityDistributionCount(group.Key, group.Sum(cell => cell.Count)))
            .OrderByDescending(count => Security.CriticalityAccess.LevelRank(count.Level))
            .ToList();

        var byCategory = cells
            .GroupBy(cell => string.IsNullOrEmpty(cell.Category) ? "Uncategorized" : cell.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CriticalityCategoryDistribution(
                group.Key,
                group.Sum(cell => cell.Count),
                group.GroupBy(cell => string.IsNullOrEmpty(cell.Level) ? AssetCriticalityApprovalStatuses.Unassessed : cell.Level, StringComparer.OrdinalIgnoreCase)
                    .Select(levelGroup => new CriticalityDistributionCount(levelGroup.Key, levelGroup.Sum(cell => cell.Count)))
                    .OrderByDescending(count => Security.CriticalityAccess.LevelRank(count.Level))
                    .ToList()))
            .OrderByDescending(category => category.Total)
            .ThenBy(category => category.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(new CriticalityDistributionResponse(byLevel, byCategory));
    }

    /// <summary>Assessment History tab: every assessment (any status), newest first.</summary>
    private static async Task<IResult> GetAssessmentsAsync(
        IAssetCriticalityLandingRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetRegistrationId = null,
        string? approvalStatus = null,
        string? assessor = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = AssetCriticalityLandingRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetCriticalityLandingRepository.MaxPageSize ? AssetCriticalityLandingRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryHistoryAsync(
            new AssetCriticalityHistoryQuery(search, assetRegistrationId, approvalStatus, assessor, from, to, page, pageSize),
            cancellationToken);

        return Results.Ok(new CriticalityAssessmentPageResponse(
            result.Items.Select(CriticalityAssessmentRowResponse.FromRow).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    private static async Task<IResult> GetFilterOptionsAsync(
        IAssetCriticalityLandingRepository repository,
        ICriticalityLevelClient levelClient,
        CancellationToken cancellationToken)
    {
        var values = await repository.GetFilterValuesAsync(cancellationToken);
        var levels = await levelClient.GetLevelsAsync(cancellationToken);

        return Results.Ok(new CriticalityFilterOptionsResponse(
            levels.Select(level => new CriticalityLevelResponse(level.Name, level.Code, level.Color, level.Rank)).ToList(),
            CriticalityFilterOptionsResponse.ApprovalStatusOptions,
            values.Organizations,
            values.Sites.Select(site => new CriticalityOptionResponse(site.Id, site.Name)).ToList(),
            values.Departments,
            values.Categories,
            values.AssetTypes,
            values.OperationalStatuses,
            values.Assessors,
            CriticalityExportColumns.All.Select(column => new CriticalityOptionResponse(column.Key, column.Header)).ToList()));
    }

    /// <summary>Excel file of the landing list with the same filters and sort; <c>columns</c> picks and orders the columns.</summary>
    private static async Task<IResult> ExportAsync(
        [AsParameters] LandingFilters filters,
        HttpRequest request,
        IAssetCriticalityLandingRepository repository,
        CancellationToken cancellationToken)
    {
        var format = request.Query["format"].ToString();
        if (!string.IsNullOrWhiteSpace(format) && !format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["format"] = ["Only xlsx is supported."] });
        }

        var requested = request.Query["columns"]
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        var columns = requested.Count == 0
            ? CriticalityExportColumns.All
            : requested
                .Select(key => CriticalityExportColumns.All.FirstOrDefault(column => column.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                .OfType<CriticalityExportColumn>()
                .DistinctBy(column => column.Key)
                .ToList();
        if (columns.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["columns"] = ["None of the requested columns exist."] });
        }

        var rows = await repository.ExportAssetsAsync(filters.ToQuery(), ExportLimit, cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Asset Criticality");
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c].Header;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (columns[c].Value(rows[r]))
                {
                    case DateTime date:
                        cell.Value = date;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                        break;
                    case decimal number:
                        cell.Value = number;
                        break;
                    case string text:
                        cell.Value = text;
                        break;
                }
            }
        }

        var header = sheet.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F0FF");
        sheet.SheetView.FreezeRows(1);
        if (rows.Count > 0)
        {
            sheet.Range(1, 1, rows.Count + 1, columns.Count).SetAutoFilter();
        }

        sheet.Columns().AdjustToContents(1, Math.Min(rows.Count + 1, 500));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Results.File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"asset-criticality-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }
}

public sealed record CriticalityExportColumn(string Key, string Header, Func<AssetCriticalityLandingRow, object?> Value);

/// <summary>Columns of the landing table, in display order; keys match the frontend column ids.</summary>
public static class CriticalityExportColumns
{
    public static readonly IReadOnlyList<CriticalityExportColumn> All =
    [
        new("assetId", "Asset ID", row => row.AssetTag ?? row.AssetRegistrationId),
        new("assetName", "Asset Name", row => row.AssetName),
        new("category", "Category", row => row.Category),
        new("assetType", "Asset Type", row => row.AssetType),
        new("organization", "Organization", row => row.Organization),
        new("site", "Site", row => row.SiteName),
        new("location", "Location", row => row.LocationPath),
        new("department", "Department", row => row.Department),
        new("owner", "Owner", row => row.Owner),
        new("operationalStatus", "Operational Status", row => row.OperationalStatus),
        new("condition", "Current Condition", row => row.Condition),
        new("level", "Criticality Level", row => row.Level ?? AssetCriticalityApprovalStatuses.Unassessed),
        new("score", "Criticality Score (/100)", row => row.Score),
        new("assessmentDate", "Assessment Date", row => row.AssessmentDate),
        new("assessedBy", "Assessed By", row => row.AssessedBy),
        new("nextReviewDate", "Next Review Date", row => row.NextReviewDate),
        new("approvalStatus", "Approval Status", row => CriticalityAssetRowResponse.StatusLabel(row.ApprovalStatus))
    ];
}

public sealed record CriticalityAssetRowResponse(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Category,
    string? AssetType,
    string? Organization,
    string? SiteName,
    string? LocationPath,
    string? Department,
    string? Owner,
    string? OperationalStatus,
    string? Condition,
    string? CurrentAssessmentId,
    string? Level,
    decimal? Score,
    DateTime? AssessmentDate,
    string? AssessedBy,
    DateTime? NextReviewDate,
    bool ReviewOverdue,
    string? OpenAssessmentId,
    string ApprovalStatus,
    string? OpenLevel = null,
    decimal? OpenScore = null,
    string? OpenAssessedBy = null,
    DateTime? OpenSubmittedAt = null,
    string? OpenType = null)
{
    public static CriticalityAssetRowResponse FromRow(AssetCriticalityLandingRow row) => new(
        row.AssetRegistrationId,
        row.AssetTag,
        row.AssetName,
        row.Category,
        row.AssetType,
        row.Organization,
        row.SiteName,
        row.LocationPath,
        row.Department,
        row.Owner,
        row.OperationalStatus,
        row.Condition,
        row.CurrentAssessmentId,
        row.Level,
        row.Score,
        row.AssessmentDate,
        row.AssessedBy,
        row.NextReviewDate,
        row.Level is not null && row.NextReviewDate is not null && row.NextReviewDate.Value < DateTime.UtcNow.Date,
        row.OpenAssessmentId,
        row.ApprovalStatus,
        row.OpenLevel,
        row.OpenScore,
        row.OpenAssessedBy,
        row.OpenSubmittedAt,
        row.OpenType);

    /// <summary>"PendingApproval" → "Pending Approval" for people (the API keeps the code).</summary>
    public static string StatusLabel(string? status) => status switch
    {
        AssetCriticalityApprovalStatuses.PendingApproval => "Pending Approval",
        null or "" => AssetCriticalityApprovalStatuses.Unassessed,
        _ => status
    };
}

public sealed record CriticalityAssetPageResponse(IReadOnlyList<CriticalityAssetRowResponse> Items, long TotalCount, int Page, int PageSize);

public sealed record CriticalityAssessmentRowResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssessmentType,
    DateTime? AssessmentDate,
    string? AssessedBy,
    string? ApprovedBy,
    string? Level,
    decimal? Score,
    string ApprovalStatus,
    bool IsCurrent,
    DateTime? CreatedAt)
{
    public static CriticalityAssessmentRowResponse FromRow(AssetCriticalityHistoryRow row) => new(
        row.Id, row.AssetRegistrationId, row.AssetTag, row.AssetName, row.AssessmentType, row.AssessmentDate,
        row.AssessedBy, row.ApprovedBy, row.Level, row.Score, row.ApprovalStatus, row.IsCurrent, row.CreatedAt);
}

public sealed record CriticalityAssessmentPageResponse(IReadOnlyList<CriticalityAssessmentRowResponse> Items, long TotalCount, int Page, int PageSize);

public sealed record CriticalityLevelResponse(string Name, string? Code, string? Color, int Rank);

public sealed record CriticalityLevelCountResponse(string Name, string? Code, string? Color, int Rank, long Count, int? MinScore = null, int? MaxScore = null);

/// <param name="Levels">Count per level from the MDM Criticality master, most severe first.</param>
public sealed record CriticalityKpiResponse(
    long Total,
    long Critical,
    long High,
    long Medium,
    long Low,
    long Unassessed,
    long PendingApprovals,
    long ReassessmentOverdue,
    IReadOnlyList<CriticalityLevelCountResponse> Levels);

public sealed record CriticalityDistributionCount(string Level, long Count);

public sealed record CriticalityCategoryDistribution(string Category, long Total, IReadOnlyList<CriticalityDistributionCount> Levels);

public sealed record CriticalityDistributionResponse(IReadOnlyList<CriticalityDistributionCount> ByLevel, IReadOnlyList<CriticalityCategoryDistribution> ByCategory);

public sealed record CriticalityOptionResponse(string Value, string Label);

public sealed record CriticalityFilterOptionsResponse(
    IReadOnlyList<CriticalityLevelResponse> Levels,
    IReadOnlyList<CriticalityOptionResponse> ApprovalStatuses,
    IReadOnlyList<string> Organizations,
    IReadOnlyList<CriticalityOptionResponse> Sites,
    IReadOnlyList<string> Departments,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> AssetTypes,
    IReadOnlyList<string> OperationalStatuses,
    IReadOnlyList<string> Assessors,
    IReadOnlyList<CriticalityOptionResponse> Columns)
{
    /// <summary>Values for the approvalStatus filter; "Pending" covers Submitted and Pending Approval.</summary>
    public static readonly IReadOnlyList<CriticalityOptionResponse> ApprovalStatusOptions =
    [
        new(AssetCriticalityApprovalStatuses.Draft, "Draft"),
        new("Pending", "Pending Approval"),
        new(AssetCriticalityApprovalStatuses.Approved, "Approved"),
        new(AssetCriticalityApprovalStatuses.Rejected, "Rejected"),
        new(AssetCriticalityApprovalStatuses.Unassessed, "Unassessed")
    ];
}
