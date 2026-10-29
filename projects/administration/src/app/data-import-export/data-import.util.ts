const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** "24 Oct 2026, 10:32" for a yyyy-mm-ddThh:mm stamp. */
export function formatStamp(stamp: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(stamp);
  if (!m) return stamp || '—';
  const date = `${m[3]} ${MONTHS[Number(m[2]) - 1]} ${m[1]}`;
  return m[4] ? `${date}, ${m[4]}:${m[5]}` : date;
}

/** 1,281,640 → "1.28M", 12,450 → "12.4K". */
export function compactNumber(n: number): string {
  if (n >= 1_000_000) return `${(Math.floor(n / 10_000) / 100).toFixed(2)}M`;
  if (n >= 10_000) return `${(Math.floor(n / 100) / 10).toFixed(1)}K`;
  return n.toLocaleString('en-US');
}

/** One CSV cell: quotes escaped, formula-looking values neutralised with a leading '. */
function csvCell(value: unknown): string {
  let s = value === null || value === undefined ? '' : String(value);
  if (/^[=+\-@]/.test(s)) s = `'${s}`;
  return /[",\r\n]/.test(s) || s !== s.trim() ? `"${s.replace(/"/g, '""')}"` : s;
}

export function toCsv(header: string[], rows: unknown[][]): string {
  return [header, ...rows].map((r) => r.map(csvCell).join(',')).join('\r\n');
}

export function downloadFile(fileName: string, content: string, type = 'text/csv;charset=utf-8'): void {
  const blob = new Blob([type.startsWith('text/csv') ? '﻿' + content : content], { type });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
