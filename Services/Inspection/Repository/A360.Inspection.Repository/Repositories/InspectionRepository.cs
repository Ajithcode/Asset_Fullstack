using MongoDB.Driver;
using A360.Repository.Repositories;
using InspectionEntity = A360.Inspection.Domain.Entities.Inspection;

namespace A360.Inspection.Repository.Repositories;

public sealed class InspectionRepository : MongoRepository<InspectionEntity>, IInspectionRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "inspections";

    public InspectionRepository(IMongoDatabase database)
        : base(database.GetCollection<InspectionEntity>(CollectionName))
    {
    }

    public async Task<InspectionEntity?> GetByInspectionCodeAsync(string inspectionCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(inspection => inspection.InspectionCode == inspectionCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<InspectionEntity>(
                Builders<InspectionEntity>.IndexKeys.Ascending(inspection => inspection.InspectionCode),
                new CreateIndexOptions { Name = "ix_inspections_inspection_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
