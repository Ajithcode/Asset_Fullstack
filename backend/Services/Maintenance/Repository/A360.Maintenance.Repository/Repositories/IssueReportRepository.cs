
using IssueReportEntity = A360.Maintenance.Domain.Entities.IssueReport;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class IssueReportRepository : MongoRepository<IssueReportEntity>,
    IIssueReportRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "issue_reports";

    public IssueReportRepository(IMongoDatabase database)
        : base(database.GetCollection<IssueReportEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<IssueReportEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<IssueReportEntity>(
                Builders<IssueReportEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_report_client"
                }),
            new CreateIndexModel<IssueReportEntity>(
                Builders<IssueReportEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_report_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
