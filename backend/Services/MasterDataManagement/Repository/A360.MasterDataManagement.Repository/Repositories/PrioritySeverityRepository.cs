using System.Text.RegularExpressions;
using PrioritySeverityEntity = A360.MasterDataManagement.Domain.Entities.PrioritySeverity;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class PrioritySeverityRepository : MongoRepository<PrioritySeverityEntity>,
    IPrioritySeverityRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_priority_severities";

    public PrioritySeverityRepository(IMongoDatabase database)
        : base(database.GetCollection<PrioritySeverityEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PrioritySeverityEntity>> SearchAsync(
        PrioritySeverityFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<PrioritySeverityEntity>.Filter;
        var filters = new List<FilterDefinition<PrioritySeverityEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.Name, pattern),
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
        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PrioritySeverityEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<PrioritySeverityEntity>.Filter;
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
            new CreateIndexModel<PrioritySeverityEntity>(
                Builders<PrioritySeverityEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_priority_severity_client_code"
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
