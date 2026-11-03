using SystemSettingsEntity = A360.MasterDataManagement.Domain.Entities.SystemSettings;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ISystemSettingsRepository : IMongoRepository<SystemSettingsEntity>
{
    Task<SystemSettingsEntity?> GetByClientAsync(string? clientId, CancellationToken cancellationToken = default);
}
