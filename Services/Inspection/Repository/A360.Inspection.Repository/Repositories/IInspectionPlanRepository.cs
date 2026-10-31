using A360.Repository.Repositories;
using InspectionPlanEntity = A360.Inspection.Domain.Entities.InspectionPlan;

namespace A360.Inspection.Repository.Repositories;

public interface IInspectionPlanRepository : IMongoRepository<InspectionPlanEntity>
{
    Task<InspectionPlanEntity?> GetByPlanCodeAsync(string planCode, CancellationToken cancellationToken = default);
}
