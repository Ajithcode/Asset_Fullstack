using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Repository.Repositories;

/// <summary>
/// Filters of the Asset Criticality landing page. Empty values mean "all".
/// Level "Unassessed" lists assets without an approved assessment.
/// </summary>
public sealed record AssetCriticalityLandingQuery(
    string? Search = null,
    string? Organization = null,
    string? Site = null,
    string? LocationId = null,
    string? Department = null,
    string? Category = null,
    string? AssetType = null,
    string? Level = null,
    string? ApprovalStatus = null,
    string? Assessor = null,
    DateTime? ReviewDueFrom = null,
    DateTime? ReviewDueTo = null,
    bool Overdue = false,
    string? OperationalStatus = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = AssetCriticalityLandingRepository.DefaultPageSize);

/// <summary>One registered asset with its approved criticality and its open assessment, if any.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityLandingRow
{
    [BsonElement("asset_registration_id")] public string AssetRegistrationId { get; set; } = string.Empty;
    [BsonElement("asset_tag")] public string? AssetTag { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("category")] public string? Category { get; set; }
    [BsonElement("asset_type")] public string? AssetType { get; set; }
    [BsonElement("business_unit")] public string? Organization { get; set; }
    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("location_path")] public string? LocationPath { get; set; }
    [BsonElement("department")] public string? Department { get; set; }
    [BsonElement("owner")] public string? Owner { get; set; }
    [BsonElement("operational_status")] public string? OperationalStatus { get; set; }
    [BsonElement("condition")] public string? Condition { get; set; }

    // From the current approved assessment; null when the asset is unassessed.
    [BsonElement("current_assessment_id")] public string? CurrentAssessmentId { get; set; }
    [BsonElement("level")] public string? Level { get; set; }
    [BsonElement("score")] public decimal? Score { get; set; }
    [BsonElement("assessment_date")] public DateTime? AssessmentDate { get; set; }
    [BsonElement("assessed_by")] public string? AssessedBy { get; set; }
    [BsonElement("next_review_date")] public DateTime? NextReviewDate { get; set; }

    // The latest Draft / Submitted / PendingApproval / Rejected assessment.
    [BsonElement("open_assessment_id")] public string? OpenAssessmentId { get; set; }
    [BsonElement("open_status")] public string? OpenStatus { get; set; }
    [BsonElement("open_level")] public string? OpenLevel { get; set; }
    [BsonElement("open_score")] public decimal? OpenScore { get; set; }
    [BsonElement("open_assessed_by")] public string? OpenAssessedBy { get; set; }
    [BsonElement("open_submitted_at")] public DateTime? OpenSubmittedAt { get; set; }
    [BsonElement("open_type")] public string? OpenType { get; set; }

    // Open status if there is one, else Approved or Unassessed.
    [BsonElement("approval_status")] public string ApprovalStatus { get; set; } = AssetCriticalityApprovalStatuses.Unassessed;
}

public sealed record AssetCriticalityLandingPage(IReadOnlyList<AssetCriticalityLandingRow> Items, long TotalCount);

/// <param name="LevelCounts">Assets per approved level name (case as stored).</param>
public sealed record AssetCriticalityKpiCounts(
    long Total,
    IReadOnlyDictionary<string, long> LevelCounts,
    long Unassessed,
    long PendingApprovals,
    long ReassessmentOverdue);

public sealed record AssetCriticalityDistributionCell(string Category, string Level, long Count);

public sealed record AssetCriticalityFilterValues(
    IReadOnlyList<string> Organizations,
    IReadOnlyList<AssetCriticalitySiteOption> Sites,
    IReadOnlyList<string> Departments,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> AssetTypes,
    IReadOnlyList<string> OperationalStatuses,
    IReadOnlyList<string> Assessors);

public sealed record AssetCriticalitySiteOption(string Id, string Name);

/// <summary>Assessment History tab: filters over all assessments.</summary>
public sealed record AssetCriticalityHistoryQuery(
    string? Search = null,
    string? AssetRegistrationId = null,
    string? ApprovalStatus = null,
    string? Assessor = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = AssetCriticalityLandingRepository.DefaultPageSize);

