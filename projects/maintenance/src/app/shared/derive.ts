/**
 * Derived values shared by the Maintenance pages: display statuses (Overdue is computed, never stored),
 * date-range filtering, monthly buckets for the trend charts, and period-over-period change.
 */
import { PredictiveMaintenance, PreventiveMaintenance, ServiceRequest, WorkOrder } from './maintenance.models';
import { isoDay } from './mnt-utils';

export interface DateRange {
  /** 'YYYY-MM-DD', inclusive. */
  from: string;
  to: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export const today = (): string => isoDay(new Date());

/** 'YYYY-MM-DD' part of an ISO value, or ''. */
export function day(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

export function inRange(value: string | null | undefined, range: DateRange): boolean {
  const d = day(value);
  return !!d && d >= range.from && d <= range.to;
}

/** Same-length range immediately before `range` (for "vs previous period"). */
export function previousRange(range: DateRange): DateRange {
  const from = new Date(`${range.from}T00:00:00`);
  const to = new Date(`${range.to}T00:00:00`);
  const days = Math.round((to.getTime() - from.getTime()) / 86_400_000) + 1;
  const prevTo = new Date(from.getTime() - 86_400_000);
  const prevFrom = new Date(prevTo.getTime() - (days - 1) * 86_400_000);
  return { from: isoDay(prevFrom), to: isoDay(prevTo) };
}

/** Percentage change from `before` to `now`, rounded; null when there is no baseline. */
export function change(now: number, before: number): number | null {
  if (!before) return null;
  return Math.round(((now - before) / before) * 100);
}

/** Share of `part` in `total` as a whole percent. */
export function pct(part: number, total: number): number {
  return total ? Math.round((part / total) * 100) : 0;
}

/** Month buckets covering the range: keys 'YYYY-MM', labels 'Jan' (or "Jan '27" when the range spans years). */
export function monthBuckets(range: DateRange): { key: string; label: string }[] {
  const start = new Date(`${range.from}T00:00:00`);
  const end = new Date(`${range.to}T00:00:00`);
  const multiYear = start.getFullYear() !== end.getFullYear();
  const out: { key: string; label: string }[] = [];
  const d = new Date(start.getFullYear(), start.getMonth(), 1);
  while (d <= end && out.length < 36) {
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    out.push({ key, label: multiYear ? `${MONTHS[d.getMonth()]} '${String(d.getFullYear()).slice(2)}` : MONTHS[d.getMonth()]! });
    d.setMonth(d.getMonth() + 1);
  }
  return out;
}

/** Counts per month bucket for each series: series(row) returns the series name or null to skip. */
export function monthlySeries<T>(
  rows: T[],
  range: DateRange,
  dateOf: (row: T) => string | null | undefined,
  seriesOf: (row: T) => string | null,
  seriesNames: string[]
): { labels: string[]; values: Record<string, number[]> } {
  const buckets = monthBuckets(range);
  const index = new Map(buckets.map((b, i) => [b.key, i]));
  const values: Record<string, number[]> = Object.fromEntries(seriesNames.map((s) => [s, buckets.map(() => 0)]));
  for (const row of rows) {
    const d = day(dateOf(row));
    if (!d || d < range.from || d > range.to) continue;
    const i = index.get(d.slice(0, 7));
    const s = seriesOf(row);
    if (i === undefined || !s || !values[s]) continue;
    values[s]![i]!++;
  }
  return { labels: buckets.map((b) => b.label), values };
}

// ---------------------------------------------------------------- work orders

const WO_DONE = ['Completed', 'Cancelled'];

export function woDue(w: WorkOrder): string {
  return day(w.dueDate ?? w.scheduledDate);
}

/** Date a work order is reported under (due date, else created). */
export function woDate(w: WorkOrder): string {
  return woDue(w) || day(w.createdAt);
}

export function woIsOpen(w: WorkOrder): boolean {
  return !WO_DONE.includes(w.status ?? '');
}

/** Stored status, or 'Overdue' when still open past its due date. */
export function woStatus(w: WorkOrder): string {
  const due = woDue(w);
  if (woIsOpen(w) && due && due < today()) return 'Overdue';
  return w.status || 'Open';
}

// ---------------------------------------------------------------- service requests

const SR_DONE = ['Resolved', 'Closed', 'Rejected'];

export function srIsOpen(r: ServiceRequest): boolean {
  return !SR_DONE.includes(r.status ?? '');
}

export function srStatus(r: ServiceRequest): string {
  const target = day(r.targetDate);
  if (srIsOpen(r) && target && target < today()) return 'Overdue';
  return r.status || 'Open';
}

export function srDate(r: ServiceRequest): string {
  return day(r.requestedDate ?? r.createdAt);
}

// ---------------------------------------------------------------- preventive

/** Display status of a PM schedule: Paused / Inactive, In Progress (open WO being worked), Overdue, On Schedule. */
export function pmStatus(p: PreventiveMaintenance, workOrders: WorkOrder[]): string {
  if (p.status && p.status !== 'Active') return p.status;
  if (workOrders.some((w) => w.sourceReference === p.pmScheduleId && w.status === 'In Progress')) return 'In Progress';
  const next = day(p.nextDueDate);
  if (next && next < today()) return 'Overdue';
  return 'On Schedule';
}

/** Days between services for a frequency. Hour-based frequencies assume 10 running hours a day. */
export function frequencyDays(frequency: string): number {
  const hours = /every\s+(\d+)\s+hours?/i.exec(frequency);
  if (hours) return Math.max(1, Math.round(Number(hours[1]) / 10));
  switch (frequency) {
    case 'Daily': return 1;
    case 'Weekly': return 7;
    case 'Monthly': return 30;
    case 'Quarterly': return 91;
    case 'Bi-Annual': return 182;
    case 'Yearly': return 365;
    default: return 30;
  }
}

/** 'YYYY-MM-DD' one interval after `from` (calendar months for month-based frequencies). */
export function nextDueFrom(from: string, frequency: string): string {
  const d = new Date(`${from}T00:00:00`);
  const months: Record<string, number> = { Monthly: 1, Quarterly: 3, 'Bi-Annual': 6, Yearly: 12 };
  if (months[frequency]) d.setMonth(d.getMonth() + months[frequency]!);
  else d.setDate(d.getDate() + frequencyDays(frequency));
  return isoDay(d);
}

// ---------------------------------------------------------------- predictive

/** Normalises risk to High / At Risk / Healthy (older records may hold Critical / Medium / Low). */
export function riskOf(p: PredictiveMaintenance): string {
  switch (p.riskLevel) {
    case 'Critical':
    case 'High':
      return 'High';
    case 'Medium':
    case 'At Risk':
      return 'At Risk';
    default:
      return 'Healthy';
  }
}

export function pdmIsLive(p: PredictiveMaintenance): boolean {
  return p.status !== 'Resolved';
}

/** True when the last reading breaches the rule. */
export function isBreach(p: PredictiveMaintenance): boolean {
  if (p.lastReading === null || p.lastReading === undefined) return false;
  return p.alertCondition === 'Below Threshold' ? p.lastReading < p.thresholdValue : p.lastReading > p.thresholdValue;
}

// ---------------------------------------------------------------- range presets

export interface RangePreset {
  key: string;
  label: string;
  range: () => DateRange;
}

export const RANGE_PRESETS: RangePreset[] = [
  { key: 'year', label: 'This Year', range: () => { const y = new Date().getFullYear(); return { from: `${y}-01-01`, to: `${y}-12-31` }; } },
  { key: 'last-year', label: 'Last Year', range: () => { const y = new Date().getFullYear() - 1; return { from: `${y}-01-01`, to: `${y}-12-31` }; } },
  { key: '12m', label: 'Last 12 Months', range: () => { const t = new Date(); const f = new Date(t.getFullYear(), t.getMonth() - 11, 1); return { from: isoDay(f), to: isoDay(t) }; } },
  { key: 'quarter', label: 'This Quarter', range: () => { const t = new Date(); const q = Math.floor(t.getMonth() / 3) * 3; return { from: isoDay(new Date(t.getFullYear(), q, 1)), to: isoDay(new Date(t.getFullYear(), q + 3, 0)) }; } },
  { key: '30d', label: 'Last 30 Days', range: () => { const t = new Date(); return { from: isoDay(new Date(t.getTime() - 29 * 86_400_000)), to: isoDay(t) }; } }
];
