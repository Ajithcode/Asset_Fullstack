using System.Text.RegularExpressions;
using CertificationTypeEntity = A360.MasterDataManagement.Domain.Entities.CertificationType;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class CertificationTypeRepository : MongoRepository<CertificationTypeEntity>,
    ICertificationTypeRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_certification_types";

    public CertificationTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<CertificationTypeEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<CertificationTypeEntity>> SearchAsync(
        CertificationTypeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CertificationTypeEntity>.Filter;
        var filters = new List<FilterDefinition<CertificationTypeEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.CertificationTypeName, pattern),
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

        if (!string.IsNullOrWhiteSpace(filter.ApplicableTo))
        {
            filters.Add(builder.Regex(x => x.ApplicableTo, ExactIgnoreCase(filter.ApplicableTo)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.CertificationTypeName)
            .ToListAsync(cancellationToken);
    }

    public async Task<CertificationTypeEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<CertificationTypeEntity>.Filter;
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
            new CreateIndexModel<CertificationTypeEntity>(
                Builders<CertificationTypeEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_certification_type_client_code"
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
