using System.Text.RegularExpressions;
using CriticalityEntity = A360.MasterDataManagement.Domain.Entities.Criticality;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class CriticalityRepository : MongoRepository<CriticalityEntity>,
    ICriticalityRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_criticalities";

    public CriticalityRepository(IMongoDatabase database)
        : base(database.GetCollection<CriticalityEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<CriticalityEntity>> SearchAsync(
        CriticalityFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CriticalityEntity>.Filter;
        var filters = new List<FilterDefinition<CriticalityEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.CriticalityName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.RiskLevel))
        {
            filters.Add(builder.Regex(x => x.RiskLevel, ExactIgnoreCase(filter.RiskLevel)));
        }

        if (!string.IsNullOrWhiteSpace(filter.MaintenancePriority))
        {
            filters.Add(builder.Regex(x => x.MaintenancePriority, ExactIgnoreCase(filter.MaintenancePriority)));
        }

        if (filter.DefaultCriticality is not null)
        {
            filters.Add(builder.Eq(x => x.DefaultCriticality, filter.DefaultCriticality.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.CriticalityName)
            .ToListAsync(cancellationToken);
    }

    public async Task<CriticalityEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CriticalityEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ClearDefaultAsync(
        string? clientId,
        string exceptCriticalityId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CriticalityEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.ClientId, clientId)
                & builder.Eq(x => x.DefaultCriticality, true)
                & builder.Ne(x => x.Id, exceptCriticalityId),
            Builders<CriticalityEntity>.Update.Set(x => x.DefaultCriticality, false),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<CriticalityEntity>(
                Builders<CriticalityEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_criticality_client_code"
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
