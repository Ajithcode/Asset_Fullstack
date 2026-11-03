using ManufacturerEntity = A360.MasterDataManagement.Domain.Entities.Manufacturer;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IManufacturerRepository : IMongoRepository<ManufacturerEntity>
{
    Task<IReadOnlyCollection<ManufacturerEntity>> SearchAsync(ManufacturerFilter filter, CancellationToken cancellationToken = default);

    Task<ManufacturerEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ManufacturerFilter(
    string? Search,
    string? Status,
    string? Country,
    string? ClientId);
