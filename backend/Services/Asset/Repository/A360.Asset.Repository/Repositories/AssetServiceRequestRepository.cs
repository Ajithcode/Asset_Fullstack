using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetServiceRequestRepository
    : MongoRepository<AssetServiceRequest>, IAssetServiceRequestRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetServiceRequest";

    public const string DraftStatus = "Draft";
    public const string OpenStatus = "Open";
    public const string InProgressStatus = "In Progress";
    public const string OnHoldStatus = "On Hold";
    public const string ApprovedStatus = "Approved";
    public const string ClosedStatus = "Closed";
    public const string CancelledStatus = "Cancelled";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public AssetServiceRequestRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetServiceRequest>(CollectionName))
    {
    }

    public async Task<AssetServiceRequestQueryResult> QueryAsync(AssetServiceRequestQuery query, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetServiceRequest>.Filter;
        var filters = new List<FilterDefinition<AssetServiceRequest>>
        {
            builder.Eq(request => request.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(request => request.RequestNumber, pattern),
                builder.Regex(request => request.Title, pattern),
                builder.Regex("asset.asset_tag", pattern),
                builder.Regex("asset.asset_name", pattern),
                builder.Regex("requested_by.name", pattern),
                builder.Regex("assigned_to.name", pattern),
                builder.Regex("work_orders.work_order_id", pattern),
                builder.Regex(request => request.ReferenceNumber, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(query.AssetRegistrationId))
        {
            filters.Add(builder.Eq("asset.asset_registration_id", query.AssetRegistrationId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedToUserId))
        {
            filters.Add(builder.Eq("assigned_to.user_id", query.AssignedToUserId.Trim()));
        }

        AddExact(filters, "priority", query.Priority);
        AddExact(filters, "request_type", query.RequestType);

        if (query.FromDate is not null)
        {
            filters.Add(builder.Gte(request => request.RequestedDate, query.FromDate));
        }

        if (query.ToDate is not null)
        {
            // Requested dates carry a time, so the whole "to" day is included.
            filters.Add(builder.Lt(request => request.RequestedDate, query.ToDate.Value.AddDays(1)));
        }

        // Status is applied after the counts, so the summary keeps every status while one is selected.
        var withoutStatus = builder.And(filters);
        var filter = withoutStatus;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filter = builder.And(withoutStatus, builder.Regex(request => request.Status, ExactIgnoreCase(query.Status)));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        // The list does not need the timeline or links, which can be long.
        var items = await Collection
            .Find(filter)
            .Project<AssetServiceRequest>(Builders<AssetServiceRequest>.Projection
                .Exclude(request => request.Updates)
                .Exclude(request => request.RelatedAssets)
                .Exclude(request => request.Documents))
            .Sort(Builders<AssetServiceRequest>.Sort.Descending(request => request.RequestedDate).Descending(request => request.CreatedAt).Ascending(request => request.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        var totalCount = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var statuses = await Collection
            .Find(withoutStatus)
            .Project(request => request.Status)
            .ToListAsync(cancellationToken);

        var statusCounts = statuses
            .GroupBy(status => status ?? string.Empty)
            .ToDictionary(group => group.Key, group => (long)group.Count());

        return new AssetServiceRequestQueryResult(items, totalCount, statusCounts);
    }

    public async Task<AssetServiceRequest?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(request => request.RequestNumber == requestNumber.Trim() && !request.IsDeleted, new FindOptions { Collation = CaseInsensitive })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void AddExact(List<FilterDefinition<AssetServiceRequest>> filters, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetServiceRequest>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value) =>
        new($"^{Regex.Escape(value.Trim())}$", "i");

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetServiceRequest>(
                Builders<AssetServiceRequest>.IndexKeys.Ascending(request => request.RequestNumber),
                new CreateIndexOptions { Name = "ix_asset_service_request_request_number" }),
            new CreateIndexModel<AssetServiceRequest>(
                Builders<AssetServiceRequest>.IndexKeys.Ascending("asset.asset_registration_id").Descending(request => request.RequestedDate),
                new CreateIndexOptions { Name = "ix_asset_service_request_asset_requested_date" }),
            new CreateIndexModel<AssetServiceRequest>(
                Builders<AssetServiceRequest>.IndexKeys.Ascending("assigned_to.user_id").Ascending(request => request.Status),
                new CreateIndexOptions { Name = "ix_asset_service_request_assigned_to_status" }),
            new CreateIndexModel<AssetServiceRequest>(
                Builders<AssetServiceRequest>.IndexKeys.Descending(request => request.RequestedDate),
                new CreateIndexOptions { Name = "ix_asset_service_request_requested_date" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
