using System.Text.RegularExpressions;
using SupplierEntity = A360.MasterDataManagement.Domain.Entities.Supplier;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class SupplierRepository : MongoRepository<SupplierEntity>,
    ISupplierRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_suppliers";

    public SupplierRepository(IMongoDatabase database)
        : base(database.GetCollection<SupplierEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SupplierEntity>> SearchAsync(
        SupplierFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SupplierEntity>.Filter;
        var filters = new List<FilterDefinition<SupplierEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.SupplierName, pattern),
                builder.Regex(x => x.Code, pattern),
                builder.Regex(x => x.PrimaryContact, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            filters.Add(builder.Regex(x => x.Country, ExactIgnoreCase(filter.Country)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            filters.Add(builder.Regex(x => x.Category, ExactIgnoreCase(filter.Category)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.SupplierName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SupplierEntity>.Filter;
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
            new CreateIndexModel<SupplierEntity>(
                Builders<SupplierEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_supplier_client_code"
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
