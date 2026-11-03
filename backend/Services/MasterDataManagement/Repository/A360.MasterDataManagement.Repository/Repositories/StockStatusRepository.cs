using System.Text.RegularExpressions;
using StockStatusEntity = A360.MasterDataManagement.Domain.Entities.StockStatus;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class StockStatusRepository : MongoRepository<StockStatusEntity>,
    IStockStatusRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_stock_statuses";

    public StockStatusRepository(IMongoDatabase database)
        : base(database.GetCollection<StockStatusEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<StockStatusEntity>> SearchAsync(
        StockStatusFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<StockStatusEntity>.Filter;
        var filters = new List<FilterDefinition<StockStatusEntity>>();

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

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            filters.Add(builder.Regex(x => x.Type, ExactIgnoreCase(filter.Type)));
        }

        if (filter.SystemDefault is not null)
        {
            filters.Add(builder.Eq(x => x.SystemDefault, filter.SystemDefault.Value));
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

    public async Task<StockStatusEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<StockStatusEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ClearSystemDefaultAsync(
        string? clientId,
        string exceptId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<StockStatusEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.ClientId, clientId)
                & builder.Eq(x => x.SystemDefault, true)
                & builder.Ne(x => x.Id, exceptId),
            Builders<StockStatusEntity>.Update.Set(x => x.SystemDefault, false),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<StockStatusEntity>(
                Builders<StockStatusEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_stock_status_client_code"
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
