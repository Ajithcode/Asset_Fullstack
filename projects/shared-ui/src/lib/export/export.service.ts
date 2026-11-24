import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, expand, firstValueFrom, isObservable, map, reduce, EMPTY } from 'rxjs';
import { AuthService } from '../auth/auth.service';

export type ExportFormat = 'csv' | 'pdf';
export type ExportCell = string | number | boolean | Date | null | undefined;

/** One exported column: `key` reads row[key] unless `value` is given. */
export interface ExportColumn<T = any> {
  label: string;
  key?: string;
  value?: (row: T) => ExportCell;
  /** PDF only: relative width weight (default: auto-fit). */
  width?: number;
  align?: 'left' | 'right' | 'center';
}

/**
 * What one Export button exports. `rows` runs when the user picks CSV / PDF, so it reads the page's CURRENT filters and sort
 * and must return ALL of the matching rows (use ExportService.allPages for paged APIs), not just the visible page.
 */
export interface ExportRequest<T = any> {
  /** Page title (PDF header), e.g. "Asset List". */
  title: string;
  /** File name base: <fileBase>-<yyyy-MM-dd>.csv / .pdf, e.g. "asset-list". */
  fileBase: string;
  columns: ExportColumn<T>[];
  /** Applied filters, printed in the PDF header: { Status: 'Active', Search: 'pump' } or plain strings. */
  filters?: Record<string, string | number | null | undefined> | string[];
  rows: () => Observable<T[]> | Promise<T[]> | T[];
}

interface LogoInfo { data: string; type: 'JPEG' | 'PNG'; ratio: number }

export const PDF_ROW_LIMIT = 5000;
export const PDF_LIMIT_MESSAGE = 'PDF shows the first 5,000 rows – use CSV for the full data';

/** Shared export: CSV (UTF-8 with BOM) and PDF (A4 landscape) for every page's Export menu. */
@Injectable({ providedIn: 'root' })
export class ExportService {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private logo?: Promise<LogoInfo | null>;

  /** Every page of a paged API: loader(page, pageSize) → { items, total } | items[] (page 1-based). */
  allPages<T>(loader: (page: number, size: number) => Observable<{ items?: T[]; total?: number; totalPages?: number } | T[]>, size = 200, maxPages = 500): Observable<T[]> {
    return loader(1, size).pipe(
      expand((res, i) => {
        if (Array.isArray(res)) return EMPTY;
        const items = res?.items ?? [];
        const more = res?.totalPages ? i + 1 < res.totalPages : res?.total !== undefined ? (i + 1) * size < res.total : items.length === size;
        return more && i + 1 < maxPages ? loader(i + 2, size) : EMPTY;
      }),
      map((res) => (Array.isArray(res) ? res : res?.items ?? [])),
      reduce((all, rows) => all.concat(rows), [] as T[])
    );
  }

  /** Convenience for GET list endpoints with page / pageSize query params. */
  fetchPages<T>(url: string, params: HttpParams | Record<string, string | number | boolean>, size = 200): Observable<T[]> {
    const base = params instanceof HttpParams ? params : new HttpParams({ fromObject: Object.fromEntries(Object.entries(params).map(([k, v]) => [k, String(v)])) });
    return this.allPages<T>((page, s) => this.http.get<{ items?: T[]; total?: number; totalPages?: number } | T[]>(url, { params: base.set('page', page).set('pageSize', s) }), size);
  }

  /** Runs one export. Resolves with { rows, truncated } and rejects with an Error(message) the menu shows as a toast. */
  async run<T>(format: ExportFormat, req: ExportRequest<T>): Promise<{ rows: number; truncated: boolean }> {
    const src = req.rows();
    const all = Array.isArray(src) ? src : isObservable(src) ? await firstValueFrom(src.pipe(map((r) => r ?? []))) : await src;
    const rows = all ?? [];
    if (format === 'csv') {
      this.save(new Blob(['﻿', this.toCsv(rows, req.columns)], { type: 'text/csv;charset=utf-8' }), `${req.fileBase}-${this.today()}.csv`);
      return { rows: rows.length, truncated: false };
    }
    const truncated = rows.length > PDF_ROW_LIMIT;
    await this.toPdf(truncated ? rows.slice(0, PDF_ROW_LIMIT) : rows, req, rows.length);
    return { rows: Math.min(rows.length, PDF_ROW_LIMIT), truncated };
  }

