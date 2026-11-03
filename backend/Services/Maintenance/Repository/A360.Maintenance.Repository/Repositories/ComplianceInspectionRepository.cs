
using ComplianceInspectionEntity = A360.Maintenance.Domain.Entities.ComplianceInspection;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class ComplianceInspectionRepository : MongoRepository<ComplianceInspectionEntity>,
    IComplianceInspectionRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "compliance_inspections";

    public ComplianceInspectionRepository(IMongoDatabase database)
        : base(database.GetCollection<ComplianceInspectionEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ComplianceInspectionEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ComplianceInspectionEntity>(
                Builders<ComplianceInspectionEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_compliance_inspection_client"
                }),
            new CreateIndexModel<ComplianceInspectionEntity>(
                Builders<ComplianceInspectionEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_compliance_inspection_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
