using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetDisposalRequestRepository
    : MongoRepository<AssetDisposalRequest>, IAssetDisposalRequestRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetDisposalRequest";

    public const string DraftStatus = "Draft";
    public const string PendingApprovalStatus = "Pending Approval";
    public const string ApprovedStatus = "Approved";
    public const string RejectedStatus = "Rejected";
    public const string CompletedStatus = "Completed";
    public const string CancelledStatus = "Cancelled";

    // Approver levels.
    public const string PendingApproval = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Skipped = "Skipped";

    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public AssetDisposalRequestRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetDisposalRequest>(CollectionName))
    {
    }

    public async Task<AssetDisposalRequestQueryResult> QueryAsync(AssetDisposalRequestQuery query, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AssetDisposalRequest>.Filter;
        var filters = new List<FilterDefinition<AssetDisposalRequest>>
        {
            builder.Eq(request => request.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(request => request.RequestNumber, pattern),
                builder.Regex(request => request.RequestTitle, pattern),
                builder.Regex("requested_by.name", pattern),
                builder.Regex("method.vendor.name", pattern),
                builder.Regex("assets.asset_tag", pattern),
                builder.Regex("assets.asset_name", pattern)));
        }

        AddExact(filters, "method.method", query.Method);
        AddExact(filters, "reason", query.Reason);

        if (!string.IsNullOrWhiteSpace(query.ApproverUserId))
        {
            filters.Add(builder.Eq(request => request.Status, PendingApprovalStatus));
            filters.Add(builder.ElemMatch(
                request => request.Approvers,
                Builders<AssetDisposalApprover>.Filter.And(
                    Builders<AssetDisposalApprover>.Filter.Eq("approver.user_id", query.ApproverUserId.Trim()),
                    Builders<AssetDisposalApprover>.Filter.Eq(approver => approver.Status, PendingApproval))));
        }

        if (query.FromDate is not null)
        {
            filters.Add(builder.Gte(request => request.RequestDate, query.FromDate));
        }

        if (query.ToDate is not null)
        {
            filters.Add(builder.Lte(request => request.RequestDate, query.ToDate));
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

        var items = await Collection
            .Find(filter)
            .Sort(Builders<AssetDisposalRequest>.Sort.Descending(request => request.CreatedAt).Ascending(request => request.Id))
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

        return new AssetDisposalRequestQueryResult(items, totalCount, statusCounts);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetOpenRequestNumbersAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        string? excludeId,
        CancellationToken cancellationToken = default)
    {
        if (assetRegistrationIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var builder = Builders<AssetDisposalRequest>.Filter;
        var filters = new List<FilterDefinition<AssetDisposalRequest>>
        {
            builder.Eq(request => request.IsDeleted, false),
            builder.In(request => request.Status, [PendingApprovalStatus, ApprovedStatus]),
            builder.In("assets.asset_registration_id", assetRegistrationIds)
        };

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            filters.Add(builder.Ne(request => request.Id, excludeId));
        }

        var open = await Collection
            .Find(builder.And(filters))
            .Project(request => new AssetDisposalRequest { RequestNumber = request.RequestNumber, Assets = request.Assets })
            .ToListAsync(cancellationToken);

        var requested = assetRegistrationIds.ToHashSet(StringComparer.Ordinal);
        var numbers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var request in open)
        {
            foreach (var asset in request.Assets.Where(asset => requested.Contains(asset.AssetRegistrationId)))
            {
                numbers.TryAdd(asset.AssetRegistrationId, request.RequestNumber);
            }
        }

        return numbers;
    }

    private static void AddExact(List<FilterDefinition<AssetDisposalRequest>> filters, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<AssetDisposalRequest>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value) =>
        new($"^{Regex.Escape(value.Trim())}$", "i");

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetDisposalRequest>(
                Builders<AssetDisposalRequest>.IndexKeys.Ascending(request => request.RequestNumber),
                new CreateIndexOptions { Name = "ix_asset_disposal_request_request_number" }),
            new CreateIndexModel<AssetDisposalRequest>(
                Builders<AssetDisposalRequest>.IndexKeys.Ascending("assets.asset_registration_id"),
                new CreateIndexOptions { Name = "ix_asset_disposal_request_asset_registration_id" }),
            new CreateIndexModel<AssetDisposalRequest>(
                Builders<AssetDisposalRequest>.IndexKeys.Ascending("approvers.approver.user_id").Ascending(request => request.Status),
                new CreateIndexOptions { Name = "ix_asset_disposal_request_approver_status" }),
            new CreateIndexModel<AssetDisposalRequest>(
                Builders<AssetDisposalRequest>.IndexKeys.Descending(request => request.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_disposal_request_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
