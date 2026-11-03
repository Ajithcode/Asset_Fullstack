using MongoDB.Driver;
using A360.Repository.Repositories;
using ReportTemplateEntity = A360.Inspection.Domain.Entities.ReportTemplate;

namespace A360.Inspection.Repository.Repositories;

public sealed class ReportTemplateRepository : MongoRepository<ReportTemplateEntity>, IReportTemplateRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "report_templates";

    public ReportTemplateRepository(IMongoDatabase database)
        : base(database.GetCollection<ReportTemplateEntity>(CollectionName))
    {
    }

    public async Task<ReportTemplateEntity?> GetByReportTemplateCodeAsync(string reportTemplateCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(template => template.ReportTemplateCode == reportTemplateCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ReportTemplateEntity>(
                Builders<ReportTemplateEntity>.IndexKeys.Ascending(template => template.ReportTemplateCode),
                new CreateIndexOptions { Name = "ix_report_templates_report_template_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
