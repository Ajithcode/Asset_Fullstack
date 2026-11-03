using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetAssignmentRepository
    : MongoRepository<AssetAssignmentRecord>, IAssetAssignmentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetAssignment";

    public const string DraftStatus = "Draft";
    public const string ActiveStatus = "Active";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetAssignmentRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetAssignmentRecord>(CollectionName))
    {
    }

    public async Task<AssetAssignmentQueryResult> QueryAsync(AssetAssignmentQuery query, CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<AssetAssignmentRecord>>
        {
            Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Or(
                Builders<AssetAssignmentRecord>.Filter.Regex(assignment => assignment.AssignmentNumber, pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("assets.asset_registration_id", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("assets.asset_name", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("assets.asset_tag", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("assets.serial_number", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("employee.employee_name", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("employee.employee_code", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("department.department_name", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("location.location_path", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("other_entity.entity_name", pattern),
                Builders<AssetAssignmentRecord>.Filter.Regex("custom_fields.assignment_reference", pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignTo))
        {
            var pattern = new BsonRegularExpression($"^{Regex.Escape(query.AssignTo.Trim())}$", "i");
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Regex(assignment => assignment.AssignTo, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Eq("assets.asset_registration_id", query.AssetRegistrationId.Trim()));
        }

        if (query.FromDate is not null)
        {
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Or(
                Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.ToDate, null),
                Builders<AssetAssignmentRecord>.Filter.Gte(assignment => assignment.ToDate, query.FromDate)));
        }

        if (query.ToDate is not null)
        {
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Lte(assignment => assignment.FromDate, query.ToDate));
        }

        // Status is applied after the counts, so the summary keeps every status while one is selected.
        var withoutStatus = Builders<AssetAssignmentRecord>.Filter.And(filters);
        var filter = withoutStatus;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var pattern = new BsonRegularExpression($"^{Regex.Escape(query.Status.Trim())}$", "i");
            filter = Builders<AssetAssignmentRecord>.Filter.And(
                withoutStatus,
                Builders<AssetAssignmentRecord>.Filter.Regex(assignment => assignment.Status, pattern));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetAssignmentRecord>.Sort.Descending(assignment => assignment.CreatedAt).Ascending(assignment => assignment.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var statuses = await Collection
            .Find(withoutStatus)
            .Project(assignment => assignment.Status)
            .ToListAsync(cancellationToken);

        var statusCounts = statuses
            .GroupBy(status => status ?? string.Empty)
            .ToDictionary(group => group.Key, group => (long)group.Count());

        return new AssetAssignmentQueryResult(items, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<string>> GetAssignedAssetIdsAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        DateTime asOf,
        string? excludeAssignmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (assetRegistrationIds.Count == 0)
        {
            return [];
        }

        var filters = new List<FilterDefinition<AssetAssignmentRecord>>
        {
            Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.IsDeleted, false),
            Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.Status, ActiveStatus),
            Builders<AssetAssignmentRecord>.Filter.In("assets.asset_registration_id", assetRegistrationIds),
            Builders<AssetAssignmentRecord>.Filter.Or(
                Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.ToDate, null),
                Builders<AssetAssignmentRecord>.Filter.Gte(assignment => assignment.ToDate, asOf))
        };

        if (!string.IsNullOrWhiteSpace(excludeAssignmentId))
        {
            filters.Add(Builders<AssetAssignmentRecord>.Filter.Ne(assignment => assignment.Id, excludeAssignmentId));
        }

        var assigned = await Collection
            .Find(Builders<AssetAssignmentRecord>.Filter.And(filters))
            .Project(assignment => assignment.Assets)
            .ToListAsync(cancellationToken);

        var requested = assetRegistrationIds.ToHashSet(StringComparer.Ordinal);
        return assigned
            .SelectMany(assets => assets)
            .Select(asset => asset.AssetRegistrationId)
            .Where(requested.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyCollection<AssetAssignmentRecord>> GetActiveByAssetIdsAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        DateTime asOf,
        CancellationToken cancellationToken = default)
    {
        if (assetRegistrationIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<AssetAssignmentRecord>.Filter.And(
            Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.IsDeleted, false),
            Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.Status, ActiveStatus),
            Builders<AssetAssignmentRecord>.Filter.In("assets.asset_registration_id", assetRegistrationIds),
            Builders<AssetAssignmentRecord>.Filter.Or(
                Builders<AssetAssignmentRecord>.Filter.Eq(assignment => assignment.ToDate, null),
                Builders<AssetAssignmentRecord>.Filter.Gte(assignment => assignment.ToDate, asOf)));

        return await Collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetAssignmentRecord>(
                Builders<AssetAssignmentRecord>.IndexKeys.Ascending(assignment => assignment.AssignmentNumber),
                new CreateIndexOptions { Name = "ix_asset_assignment_assignment_number" }),
            new CreateIndexModel<AssetAssignmentRecord>(
                Builders<AssetAssignmentRecord>.IndexKeys.Ascending("assets.asset_registration_id"),
                new CreateIndexOptions { Name = "ix_asset_assignment_asset_registration_id" }),
            new CreateIndexModel<AssetAssignmentRecord>(
                Builders<AssetAssignmentRecord>.IndexKeys.Descending(assignment => assignment.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_assignment_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