  today(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  cellText(v: ExportCell): string {
    if (v === null || v === undefined) return '';
    if (v instanceof Date) return Number.isNaN(v.getTime()) ? '' : v.toISOString().replace('T', ' ').slice(0, 19);
    if (typeof v === 'boolean') return v ? 'Yes' : 'No';
    return String(v);
  }

  private read<T>(row: T, c: ExportColumn<T>): ExportCell {
    return c.value ? c.value(row) : c.key ? ((row as Record<string, ExportCell>)?.[c.key] ?? null) : null;
  }

  private toCsv<T>(rows: T[], cols: ExportColumn<T>[]): string {
    const esc = (v: ExportCell): string => {
      let t = this.cellText(v);
      // a text cell that starts like a formula must not run in Excel
      if (typeof v === 'string' && /^[=+\-@\t\r]/.test(t) && !/^[+-]?\d+([.,]\d+)?$/.test(t)) t = `'${t}`;
      return /[",\r\n]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t;
    };
    const lines = [cols.map((c) => esc(c.label)).join(',')];
    for (const r of rows) lines.push(cols.map((c) => esc(this.read(r, c))).join(','));
    return lines.join('\r\n') + '\r\n';
  }

  private async toPdf<T>(rows: T[], req: ExportRequest<T>, total: number): Promise<void> {
    const [{ jsPDF }, autoTableModule] = await Promise.all([import('jspdf'), import('jspdf-autotable')]);
    const autoTable = (autoTableModule as unknown as { default: (doc: unknown, o: unknown) => void }).default ?? (autoTableModule as unknown as { autoTable: (doc: unknown, o: unknown) => void }).autoTable;
    const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
    const logo = await this.loadLogo();
    const by = this.auth.getUser()?.userName || this.auth.getUserEmail() || 'unknown user';
    const stamp = new Date().toLocaleString();
    const filters = Array.isArray(req.filters)
      ? req.filters.filter(Boolean).join(' · ')
      : Object.entries(req.filters ?? {}).filter(([, v]) => v !== null && v !== undefined && String(v) !== '').map(([k, v]) => `${k}: ${v}`).join(' · ');
    const margin = 10;
    const pageW = doc.internal.pageSize.getWidth();
    const pageH = doc.internal.pageSize.getHeight();
    const headerH = 24;

    const header = () => {
      if (logo) doc.addImage(logo.data, logo.type, margin, 6, 30, 30 * logo.ratio);
      doc.setTextColor(30, 20, 70).setFont('helvetica', 'bold').setFontSize(14).text(req.title, margin + 36, 11);
      doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(90, 90, 100);
      doc.text(doc.splitTextToSize(`Filters: ${filters || 'none'}`, pageW - margin * 2 - 36)[0], margin + 36, 16);
      doc.text(`Generated by ${by} on ${stamp} · ${total.toLocaleString()} row${total === 1 ? '' : 's'}${total > rows.length ? ` (first ${rows.length.toLocaleString()} shown)` : ''}`, margin + 36, 20.5);
    };

    const body = rows.map((r) => req.columns.map((c) => this.cellText(this.read(r, c))));
    const weights = req.columns.map((c) => c.width);
    const usable = pageW - margin * 2;
    const sum = weights.reduce<number>((a, w) => a + (w ?? 0), 0);
    const columnStyles: Record<number, unknown> = {};
    req.columns.forEach((c, i) => {
      const s: Record<string, unknown> = { halign: c.align ?? 'left' };
      if (c.width && sum) s['cellWidth'] = (usable * c.width) / sum;
      columnStyles[i] = s;
    });
    autoTable(doc, {
      head: [req.columns.map((c) => c.label)],
      body: body.length ? body : [[{ content: 'No data available', colSpan: req.columns.length, styles: { halign: 'center' } }]],
      startY: headerH + 6,
      margin: { top: headerH + 6, left: margin, right: margin, bottom: 12 },
      tableWidth: 'auto',
      theme: 'grid',
      showHead: 'everyPage',
      styles: { font: 'helvetica', fontSize: 7, cellPadding: 1.4, overflow: 'linebreak', valign: 'top', lineColor: [225, 225, 235], lineWidth: 0.1 },
      headStyles: { fillColor: [91, 33, 182], textColor: 255, fontStyle: 'bold' },
      alternateRowStyles: { fillColor: [248, 247, 253] },
      columnStyles,
      didDrawPage: header
    });
    const pages = doc.getNumberOfPages();
    for (let p = 1; p <= pages; p++) {
      doc.setPage(p);
      doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(110, 110, 120);
      doc.text(`Page ${p} of ${pages}`, pageW - margin, pageH - 5, { align: 'right' });
      doc.text('Asset360', margin, pageH - 5);
    }
    this.save(doc.output('blob'), `${req.fileBase}-${this.today()}.pdf`);
  }

  /** The Asset360 logo (same file as the header), cached; null when it cannot be loaded (the PDF then has no logo). */
  private loadLogo(): Promise<LogoInfo | null> {
    this.logo ??= (async () => {
      try {
        const res = await fetch('/purple360-logo.jpeg', { cache: 'force-cache' });
        if (!res.ok) return null;
        const blob = await res.blob();
        const data = await new Promise<string>((ok, bad) => {
          const r = new FileReader();
          r.onload = () => ok(String(r.result));
          r.onerror = () => bad(r.error);
          r.readAsDataURL(blob);
        });
        const img = await new Promise<HTMLImageElement>((ok, bad) => {
          const i = new Image();
          i.onload = () => ok(i);
          i.onerror = bad;
          i.src = data;
        });
        return { data, type: 'JPEG' as const, ratio: img.naturalHeight / img.naturalWidth };
      } catch {
        return null;
      }
    })();
    return this.logo;
  }

  private save(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }
}
