using A360.Repository.Repositories;
using DeviceDrawingEntity = A360.Locating.Domain.Entities.DeviceDrawing;

namespace A360.Locating.Repository.Repositories;

public interface IDeviceDrawingRepository : IMongoRepository<DeviceDrawingEntity>
{
}
