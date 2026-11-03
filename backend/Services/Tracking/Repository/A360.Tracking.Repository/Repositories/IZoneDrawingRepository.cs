using A360.Repository.Repositories;
using ZoneDrawingEntity = A360.Tracking.Domain.Entities.ZoneDrawing;

namespace A360.Tracking.Repository.Repositories;

public interface IZoneDrawingRepository : IMongoRepository<ZoneDrawingEntity>
{
    Task<IReadOnlyCollection<ZoneDrawingEntity>> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default);
}
