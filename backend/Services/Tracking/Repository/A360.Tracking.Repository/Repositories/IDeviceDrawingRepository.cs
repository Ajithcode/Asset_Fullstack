using A360.Repository.Repositories;
using DeviceDrawingEntity = A360.Tracking.Domain.Entities.DeviceDrawing;

namespace A360.Tracking.Repository.Repositories;

public interface IDeviceDrawingRepository : IMongoRepository<DeviceDrawingEntity>
{
}
