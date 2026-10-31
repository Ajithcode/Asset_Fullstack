using MongoDB.Driver;
using A360.Repository.Repositories;
using InspectionFindingEntity = A360.Inspection.Domain.Entities.InspectionFinding;

namespace A360.Inspection.Repository.Repositories;

public sealed class InspectionFindingRepository : MongoRepository<InspectionFindingEntity>, IInspectionFindingRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "inspection_findings";

    public InspectionFindingRepository(IMongoDatabase database)
        : base(database.GetCollection<InspectionFindingEntity>(CollectionName))
    {
    }

    public async Task<InspectionFindingEntity?> GetByFindingCodeAsync(string findingCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(finding => finding.FindingCode == findingCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<InspectionFindingEntity>(
                Builders<InspectionFindingEntity>.IndexKeys.Ascending(finding => finding.FindingCode),
                new CreateIndexOptions { Name = "ix_inspection_findings_finding_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
