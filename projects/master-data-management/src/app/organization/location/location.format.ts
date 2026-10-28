const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** '2024-01-12T10:30' -> '12-Jan-2024 10:30 AM' */
export function formatDateTime(value: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
  if (!m) return value || '-';
  const [, y, mo, d, h, mi] = m;
  const hour = Number(h);
  const h12 = String(hour % 12 || 12).padStart(2, '0');
  return `${d}-${MONTHS[Number(mo) - 1]}-${y} ${h12}:${mi} ${hour < 12 ? 'AM' : 'PM'}`;
}

/** 5000 -> '5,000 m²' */
export function formatArea(value: number | null): string {
  return value === null || value === undefined ? '-' : `${value.toLocaleString('en-US')} m²`;
}
