import { CalendarException, WorkingCalendar } from '../shared/inspection.models';
import { isoDay } from '../shared/insp-utils';

export const WEEK_DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
export const EXCEPTION_TYPES = ['Holiday', 'Site exception', 'Working override'];

/** 'YYYY-MM-DD' (or ISO date-time) -> local Date at midnight. */
export function parseDay(value: string): Date {
  return new Date(`${value.slice(0, 10)}T00:00:00`);
}

/** JS getDay() (0 = Sunday) -> 'Monday' ... 'Sunday'. */
export function dayName(d: Date): string {
  return WEEK_DAYS[(d.getDay() + 6) % 7]!;
}

export function exceptionOn(cal: WorkingCalendar, day: string): CalendarException | undefined {
  return cal.exceptions.find((e) => e.date.slice(0, 10) === day);
}

/** True when the weekday is marked non-working in the working week. */
export function isWeekendDay(cal: WorkingCalendar, d: Date): boolean {
  const wd = cal.workingWeek.find((w) => w.day === dayName(d));
  return wd ? !wd.working : false;
}

/** Working day after applying exceptions: overrides open a day, holidays / site exceptions close it. */
export function isWorkingDay(cal: WorkingCalendar, d: Date): boolean {
  const ex = exceptionOn(cal, isoDay(d));
  if (ex) {
    if (ex.type === 'Working override') return true;
    if (ex.type === 'Site exception') return ex.duration !== 'Full day' ? !isWeekendDay(cal, d) : false;
    if (ex.type === 'Holiday') return !cal.skipHolidays && !isWeekendDay(cal, d);
  }
  return !isWeekendDay(cal, d);
}

/** Applies the non-working-day rule; returns the same day when it is already a working day. */
export function reschedule(cal: WorkingCalendar, d: Date): Date {
  if (isWorkingDay(cal, d) || cal.nonWorkingDayRule === 'Keep date') return d;
  const step = cal.nonWorkingDayRule === 'Move to previous working day' ? -1 : 1;
  const out = new Date(d);
  for (let i = 0; i < 60; i++) {
    out.setDate(out.getDate() + step);
    if (isWorkingDay(cal, out)) return out;
  }
  return d;
}

export function exceptionClass(type: string): string {
  switch (type) {
    case 'Site exception':
      return 'ex--site';
    case 'Working override':
      return 'ex--override';
    case 'Holiday':
      return 'ex--holiday';
    default:
      return '';
  }
}
