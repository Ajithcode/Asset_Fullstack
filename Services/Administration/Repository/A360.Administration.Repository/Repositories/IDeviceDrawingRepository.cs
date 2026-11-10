using A360.Repository.Repositories;
using DeviceDrawingEntity = A360.Administration.Domain.Entities.DeviceDrawing;

namespace A360.Administration.Repository.Repositories;

public interface IDeviceDrawingRepository : IMongoRepository<DeviceDrawingEntity>
{
}
