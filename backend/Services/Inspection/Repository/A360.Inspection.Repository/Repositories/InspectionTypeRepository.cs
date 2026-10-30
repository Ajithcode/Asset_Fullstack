using MongoDB.Driver;
using A360.Repository.Repositories;
using InspectionTypeEntity = A360.Inspection.Domain.Entities.InspectionType;

namespace A360.Inspection.Repository.Repositories;

public sealed class InspectionTypeRepository : MongoRepository<InspectionTypeEntity>, IInspectionTypeRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "inspection_types";

    public InspectionTypeRepository(IMongoDatabase database)
        : base(database.GetCollection<InspectionTypeEntity>(CollectionName))
    {
    }

    public async Task<InspectionTypeEntity?> GetByInspectionTypeCodeAsync(string inspectionTypeCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(inspectionType => inspectionType.InspectionTypeCode == inspectionTypeCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<InspectionTypeEntity>(
                Builders<InspectionTypeEntity>.IndexKeys.Ascending(inspectionType => inspectionType.InspectionTypeCode),
                new CreateIndexOptions { Name = "ix_inspection_types_inspection_type_code", Unique = true }),
            new CreateIndexModel<InspectionTypeEntity>(
                Builders<InspectionTypeEntity>.IndexKeys.Ascending(inspectionType => inspectionType.AssetId),
                new CreateIndexOptions { Name = "ix_inspection_types_asset_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
