using System.Text.RegularExpressions;
using SkillEntity = A360.MasterDataManagement.Domain.Entities.Skill;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class SkillRepository : MongoRepository<SkillEntity>,
    ISkillRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_skills";

    public SkillRepository(IMongoDatabase database)
        : base(database.GetCollection<SkillEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SkillEntity>> SearchAsync(
        SkillFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SkillEntity>.Filter;
        var filters = new List<FilterDefinition<SkillEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.SkillName, pattern),
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
            .SortBy(x => x.SkillName)
            .ToListAsync(cancellationToken);
    }

    public async Task<SkillEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<SkillEntity>.Filter;
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
            new CreateIndexModel<SkillEntity>(
                Builders<SkillEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_skill_client_code"
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
