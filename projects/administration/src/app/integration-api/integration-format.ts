import { IntegrationStatus, LogLevel, RunStatus } from './integration-api.data';

/** Display helpers shared by the Integration & API page and its tabs. */

export const HTTPS_URL = /^https:\/\/[a-z0-9-]+(\.[a-z0-9-]+)+(:\d{2,5})?(\/[^\s]*)?$/i;

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** "Today 10:42", "Yesterday 17:20" or "12 Mar 2026 09:15". */
export function stampLabel(iso: string | null): string {
  if (!iso) return 'Never';
  const d = new Date(iso);
  const time = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  const now = new Date();
  if (sameDay(d, now)) return `Today ${time}`;
  const y = new Date(now);
  y.setDate(now.getDate() - 1);
  if (sameDay(d, y)) return `Yesterday ${time}`;
  if (d.getTime() > now.getTime()) {
    const t = new Date(now);
    t.setDate(now.getDate() + 1);
    if (sameDay(d, t)) return `Tomorrow ${time}`;
  }
  return `${d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })} ${time}`;
}

/** "5 minutes ago" / "in 10 minutes" / "2 days ago". */
export function relative(iso: string | null): string {
  if (!iso) return '';
  const diff = new Date(iso).getTime() - Date.now();
  const mins = Math.round(Math.abs(diff) / 60000);
  let text: string;
  if (mins < 1) return diff >= 0 ? 'in under a minute' : 'just now';
  if (mins < 60) text = `${mins} minute${mins === 1 ? '' : 's'}`;
  else if (mins < 1440) text = `${Math.round(mins / 60)} hour${Math.round(mins / 60) === 1 ? '' : 's'}`;
  else text = `${Math.round(mins / 1440)} day${Math.round(mins / 1440) === 1 ? '' : 's'}`;
  return diff >= 0 ? `in ${text}` : `${text} ago`;
}

/** 84 → "01:24". */
export function duration(sec: number): string {
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export function shortDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function statusTone(s: IntegrationStatus): string {
  return s === 'Connected' ? 'green' : s === 'Active' ? 'blue' : s === 'Warning' ? 'orange' : 'grey';
}

export function runTone(s: RunStatus): string {
  return s === 'Success' ? 'green' : s === 'Warning' ? 'orange' : 'red';
}

export function levelTone(l: LogLevel): string {
  return l === 'Info' ? 'blue' : l === 'Warning' ? 'orange' : 'red';
}

/** Masked secret showing only its last 4 characters. */
export function masked(last4: string, prefix = ''): string {
  return `${prefix}••••••••••••${last4}`;
}

export function slug(text: string): string {
  return text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}
