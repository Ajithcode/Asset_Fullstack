using System.Text.RegularExpressions;
using AmcVendorEntity = A360.MasterDataManagement.Domain.Entities.AmcVendor;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class AmcVendorRepository : MongoRepository<AmcVendorEntity>,
    IAmcVendorRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_amc_vendors";

    public AmcVendorRepository(IMongoDatabase database)
        : base(database.GetCollection<AmcVendorEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AmcVendorEntity>> SearchAsync(
        AmcVendorFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AmcVendorEntity>.Filter;
        var filters = new List<FilterDefinition<AmcVendorEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.AmcVendorName, pattern),
                builder.Regex(x => x.Code, pattern),
                builder.Regex(x => x.PrimaryContact, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ContractType))
        {
            filters.Add(builder.Regex(x => x.ContractType, ExactIgnoreCase(filter.ContractType)));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssetTypeId))
        {
            filters.Add(builder.Eq(x => x.AssetType!.Id, filter.AssetTypeId));
        }

        if (filter.ActiveOn is not null)
        {
            filters.Add(builder.Lte(x => x.StartDate, filter.ActiveOn.Value)
                & builder.Gte(x => x.EndDate, filter.ActiveOn.Value));
        }

        if (filter.EndsBy is not null)
        {
            filters.Add(builder.Lte(x => x.EndDate, filter.EndsBy.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        // Soonest-ending contracts first, since those need attention.
        return await Collection
            .Find(combined)
            .SortBy(x => x.EndDate)
            .ThenBy(x => x.AmcVendorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<AmcVendorEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<AmcVendorEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithAssetTypeAsync(
        string assetTypeId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.AssetType!.Id == assetTypeId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateAssetTypeNameAsync(
        string assetTypeId,
        string assetTypeName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.AssetType!.Id == assetTypeId,
            Builders<AmcVendorEntity>.Update.Set(x => x.AssetType!.Name, assetTypeName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AmcVendorEntity>(
                Builders<AmcVendorEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_amc_vendor_client_code"
                }),
            new CreateIndexModel<AmcVendorEntity>(
                Builders<AmcVendorEntity>.IndexKeys
                    .Ascending(x => x.AssetType!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_amc_vendor_asset_type"
                }),
            new CreateIndexModel<AmcVendorEntity>(
                Builders<AmcVendorEntity>.IndexKeys
                    .Ascending(x => x.EndDate),
                new CreateIndexOptions
                {
                    Name = "ix_master_amc_vendor_end_date"
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