/// <summary>An assessment with the asset's current name and tag (read from the asset, not the copy on the assessment).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityHistoryRow
{
    [BsonElement("id")] public string Id { get; set; } = string.Empty;
    [BsonElement("asset_registration_id")] public string AssetRegistrationId { get; set; } = string.Empty;
    [BsonElement("asset_tag")] public string? AssetTag { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("assessment_type")] public string? AssessmentType { get; set; }
    [BsonElement("assessment_date")] public DateTime? AssessmentDate { get; set; }
    [BsonElement("assessed_by")] public string? AssessedBy { get; set; }
    [BsonElement("approved_by")] public string? ApprovedBy { get; set; }
    [BsonElement("level")] public string? Level { get; set; }
    [BsonElement("score")] public decimal? Score { get; set; }
    [BsonElement("approval_status")] public string ApprovalStatus { get; set; } = AssetCriticalityApprovalStatuses.Approved;
    [BsonElement("is_current")] public bool IsCurrent { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
}

public sealed record AssetCriticalityHistoryPage(IReadOnlyList<AssetCriticalityHistoryRow> Items, long TotalCount);

public interface IAssetCriticalityLandingRepository
{
    Task<AssetCriticalityLandingPage> QueryAssetsAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every matching row (no paging), for the export; at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AssetCriticalityLandingRow>> ExportAssetsAsync(AssetCriticalityLandingQuery query, int limit, CancellationToken cancellationToken = default);

    /// <summary>KPI cards. Level, approval status and overdue are left out of the filter so every card keeps its count while one is selected.</summary>
    Task<AssetCriticalityKpiCounts> GetKpisAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetCriticalityDistributionCell>> GetDistributionAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default);

    Task<AssetCriticalityFilterValues> GetFilterValuesAsync(CancellationToken cancellationToken = default);

    Task<AssetCriticalityHistoryPage> QueryHistoryAsync(AssetCriticalityHistoryQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read side of the Asset Criticality landing page. Starts from AssetRegistration, so unassessed assets are listed too,
/// and joins each asset's current approved assessment and its latest open one. Asset fields always come from the asset.
/// </summary>
public sealed class AssetCriticalityLandingRepository(IMongoDatabase database) : IAssetCriticalityLandingRepository
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    private const string Assessments = AssetCriticalityAssessmentRepository.CollectionName;

    private static readonly Dictionary<string, string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assetId"] = "asset_tag",
        ["assetName"] = "asset_name",
        ["category"] = "category",
        ["assetType"] = "asset_type",
        ["site"] = "site_name",
        ["department"] = "department",
        ["operationalStatus"] = "operational_status",
        ["condition"] = "condition",
        ["level"] = "level_rank",
        ["score"] = "score",
        ["assessmentDate"] = "assessment_date",
        ["assessedBy"] = "assessed_by",
        ["nextReviewDate"] = "next_review_date",
        ["approvalStatus"] = "approval_status"
    };

    private IMongoCollection<BsonDocument> Assets => database.GetCollection<BsonDocument>(AssetRegistrationRepository.CollectionName);

    private IMongoCollection<BsonDocument> AssessmentDocs => database.GetCollection<BsonDocument>(Assessments);

    public async Task<AssetCriticalityLandingPage> QueryAssetsAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var stages = RowStages(query, includeCardFilters: true);
        stages.Add(SortStage(query));
        stages.Add(new BsonDocument("$facet", new BsonDocument
        {
            ["items"] = new BsonArray { new BsonDocument("$skip", (page - 1) * pageSize), new BsonDocument("$limit", pageSize) },
            ["total"] = new BsonArray { new BsonDocument("$count", "n") }
        }));

        var result = await Assets.Aggregate<BsonDocument>(stages, cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray.Select(item => BsonSerializer.Deserialize<AssetCriticalityLandingRow>(item.AsBsonDocument)).ToList() ?? [];
        var total = result?["total"].AsBsonArray.FirstOrDefault()?["n"].ToInt64() ?? 0;
        return new AssetCriticalityLandingPage(items, total);
    }

    public async Task<IReadOnlyList<AssetCriticalityLandingRow>> ExportAssetsAsync(AssetCriticalityLandingQuery query, int limit, CancellationToken cancellationToken = default)
    {
        var stages = RowStages(query, includeCardFilters: true);
        stages.Add(SortStage(query));
        stages.Add(new BsonDocument("$limit", limit));

        var docs = await Assets.Aggregate<BsonDocument>(stages, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        return docs.Select(doc => BsonSerializer.Deserialize<AssetCriticalityLandingRow>(doc)).ToList();
    }

    public async Task<AssetCriticalityKpiCounts> GetKpisAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default)
    {
        var stages = RowStages(query, includeCardFilters: false);
        stages.Add(new BsonDocument("$facet", new BsonDocument
        {
            ["levels"] = new BsonArray { new BsonDocument("$group", new BsonDocument { ["_id"] = "$level", ["n"] = new BsonDocument("$sum", 1) }) },
            ["pending"] = new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("open_status", new BsonDocument("$in", new BsonArray(AssetCriticalityApprovalStatuses.AwaitingApproval)))),
                new BsonDocument("$count", "n")
            },
            ["overdue"] = new BsonArray
            {
                new BsonDocument("$match", OverdueFilter()),
                new BsonDocument("$count", "n")
            }
        }));

        var result = await Assets.Aggregate<BsonDocument>(stages, cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken);
        var levelCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long total = 0, unassessed = 0;
        foreach (var group in result?["levels"].AsBsonArray ?? [])
        {
            var count = group["n"].ToInt64();
            total += count;
            var level = group["_id"].IsBsonNull ? null : group["_id"].AsString;
            if (string.IsNullOrWhiteSpace(level))
            {
                unassessed += count;
            }
            else
            {
                levelCounts[level] = levelCounts.GetValueOrDefault(level) + count;
            }
        }

        long CountOf(string facet) => result?[facet].AsBsonArray.FirstOrDefault()?["n"].ToInt64() ?? 0;

        return new AssetCriticalityKpiCounts(total, levelCounts, unassessed, CountOf("pending"), CountOf("overdue"));
    }

    public async Task<IReadOnlyList<AssetCriticalityDistributionCell>> GetDistributionAsync(AssetCriticalityLandingQuery query, CancellationToken cancellationToken = default)
    {
        var stages = RowStages(query, includeCardFilters: true);
        stages.Add(new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = new BsonDocument
            {
                ["category"] = new BsonDocument("$ifNull", new BsonArray { "$category", "" }),
                ["level"] = new BsonDocument("$ifNull", new BsonArray { "$level", "" })
            },
            ["n"] = new BsonDocument("$sum", 1)
        }));

        var docs = await Assets.Aggregate<BsonDocument>(stages, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        return docs
            .Select(doc => new AssetCriticalityDistributionCell(
                doc["_id"]["category"].AsString,
                doc["_id"]["level"].AsString,
                doc["n"].ToInt64()))
            .OrderBy(cell => cell.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AssetCriticalityFilterValues> GetFilterValuesAsync(CancellationToken cancellationToken = default)
    {
        var notDeleted = new BsonDocument("is_deleted", new BsonDocument("$ne", true));

        async Task<IReadOnlyList<string>> DistinctAsync(IMongoCollection<BsonDocument> collection, string field, BsonDocument filter)
        {
            var values = await (await collection.DistinctAsync<BsonValue>(field, filter, cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
            return values
                .Where(value => value.IsString && !string.IsNullOrWhiteSpace(value.AsString))
                .Select(value => value.AsString.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var siteDocs = await Assets.Aggregate()
            .Match(notDeleted)
            .Group(new BsonDocument
            {
                ["_id"] = "$location_assignment.location.site_id",
                ["name"] = new BsonDocument("$first", "$location_assignment.location.site_name")
            })
            .ToListAsync(cancellationToken);
        var sites = siteDocs
            .Where(doc => doc["_id"].IsString && !string.IsNullOrWhiteSpace(doc["_id"].AsString))
            .Select(doc => new AssetCriticalitySiteOption(doc["_id"].AsString, doc["name"].IsString ? doc["name"].AsString : doc["_id"].AsString))
            .OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var departments = (await DistinctAsync(Assets, "classification.department", notDeleted))
            .Concat(await DistinctAsync(Assets, "location_assignment.assignment.custodian_department", notDeleted))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AssetCriticalityFilterValues(
            await DistinctAsync(Assets, "classification.business_unit", notDeleted),
            sites,
            departments,
            await DistinctAsync(Assets, "classification.category", notDeleted),
            await DistinctAsync(Assets, "classification.asset_type", notDeleted),
            await DistinctAsync(Assets, "basic_information.status", notDeleted),
            await DistinctAsync(AssessmentDocs, "assessed_by", notDeleted));
    }

    public async Task<AssetCriticalityHistoryPage> QueryHistoryAsync(AssetCriticalityHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var match = new BsonDocument("is_deleted", new BsonDocument("$ne", true));
        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            match["asset_registration_id"] = query.AssetRegistrationId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.Assessor))
        {
            match["assessed_by"] = query.Assessor.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.ApprovalStatus))
        {
            // Assessments saved before the workflow have no status and count as Approved.
            match["approval_status"] = query.ApprovalStatus.Trim().Equals(AssetCriticalityApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                ? new BsonDocument("$in", new BsonArray { AssetCriticalityApprovalStatuses.Approved, BsonNull.Value })
                : query.ApprovalStatus.Trim();
        }

        var dateRange = DateRange(query.From, query.To);
        if (dateRange is not null)
        {
            match["assessment_date"] = dateRange;
        }

        var stages = new List<BsonDocument>
        {
            new("$match", match),
            new("$lookup", new BsonDocument
            {
                ["from"] = AssetRegistrationRepository.CollectionName,
                ["localField"] = "asset_registration_id",
                ["foreignField"] = "asset_registration_id",
                ["pipeline"] = new BsonArray
                {
                    new BsonDocument("$project", new BsonDocument { ["asset_tag"] = "$basic_information.asset_tag", ["asset_name"] = "$basic_information.asset_name" })
                },
                ["as"] = "asset"
            }),
            new("$set", new BsonDocument("asset", new BsonDocument("$first", "$asset"))),
            new("$project", new BsonDocument
            {
                ["_id"] = 0,
                ["id"] = new BsonDocument("$toString", "$_id"),
                ["asset_registration_id"] = 1,
                ["asset_tag"] = new BsonDocument("$ifNull", new BsonArray { "$asset.asset_tag", "$asset_tag" }),
                ["asset_name"] = new BsonDocument("$ifNull", new BsonArray { "$asset.asset_name", "$asset_name" }),
                ["assessment_type"] = 1,
                ["assessment_date"] = 1,
                ["assessed_by"] = 1,
                ["approved_by"] = 1,
                ["level"] = "$criticality_level",
                ["score"] = "$criticality_score",
                ["approval_status"] = new BsonDocument("$ifNull", new BsonArray { "$approval_status", AssetCriticalityApprovalStatuses.Approved }),
                ["is_current"] = 1,
                ["created_at"] = 1
            })
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            stages.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("asset_registration_id", pattern),
                new BsonDocument("asset_tag", pattern),
                new BsonDocument("asset_name", pattern),
                new BsonDocument("assessed_by", pattern)
            })));
        }

        stages.Add(new BsonDocument("$sort", new BsonDocument { ["assessment_date"] = -1, ["created_at"] = -1 }));
        stages.Add(new BsonDocument("$facet", new BsonDocument
        {
            ["items"] = new BsonArray { new BsonDocument("$skip", (page - 1) * pageSize), new BsonDocument("$limit", pageSize) },
            ["total"] = new BsonArray { new BsonDocument("$count", "n") }
        }));

        var result = await AssessmentDocs.Aggregate<BsonDocument>(stages, cancellationToken: cancellationToken).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray.Select(item => BsonSerializer.Deserialize<AssetCriticalityHistoryRow>(item.AsBsonDocument)).ToList() ?? [];
        var total = result?["total"].AsBsonArray.FirstOrDefault()?["n"].ToInt64() ?? 0;
        return new AssetCriticalityHistoryPage(items, total);
    }

    /// <summary>
    /// Asset filter → join assessments → flat row → assessment filters. <paramref name="includeCardFilters"/> = false leaves
    /// out level, approval status and overdue (the filters the KPI cards set).
    /// </summary>
    private static List<BsonDocument> RowStages(AssetCriticalityLandingQuery query, bool includeCardFilters)
    {
        var stages = new List<BsonDocument>
        {
            new("$match", AssetFilter(query)),
            Lookup("current", new BsonDocument
            {
                ["is_current"] = true,
                ["is_deleted"] = new BsonDocument("$ne", true),
                // Assessments saved before the workflow have no status and count as Approved.
                ["approval_status"] = new BsonDocument("$in", new BsonArray { AssetCriticalityApprovalStatuses.Approved, BsonNull.Value })
            }),
            Lookup("open", new BsonDocument
            {
                ["is_deleted"] = new BsonDocument("$ne", true),
                ["approval_status"] = new BsonDocument("$in", new BsonArray(AssetCriticalityApprovalStatuses.Open))
            }),
            new("$set", new BsonDocument
            {
                ["c"] = new BsonDocument("$first", "$current"),
                ["o"] = new BsonDocument("$first", "$open")
            }),
            new("$project", new BsonDocument
            {
                ["_id"] = 0,
                ["asset_registration_id"] = 1,
                ["created_at"] = 1,
                ["asset_tag"] = "$basic_information.asset_tag",
                ["asset_name"] = "$basic_information.asset_name",
                ["category"] = "$classification.category",
                ["asset_type"] = "$classification.asset_type",
                ["business_unit"] = "$classification.business_unit",
                ["site_name"] = "$location_assignment.location.site_name",
                ["location_path"] = "$location_assignment.location.location_path",
                ["department"] = new BsonDocument("$ifNull", new BsonArray { "$classification.department", "$location_assignment.assignment.custodian_department" }),
                ["owner"] = "$location_assignment.assignment.assigned_to_name",
                ["operational_status"] = "$basic_information.status",
                ["condition"] = "$basic_information.condition",
                ["current_assessment_id"] = new BsonDocument("$toString", "$c._id"),
                ["level"] = "$c.criticality_level",
                ["score"] = "$c.criticality_score",
                ["assessment_date"] = "$c.assessment_date",
                ["assessed_by"] = "$c.assessed_by",
                ["next_review_date"] = "$c.next_review_date",
                ["open_assessment_id"] = new BsonDocument("$toString", "$o._id"),
                ["open_status"] = "$o.approval_status",
                ["open_level"] = "$o.criticality_level",
                ["open_score"] = "$o.criticality_score",
                ["open_assessed_by"] = "$o.assessed_by",
                ["open_submitted_at"] = "$o.submitted_at",
                ["open_type"] = "$o.assessment_type",
                ["approval_status"] = new BsonDocument("$ifNull", new BsonArray
                {
                    "$o.approval_status",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$c._id", BsonNull.Value }),
                        AssetCriticalityApprovalStatuses.Approved,
                        AssetCriticalityApprovalStatuses.Unassessed
                    })
                }),
                ["level_rank"] = LevelRankExpression()
            })
        };

        var rowFilter = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(query.Assessor))
        {
            rowFilter["assessed_by"] = query.Assessor.Trim();
        }

        var reviewRange = DateRange(query.ReviewDueFrom, query.ReviewDueTo);
        if (reviewRange is not null)
        {
            rowFilter["next_review_date"] = reviewRange;
        }

        var conditions = new BsonArray();
        if (rowFilter.ElementCount > 0)
        {
            conditions.Add(rowFilter);
        }

        if (includeCardFilters)
        {
            if (!string.IsNullOrWhiteSpace(query.Level))
            {
                var level = query.Level.Trim();
                conditions.Add(level.Equals(AssetCriticalityApprovalStatuses.Unassessed, StringComparison.OrdinalIgnoreCase)
                    ? new BsonDocument("level", BsonNull.Value)
                    : new BsonDocument("level", new BsonRegularExpression("^" + Regex.Escape(level) + "$", "i")));
            }

            if (!string.IsNullOrWhiteSpace(query.ApprovalStatus))
            {
                var status = query.ApprovalStatus.Trim();
                // "Pending" (KPI card) covers Submitted and PendingApproval.
                conditions.Add(status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    ? new BsonDocument("open_status", new BsonDocument("$in", new BsonArray(AssetCriticalityApprovalStatuses.AwaitingApproval)))
                    : new BsonDocument("approval_status", status));
            }

            if (query.Overdue)
            {
                conditions.Add(OverdueFilter());
            }
        }

        if (conditions.Count > 0)
        {
            stages.Add(new BsonDocument("$match", new BsonDocument("$and", conditions)));
        }

        return stages;
    }

    private static BsonDocument Lookup(string name, BsonDocument match) => new("$lookup", new BsonDocument
    {
        ["from"] = Assessments,
        ["localField"] = "asset_registration_id",
        ["foreignField"] = "asset_registration_id",
        ["pipeline"] = new BsonArray
        {
            new BsonDocument("$match", match),
            new BsonDocument("$sort", new BsonDocument { ["assessment_date"] = -1, ["created_at"] = -1 }),
            new BsonDocument("$limit", 1),
            new BsonDocument("$project", new BsonDocument
            {
                ["criticality_level"] = 1,
                ["criticality_score"] = 1,
                ["assessment_date"] = 1,
                ["assessed_by"] = 1,
                ["next_review_date"] = 1,
                ["approval_status"] = 1
            })
        },
        ["as"] = name
    });

    private static BsonDocument AssetFilter(AssetCriticalityLandingQuery query)
    {
        var filters = new BsonArray { new BsonDocument("is_deleted", new BsonDocument("$ne", true)) };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("asset_registration_id", pattern),
                new BsonDocument("basic_information.asset_tag", pattern),
                new BsonDocument("basic_information.asset_name", pattern),
                new BsonDocument("classification.asset_type", pattern)
            }));
        }

        AddEquals(filters, "classification.business_unit", query.Organization);
        AddEquals(filters, "classification.category", query.Category);
        AddEquals(filters, "classification.asset_type", query.AssetType);
        AddEquals(filters, "basic_information.status", query.OperationalStatus);

        if (!string.IsNullOrWhiteSpace(query.Site))
        {
            var site = query.Site.Trim();
            filters.Add(new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("location_assignment.location.site_id", site),
                new BsonDocument("location_assignment.location.site_name", site)
            }));
        }

        if (!string.IsNullOrWhiteSpace(query.Department))
        {
            var department = query.Department.Trim();
            filters.Add(new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("classification.department", department),
                new BsonDocument("location_assignment.assignment.custodian_department", department)
            }));
        }

        // A location picked anywhere in the hierarchy matches every asset at or below it (as on the Asset List).
        if (!string.IsNullOrWhiteSpace(query.LocationId))
        {
            var locationId = query.LocationId.Trim();
            filters.Add(new BsonDocument("$or", new BsonArray(
                new[] { "country_id", "site_id", "building_id", "floor_id", "zone_id", "room_id" }
                    .Select(field => new BsonDocument("location_assignment.location." + field, locationId)))));
        }

        return new BsonDocument("$and", filters);
    }

    private static void AddEquals(BsonArray filters, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(new BsonDocument(field, value.Trim()));
        }
    }

    private static BsonDocument OverdueFilter() => new()
    {
        ["level"] = new BsonDocument("$ne", BsonNull.Value),
        ["next_review_date"] = new BsonDocument("$lt", DateTime.UtcNow.Date)
    };

    /// <summary>Dates are whole days: "to" includes the whole day.</summary>
    private static BsonDocument? DateRange(DateTime? from, DateTime? to)
    {
        if (from is null && to is null)
        {
            return null;
        }

        var range = new BsonDocument();
        if (from is not null)
        {
            range["$gte"] = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
        }

        if (to is not null)
        {
            range["$lt"] = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
        }

        return range;
    }

    /// <summary>Severity for sorting: Critical 4 … Low 1, other levels 0, unassessed -1.</summary>
    private static BsonDocument LevelRankExpression() => new("$switch", new BsonDocument
    {
        ["branches"] = new BsonArray(
            new[] { "low", "medium", "high", "critical" }.Select((level, index) => new BsonDocument
            {
                ["case"] = new BsonDocument("$eq", new BsonArray { new BsonDocument("$toLower", new BsonDocument("$ifNull", new BsonArray { "$c.criticality_level", "" })), level }),
                ["then"] = index + 1
            })),
        ["default"] = new BsonDocument("$cond", new BsonArray { new BsonDocument("$gt", new BsonArray { "$c._id", BsonNull.Value }), 0, -1 })
    });

    private static BsonDocument SortStage(AssetCriticalityLandingQuery query)
    {
        var field = query.SortBy is not null && SortFields.TryGetValue(query.SortBy, out var mapped) ? mapped : "asset_tag";
        var direction = query.SortDescending ? -1 : 1;
        // Ties keep a stable order so paging never repeats or skips a row.
        return new BsonDocument("$sort", new BsonDocument { [field] = direction, ["asset_registration_id"] = 1 });
    }
}
