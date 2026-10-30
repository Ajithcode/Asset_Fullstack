using A360.Repository.Repositories;
using HolidayAndWorkingCalendarEntity = A360.Inspection.Domain.Entities.HolidayAndWorkingCalendar;

namespace A360.Inspection.Repository.Repositories;

public interface IHolidayAndWorkingCalendarRepository : IMongoRepository<HolidayAndWorkingCalendarEntity>
{
    Task<HolidayAndWorkingCalendarEntity?> GetByCalendarCodeAsync(string calendarCode, CancellationToken cancellationToken = default);
}
