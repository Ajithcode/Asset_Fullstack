using System.Text.RegularExpressions;
using ApiSyncStatusEntity = A360.MasterDataManagement.Domain.Entities.ApiSyncStatus;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ApiSyncStatusRepository : MongoRepository<ApiSyncStatusEntity>,
    IApiSyncStatusRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_api_sync_statuses";

    public ApiSyncStatusRepository(IMongoDatabase database)
        : base(database.GetCollection<ApiSyncStatusEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ApiSyncStatusEntity>> SearchAsync(
        ApiSyncStatusFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ApiSyncStatusEntity>.Filter;
        var filters = new List<FilterDefinition<ApiSyncStatusEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.StatusName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (filter.IsSuccess is not null)
        {
            filters.Add(builder.Eq(x => x.IsSuccess, filter.IsSuccess.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.StatusName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiSyncStatusEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ApiSyncStatusEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ApiSyncStatusEntity>(
                Builders<ApiSyncStatusEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_api_sync_status_client_code"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
