using A360.Repository.Repositories;
using FailureReasonEntity = A360.Inspection.Domain.Entities.FailureReason;

namespace A360.Inspection.Repository.Repositories;

public interface IFailureReasonRepository : IMongoRepository<FailureReasonEntity>
{
    Task<FailureReasonEntity?> GetByFailureReasonCodeAsync(string failureReasonCode, CancellationToken cancellationToken = default);
}
