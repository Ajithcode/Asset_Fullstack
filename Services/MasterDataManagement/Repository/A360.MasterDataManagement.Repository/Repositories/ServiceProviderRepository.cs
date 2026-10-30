using System.Text.RegularExpressions;
using ServiceProviderEntity = A360.MasterDataManagement.Domain.Entities.ServiceProvider;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ServiceProviderRepository : MongoRepository<ServiceProviderEntity>,
    IServiceProviderRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_service_providers";

    public ServiceProviderRepository(IMongoDatabase database)
        : base(database.GetCollection<ServiceProviderEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ServiceProviderEntity>> SearchAsync(
        ServiceProviderFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ServiceProviderEntity>.Filter;
        var filters = new List<FilterDefinition<ServiceProviderEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.ServiceProviderName, pattern),
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

        if (!string.IsNullOrWhiteSpace(filter.ServiceType))
        {
            filters.Add(builder.Regex(x => x.ServiceType, ExactIgnoreCase(filter.ServiceType)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.ServiceProviderName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceProviderEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ServiceProviderEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> CountWithSkillAsync(
        string skillName,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ServiceProviderEntity>.Filter;
        var filter = (builder.AnyEq(x => x.Skills, skillName))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task RenameSkillAsync(
        string oldName,
        string newName,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ServiceProviderEntity>.Filter;

        await Collection.UpdateManyAsync(
            builder.AnyEq(x => x.Skills, oldName) & builder.Eq(x => x.ClientId, clientId),
            Builders<ServiceProviderEntity>.Update.Set("skills.$[skill]", newName),
            new UpdateOptions
            {
                ArrayFilters =
                [
                    new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("skill", oldName))
                ]
            },
            cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ServiceProviderEntity>(
                Builders<ServiceProviderEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_service_provider_client_code"
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
