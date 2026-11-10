using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Dashboard.Repository.Mobile;

public sealed class MobileDashboardRepository : IMobileDashboardRepository
{
    // Collections owned by other services in the shared Asset360 database (same names as their repositories use).
    public const string AssetRegistrationCollection = "AssetRegistration";
    public const string AssetConditionAssessmentCollection = "AssetConditionAssessment";
    public const string WorkOrderCollection = "work_orders";
    public const string InspectionCollection = "inspections";
    public const string ApprovalTaskCollection = "workflow_approval_tasks";
    public const string IoTAlertCollection = "IoTAlert";
    public const string SparePartCollection = "spare_parts";
    public const string PmScheduleCollection = "preventive_maintenance_schedules";
    public const string UserCollection = "Users";

    /// <summary>Statuses after which a work order no longer needs action.</summary>
    public static readonly string[] ClosedWorkOrderStatuses = ["Completed", "Cancelled", "Canceled", "Closed"];

    /// <summary>Statuses after which an approval task no longer needs a decision.</summary>
    public static readonly string[] ClosedApprovalStatuses = ["Approved", "Rejected", "Completed", "Cancelled", "Canceled", "Closed"];

    // Statuses and ids are matched ignoring case, as everywhere else in the services.
    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoDatabase _database;

    public MobileDashboardRepository(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<MobileAsset>> GetAssetsAsync(CancellationToken cancellationToken = default)
    {
        var projection = Builders<MobileAsset>.Projection
            .Include("asset_registration_id")
            .Include("basic_information.asset_name")
            .Include("basic_information.asset_tag")
            .Include("basic_information.model")
            .Include("basic_information.manufacturer")
            .Include("basic_information.status")
            .Include("basic_information.condition")
            .Include("basic_information.criticality")
            .Include("basic_information.images")
            .Include("classification.category")
            .Include("classification.asset_type")
            .Include("location_assignment.location")
            .Include("created_at")
            .Include("is_deleted");

        return await Collection<MobileAsset>(AssetRegistrationCollection)
            .Find(NotDeleted<MobileAsset>())
            .Project<MobileAsset>(projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileWorkOrder>> GetWorkOrdersAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MobileWorkOrder>.Filter;
        var match = NotDeleted<MobileWorkOrder>() & filter.Or(
            filter.Nin(order => order.Status, ClosedWorkOrderStatuses),
            filter.Gte(order => order.ScheduledDate, sinceUtc),
            filter.Gte(order => order.DueDate, sinceUtc),
            filter.Gte(order => order.ActualEndDate, sinceUtc),
            filter.Gte(order => order.CreatedAt, sinceUtc));

        // Tasks, documents and history are never shown on the dashboard, so they are not loaded.
        var projection = Builders<MobileWorkOrder>.Projection
            .Exclude("tasks")
            .Exclude("documents")
            .Exclude("history")
            .Exclude("description")
            .Exclude("notes");

        return await Collection<MobileWorkOrder>(WorkOrderCollection)
            .Find(match, new FindOptions { Collation = CaseInsensitive })
            .Project<MobileWorkOrder>(projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileInspection>> GetInspectionsAsync(CancellationToken cancellationToken = default)
    {
        var projection = Builders<MobileInspection>.Projection
            .Include("inspection_code")
            .Include("title")
            .Include("inspection_type_name")
            .Include("priority")
            .Include("status")
            .Include("asset_code")
            .Include("asset_name")
            .Include("location")
            .Include("site_name")
            .Include("assigned_to")
            .Include("due_date")
            .Include("scheduled_start")
            .Include("scheduled_end")
            .Include("completed_at")
            .Include("findings_count")
            .Include("responses.task_title")
            .Include("responses.result")
            .Include("created_at")
            .Include("is_deleted");

        return await Collection<MobileInspection>(InspectionCollection)
            .Find(NotDeleted<MobileInspection>())
            .Project<MobileInspection>(projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileApprovalTask>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var match = NotDeleted<MobileApprovalTask>()
            & Builders<MobileApprovalTask>.Filter.Nin(task => task.Status, ClosedApprovalStatuses);

        return await Collection<MobileApprovalTask>(ApprovalTaskCollection)
            .Find(match, new FindOptions { Collation = CaseInsensitive })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileIoTAlert>> GetAlertsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var match = NotDeleted<MobileIoTAlert>() & Builders<MobileIoTAlert>.Filter.Gte(alert => alert.RaisedAt, sinceUtc);

        return await Collection<MobileIoTAlert>(IoTAlertCollection)
            .Find(match)
            .SortByDescending(alert => alert.RaisedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobileSparePart>> GetLowStockSparePartsAsync(CancellationToken cancellationToken = default)
    {
        var lowStock = new BsonDocument("$expr", new BsonDocument("$lte", new BsonArray { "$quantity_in_stock", "$minimum_stock_level" }));
        var match = NotDeleted<MobileSparePart>() & new BsonDocumentFilterDefinition<MobileSparePart>(lowStock);

        return await Collection<MobileSparePart>(SparePartCollection)
            .Find(match)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePmSchedule>> GetPmSchedulesDueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MobilePmSchedule>.Filter;
        var match = NotDeleted<MobilePmSchedule>()
            & filter.Gte(schedule => schedule.NextDueDate, fromUtc)
            & filter.Lt(schedule => schedule.NextDueDate, toUtc);

        return await Collection<MobilePmSchedule>(PmScheduleCollection)
            .Find(match)
            .SortBy(schedule => schedule.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, MobileAssetHealthScore>> GetLatestHealthScoresAsync(CancellationToken cancellationToken = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("is_deleted", false)),
            new BsonDocument("$sort", new BsonDocument("assessment_date", -1)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$asset_registration_id" },
                { "health_score", new BsonDocument("$first", "$health_score") },
                { "overall_condition", new BsonDocument("$first", "$overall_condition") },
                { "remarks", new BsonDocument("$first", "$remarks") },
                { "assessment_date", new BsonDocument("$first", "$assessment_date") }
            })
        };

        var rows = await _database.GetCollection<BsonDocument>(AssetConditionAssessmentCollection)
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<string, MobileAssetHealthScore>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!row["_id"].IsString || !row["health_score"].IsNumeric)
            {
                continue;
            }

            scores[row["_id"].AsString] = new MobileAssetHealthScore(
                row["_id"].AsString,
                row["health_score"].ToDecimal(),
                row["overall_condition"].IsString ? row["overall_condition"].AsString : null,
                row["remarks"].IsString ? row["remarks"].AsString : null,
                row["assessment_date"].IsValidDateTime ? row["assessment_date"].ToUniversalTime() : null);
        }

        return scores;
    }

    public async Task<IReadOnlyList<string>> GetUserAssigneeNamesAsync(string? userId, string? userName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter;
        var match = !string.IsNullOrWhiteSpace(userId)
            ? filter.Eq("user_id", userId.Trim())
            : !string.IsNullOrWhiteSpace(userName) ? filter.Eq("user_name", userName.Trim()) : null;

        var names = new List<string>();
        if (match is not null)
        {
            var user = await _database.GetCollection<BsonDocument>(UserCollection)
                .Find(match & filter.Ne("is_deleted", true), new FindOptions { Collation = CaseInsensitive })
                .Project(Builders<BsonDocument>.Projection.Include("user_name").Include("short_name").Include("email"))
                .FirstOrDefaultAsync(cancellationToken);

            names.AddRange(new[] { "user_name", "short_name", "email" }
                .Select(field => user is not null && user.TryGetValue(field, out var value) && value.IsString ? value.AsString.Trim() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))!);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            names.Add(userName.Trim());
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IMongoCollection<TRecord> Collection<TRecord>(string name) => _database.GetCollection<TRecord>(name);

    private static FilterDefinition<TRecord> NotDeleted<TRecord>() => Builders<TRecord>.Filter.Ne("is_deleted", true);
}
