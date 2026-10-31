import { CalibrationInstrument } from '../shared/inspection.models';
import { isoDay } from '../shared/insp-utils';

export const CAL_STATUSES = ['Valid', 'Due Soon', 'Overdue', 'Out of Tolerance'];
export const DUE_SOON_DAYS = 30;

export function addDays(day: string, n: number): string {
  const d = new Date(`${day}T00:00:00`);
  d.setDate(d.getDate() + n);
  return isoDay(d);
}

export function addMonths(day: string, n: number): string {
  const d = new Date(`${day}T00:00:00`);
  const target = new Date(d.getFullYear(), d.getMonth() + n, 1);
  const last = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
  target.setDate(Math.min(d.getDate(), last));
  return isoDay(target);
}

/**
 * Status derived from the data: a stored 'Out of Tolerance' or a failed last result wins, otherwise the next due date
 * decides (past = Overdue, within 30 days = Due Soon, later = Valid). Used for display; saved only when a user action
 * changes the instrument anyway.
 */
export function calibrationStatus(i: Pick<CalibrationInstrument, 'status' | 'lastResult' | 'nextDue'>, today = isoDay(new Date())): string {
  if (i.status === 'Out of Tolerance' || (i.lastResult ?? '').toLowerCase() === 'fail') return 'Out of Tolerance';
  if (!i.nextDue) return i.status || 'Valid';
  const due = i.nextDue.slice(0, 10);
  if (due < today) return 'Overdue';
  if (due <= addDays(today, DUE_SOON_DAYS)) return 'Due Soon';
  return 'Valid';
}

/** Absolute tolerance for a reference value from text like '±0.05 bar' or '±1 %'; null when not parseable. */
export function toleranceFor(tolerance: string, reference: number): number | null {
  const m = /([\d]+(?:\.\d+)?)/.exec(tolerance ?? '');
  if (!m) return null;
  const v = Number(m[1]);
  return tolerance.includes('%') ? Math.abs(reference) * (v / 100) : v;
}

/** Link target for a certificate value when it is a URL or data URL. */
export function certHref(cert: string | null | undefined): string | null {
  return cert && (/^https?:\/\//i.test(cert) || cert.startsWith('data:')) ? cert : null;
}

export function certLabel(cert: string | null | undefined): string {
  if (!cert) return '';
  if (cert.startsWith('data:')) return 'Certificate';
  if (/^https?:\/\//i.test(cert)) return decodeURIComponent(cert.split('/').pop() || cert);
  return cert;
}
