using A360.Repository.Repositories;
using CalibrationInstrumentEntity = A360.Inspection.Domain.Entities.CalibrationInstrument;

namespace A360.Inspection.Repository.Repositories;

public interface ICalibrationInstrumentRepository : IMongoRepository<CalibrationInstrumentEntity>
{
    Task<CalibrationInstrumentEntity?> GetByInstrumentCodeAsync(string instrumentCode, CancellationToken cancellationToken = default);
}
