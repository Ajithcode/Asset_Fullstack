using MongoDB.Driver;
using A360.Repository.Repositories;
using DefectEntity = A360.Inspection.Domain.Entities.Defect;

namespace A360.Inspection.Repository.Repositories;

public sealed class DefectRepository : MongoRepository<DefectEntity>, IDefectRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "defects";

    public DefectRepository(IMongoDatabase database)
        : base(database.GetCollection<DefectEntity>(CollectionName))
    {
    }

    public async Task<DefectEntity?> GetByDefectCodeAsync(string defectCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(defect => defect.DefectCode == defectCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<DefectEntity>(
                Builders<DefectEntity>.IndexKeys.Ascending(defect => defect.DefectCode),
                new CreateIndexOptions { Name = "ix_defects_defect_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
