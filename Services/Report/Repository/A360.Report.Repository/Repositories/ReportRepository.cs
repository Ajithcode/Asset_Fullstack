using MongoDB.Driver;
using A360.Repository.Repositories;
using ReportEntity = A360.Report.Domain.Entities.Report;

namespace A360.Report.Repository.Repositories;

public sealed class ReportRepository : MongoRepository<ReportEntity>, IReportRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "reports";

    public ReportRepository(IMongoDatabase database)
        : base(database.GetCollection<ReportEntity>(CollectionName))
    {
    }

    public async Task<ReportEntity?> GetByReportIdAsync(string reportId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return null;
        }

        return await Collection
            .Find(report => report.ReportId == reportId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReportEntity>> GetAsync(
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ReportEntity>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(report => report.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(report => report.ClientId, clientId);
        }

        return await Collection
            .Find(filter)
            .SortByDescending(report => report.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ReportEntity>(
                Builders<ReportEntity>.IndexKeys.Ascending(report => report.ReportId),
                new CreateIndexOptions { Name = "ux_reports_report_id", Unique = true }),
            new CreateIndexModel<ReportEntity>(
                Builders<ReportEntity>.IndexKeys
                    .Ascending(report => report.TenantId)
                    .Ascending(report => report.ClientId),
                new CreateIndexOptions { Name = "ix_reports_tenant_id_client_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
