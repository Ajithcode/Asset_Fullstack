using MongoDB.Driver;
using A360.Repository.Repositories;
using CalibrationInstrumentEntity = A360.Inspection.Domain.Entities.CalibrationInstrument;

namespace A360.Inspection.Repository.Repositories;

public sealed class CalibrationInstrumentRepository : MongoRepository<CalibrationInstrumentEntity>, ICalibrationInstrumentRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "calibration_instruments";

    public CalibrationInstrumentRepository(IMongoDatabase database)
        : base(database.GetCollection<CalibrationInstrumentEntity>(CollectionName))
    {
    }

    public async Task<CalibrationInstrumentEntity?> GetByInstrumentCodeAsync(string instrumentCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(instrument => instrument.InstrumentCode == instrumentCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<CalibrationInstrumentEntity>(
                Builders<CalibrationInstrumentEntity>.IndexKeys.Ascending(instrument => instrument.InstrumentCode),
                new CreateIndexOptions { Name = "ix_calibration_instruments_instrument_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
