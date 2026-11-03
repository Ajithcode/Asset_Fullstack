using MongoDB.Driver;
using A360.Repository.Repositories;
using HolidayAndWorkingCalendarEntity = A360.Inspection.Domain.Entities.HolidayAndWorkingCalendar;

namespace A360.Inspection.Repository.Repositories;

public sealed class HolidayAndWorkingCalendarRepository : MongoRepository<HolidayAndWorkingCalendarEntity>, IHolidayAndWorkingCalendarRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "holiday_and_working_calendars";

    public HolidayAndWorkingCalendarRepository(IMongoDatabase database)
        : base(database.GetCollection<HolidayAndWorkingCalendarEntity>(CollectionName))
    {
    }

    public async Task<HolidayAndWorkingCalendarEntity?> GetByCalendarCodeAsync(string calendarCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(calendar => calendar.CalendarCode == calendarCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<HolidayAndWorkingCalendarEntity>(
                Builders<HolidayAndWorkingCalendarEntity>.IndexKeys.Ascending(calendar => calendar.CalendarCode),
                new CreateIndexOptions { Name = "ix_holiday_and_working_calendars_calendar_code", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
