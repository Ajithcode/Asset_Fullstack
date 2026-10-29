import { USER_ROLES } from './user-management.data';
import { UserRole } from './user-management.model';

export function roleOf(name: string): UserRole | undefined {
  return USER_ROLES.find((r) => r.name === name);
}

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/**
 * "Today 09:42", "Yesterday 14:18" or "24 Oct 2026 10:21".
 * With `long`: "Today, 28 Oct 2026 at 09:42" / "12 Jan 2024 at 11:20".
 */
export function loginLabel(iso: string, long = false): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const time = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  const date = d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  const today = new Date();
  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);
  const rel = sameDay(d, today) ? 'Today' : sameDay(d, yesterday) ? 'Yesterday' : '';
  if (long) return `${rel ? rel + ', ' : ''}${date} at ${time}`;
  return rel ? `${rel} ${time}` : `${date} ${time}`;
}
