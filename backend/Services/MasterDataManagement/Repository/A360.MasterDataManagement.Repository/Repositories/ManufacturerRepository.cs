using System.Text.RegularExpressions;
using ManufacturerEntity = A360.MasterDataManagement.Domain.Entities.Manufacturer;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ManufacturerRepository : MongoRepository<ManufacturerEntity>,
    IManufacturerRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_manufacturers";

    public ManufacturerRepository(IMongoDatabase database)
        : base(database.GetCollection<ManufacturerEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ManufacturerEntity>> SearchAsync(
        ManufacturerFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ManufacturerEntity>.Filter;
        var filters = new List<FilterDefinition<ManufacturerEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ManufacturerName, pattern),
                builder.Regex(x => x.Code, pattern),
                builder.Regex(x => x.ContactPerson, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            filters.Add(builder.Regex(x => x.Country, ExactIgnoreCase(filter.Country)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.ManufacturerName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ManufacturerEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ManufacturerEntity>.Filter;
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
            new CreateIndexModel<ManufacturerEntity>(
                Builders<ManufacturerEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_manufacturer_client_code"
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
