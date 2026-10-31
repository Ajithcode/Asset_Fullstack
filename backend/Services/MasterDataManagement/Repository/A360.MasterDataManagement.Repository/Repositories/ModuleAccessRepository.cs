using System.Text.RegularExpressions;
using ModuleAccessEntity = A360.MasterDataManagement.Domain.Entities.ModuleAccess;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ModuleAccessRepository : MongoRepository<ModuleAccessEntity>,
    IModuleAccessRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_modules";

    public ModuleAccessRepository(IMongoDatabase database)
        : base(database.GetCollection<ModuleAccessEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ModuleAccessEntity>> SearchAsync(
        ModuleAccessFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ModuleAccessEntity>.Filter;
        var filters = new List<FilterDefinition<ModuleAccessEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ModuleName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
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
            .SortBy(x => x.ModuleName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ModuleAccessEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ModuleAccessEntity>.Filter;
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
            new CreateIndexModel<ModuleAccessEntity>(
                Builders<ModuleAccessEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_module_client_code"
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
