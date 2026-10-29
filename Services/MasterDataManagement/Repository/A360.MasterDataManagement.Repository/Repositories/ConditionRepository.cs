using System.Text.RegularExpressions;
using ConditionEntity = A360.MasterDataManagement.Domain.Entities.Condition;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ConditionRepository : MongoRepository<ConditionEntity>,
    IConditionRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_conditions";

    public ConditionRepository(IMongoDatabase database)
        : base(database.GetCollection<ConditionEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ConditionEntity>> SearchAsync(
        ConditionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ConditionEntity>.Filter;
        var filters = new List<FilterDefinition<ConditionEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ConditionName, pattern),
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

        if (filter.DefaultCondition is not null)
        {
            filters.Add(builder.Eq(x => x.DefaultCondition, filter.DefaultCondition.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        // Best condition first, which is the order pick lists usually want.
        return await Collection
            .Find(combined)
            .SortByDescending(x => x.HealthScore)
            .ThenBy(x => x.ConditionName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConditionEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ConditionEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ClearDefaultAsync(
        string? clientId,
        string exceptConditionId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ConditionEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.Eq(x => x.ClientId, clientId)
                & builder.Eq(x => x.DefaultCondition, true)
                & builder.Ne(x => x.Id, exceptConditionId),
            Builders<ConditionEntity>.Update.Set(x => x.DefaultCondition, false),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ConditionEntity>(
                Builders<ConditionEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_condition_client_code"
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
