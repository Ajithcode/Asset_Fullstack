using ShiftEntity = A360.MasterDataManagement.Domain.Entities.Shift;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IShiftRepository : IMongoRepository<ShiftEntity>
{
    Task<IReadOnlyCollection<ShiftEntity>> SearchAsync(ShiftFilter filter, CancellationToken cancellationToken = default);

    Task<ShiftEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ShiftFilter(
    string? Search,
    string? Status,
    string? ShiftTime,
    // A full day name, e.g. "Monday": only shifts that work that day.
    string? WorkingDay,
    string? ClientId);
