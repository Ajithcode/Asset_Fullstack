using A360.Repository.Repositories;
using ReportTemplateEntity = A360.Inspection.Domain.Entities.ReportTemplate;

namespace A360.Inspection.Repository.Repositories;

public interface IReportTemplateRepository : IMongoRepository<ReportTemplateEntity>
{
    Task<ReportTemplateEntity?> GetByReportTemplateCodeAsync(string reportTemplateCode, CancellationToken cancellationToken = default);
}
