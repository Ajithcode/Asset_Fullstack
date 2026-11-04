/** Small view helpers shared by the Inspection pages. */

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** '2026-10-22' or an ISO date-time -> '22 Oct 2026'; empty for null. */
export function fmtDate(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value.length === 10 ? `${value}T00:00:00` : value);
  if (isNaN(d.getTime())) return value;
  return `${String(d.getDate()).padStart(2, '0')} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
}

/** ISO date-time -> '20 Oct 2026 09:24'. */
export function fmtDateTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (isNaN(d.getTime())) return value;
  return `${fmtDate(value)} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

/** ISO date-time -> '09:00'. */
export function fmtTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  return isNaN(d.getTime()) ? '' : `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

/** Local 'YYYY-MM-DD' for a Date (no UTC shift). */
export function isoDay(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Pill modifier class for any status / severity / priority label used in the module. */
export function pillClass(label: string | null | undefined): string {
  switch ((label ?? '').toLowerCase()) {
    case 'active':
    case 'published':
    case 'completed':
    case 'valid':
    case 'pass':
    case 'signed':
    case 'created':
    case 'closed':
      return 'pill pill--green';
    case 'in progress':
      return 'pill pill--blue';
    case 'scheduled':
    case 'pending verification':
      return 'pill pill--purple';
    case 'high':
    case 'medium':
    case 'due soon':
    case 'open':
    case 'awaiting review':
      return 'pill pill--amber';
    case 'critical':
    case 'overdue':
    case 'out of tolerance':
    case 'fail':
    case 'cancelled':
      return 'pill pill--red';
    default:
      return 'pill';
  }
}

/** Two-letter initials: 'Ali Khan' -> 'AK'. */
export function initials(name: string | null | undefined): string {
  return (name ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('');
}

/** Client-side paging over an already filtered list. */
export class Pager {
  page = 1;
  constructor(readonly size = 7) {}

  slice<T>(rows: T[]): T[] {
    if (this.page > this.pages(rows.length)) this.page = 1;
    return rows.slice((this.page - 1) * this.size, this.page * this.size);
  }

  pages(total: number): number {
    return Math.max(1, Math.ceil(total / this.size));
  }

  pageList(total: number): number[] {
    return Array.from({ length: this.pages(total) }, (_, i) => i + 1);
  }

  /** 'Showing 1 – 7 of 86' */
  label(total: number, noun: string): string {
    if (!total) return `No ${noun}`;
    const from = (this.page - 1) * this.size + 1;
    return `Showing ${from} – ${Math.min(this.page * this.size, total)} of ${total} ${noun}`;
  }
}

/** Saves `rows` as a CSV download. */
export function downloadCsv(fileName: string, rows: Record<string, unknown>[]): void {
  if (!rows.length) return;
  const headers = Object.keys(rows[0]!);
  const escape = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`;
  const csv = [headers.join(','), ...rows.map((r) => headers.map((h) => escape(r[h])).join(','))].join('\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
