using System.Text.RegularExpressions;
using MaintenanceTypeEntity = A360.MasterDataManagement.Domain.Entities.MaintenanceType;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class MaintenanceTypeRepository : MongoRepository<MaintenanceTypeEntity>,
    IMaintenanceTypeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_maintenance_types";

    public MaintenanceTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<MaintenanceTypeEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MaintenanceTypeEntity>> SearchAsync(
        MaintenanceTypeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<MaintenanceTypeEntity>.Filter;
        var filters = new List<FilterDefinition<MaintenanceTypeEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.MaintenanceTypeName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.MaintenanceTypeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<MaintenanceTypeEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<MaintenanceTypeEntity>.Filter;
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
            new CreateIndexModel<MaintenanceTypeEntity>(
                Builders<MaintenanceTypeEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_maintenance_type_client_code"
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
