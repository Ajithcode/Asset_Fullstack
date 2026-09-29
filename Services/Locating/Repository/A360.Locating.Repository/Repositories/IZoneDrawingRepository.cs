using A360.Repository.Repositories;
using ZoneDrawingEntity = A360.Locating.Domain.Entities.ZoneDrawing;

namespace A360.Locating.Repository.Repositories;

public interface IZoneDrawingRepository : IMongoRepository<ZoneDrawingEntity>
{
    Task<IReadOnlyCollection<ZoneDrawingEntity>> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default);
}
