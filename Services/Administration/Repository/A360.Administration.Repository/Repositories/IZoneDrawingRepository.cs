using A360.Repository.Repositories;
using ZoneDrawingEntity = A360.Administration.Domain.Entities.ZoneDrawing;

namespace A360.Administration.Repository.Repositories;

public interface IZoneDrawingRepository : IMongoRepository<ZoneDrawingEntity>
{
    Task<IReadOnlyCollection<ZoneDrawingEntity>> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default);
}
