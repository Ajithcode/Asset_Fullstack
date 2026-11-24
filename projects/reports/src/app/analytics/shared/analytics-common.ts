import { Directive, OnDestroy, inject } from '@angular/core';
import { ExportFormat, ExportRequest, ExportService, PDF_LIMIT_MESSAGE } from 'shared-ui';
import { ADMIN_LOCATIONS, ADMIN_PEOPLE, CURRENT_USER, ORGANIZATIONS } from './admin-common';

// Entity lists (organizations, locations, categories, people) and figures come from the APIs (not wired yet).

export { ADMIN_PEOPLE as RA_PEOPLE, CURRENT_USER };

export const RA_BASE_PATH = '/reports-analytics';

export const RA_ORGANIZATIONS = ['All organizations', ...ORGANIZATIONS];

/** Sites used across Asset360 (same as Administration > Organization). */
export const RA_LOCATIONS = ['All locations', ...ADMIN_LOCATIONS];

/** Asset categories from Master Data Management. */
export const RA_CATEGORIES: string[] = [];

/** Reporting months for trend charts come with the API data. */
export const RA_MONTHS: string[] = [];
export const RA_MONTH_LABELS: string[] = [];

export const RA_PERIODS = ['Year to date', 'This month', 'Last quarter', 'Last 30 days', 'Last 12 months'];

/** Amounts are reported in UAE Dirham (the group's base currency). */
export const RA_CURRENCY = 'AED';

/** 1860000 → "AED 1.86M" */
export function money(value: number, digits = 2): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (abs >= 1_000_000) return `${sign}${RA_CURRENCY} ${(abs / 1_000_000).toFixed(digits).replace(/\.?0+$/, '')}M`;
  if (abs >= 1_000) return `${sign}${RA_CURRENCY} ${(abs / 1_000).toFixed(abs >= 100_000 ? 0 : 1).replace(/\.0$/, '')}K`;
  return `${sign}${RA_CURRENCY} ${abs.toLocaleString('en-US')}`;
}

export function percent(value: number, digits = 1): string {
  return `${Number(value.toFixed(digits))}%`;
}

interface CollectedTable { file: string; header: string[]; rows: (string | number)[][] }

let exportService: ExportService | null = null;
let collector: CollectedTable[] | null = null;
let nextExportFormat: ExportFormat = 'csv';

/** Runs `fn` (a handler that calls exportTable) so its table downloads in `format`; plain calls export CSV. */
export function withExportFormat(format: ExportFormat, fn: () => void): void {
  nextExportFormat = format;
  try {
    fn();
  } finally {
    nextExportFormat = 'csv';
  }
}

/** Format for a widget menu entry: 'Export PDF' → pdf, 'Export CSV' / 'Export' → csv. */
export function formatOfAction(action: string): ExportFormat {
  return /pdf/i.test(action) ? 'pdf' : 'csv';
}

export function isExportAction(action: string): boolean {
  return action === 'Export' || action === 'Export CSV' || action === 'Export PDF';
}

/**
 * Exports a table through the shared ExportService (CSV, or PDF inside withExportFormat). `fileName` is the file base
 * (a trailing .csv is ignored); the title is derived from it. Errors show a small toast.
 */
export function exportTable(fileName: string, header: string[], rows: (string | number)[][], format: ExportFormat = nextExportFormat): void {
  if (collector) {
    collector.push({ file: fileName, header, rows });
    return;
  }
  const svc = exportService;
  if (!svc) return;
  const fileBase = fileName.replace(/\.(csv|pdf)$/i, '');
  const title = fileBase.replace(/[-_]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  svc
    .run(format, { title, fileBase, columns: header.map((label, i) => ({ label, value: (r: (string | number)[]) => r[i] })), rows: () => rows })
    .then((out) => {
      if (out.truncated) toast(PDF_LIMIT_MESSAGE);
    })
    .catch((err: Error) => toast(err?.message || 'Export failed.', true));
}

/**
 * ExportRequest for a page-level Export built from the page's existing per-table export handlers: `run` calls them
 * (they call exportTable, which is collected instead of downloaded). One table is exported as is; several tables become one
 * table with a Section column, each section's column names as a 'Header' row.
 */
export function tablesRequest(opts: { title: string; fileBase: string; filters?: Record<string, string | number | null | undefined> }, run: () => void): ExportRequest<(string | number)[]> {
  const tables: CollectedTable[] = [];
  collector = tables;
  try {
    run();
  } finally {
    collector = null;
  }
  const humanize = (f: string) => f.replace(/\.(csv|pdf)$/i, '').replace(/[-_]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  if (tables.length === 1) {
    const t = tables[0];
    return { ...opts, columns: t.header.map((label, i) => ({ label, value: (r) => r[i] })), rows: () => t.rows };
  }
  const width = Math.max(1, ...tables.map((t) => t.header.length));
  return {
    ...opts,
    columns: [
      { label: 'Section', value: (r) => r[0] },
      { label: 'Row type', value: (r) => r[1] },
      ...Array.from({ length: width }, (_, i) => ({ label: `Value ${i + 1}`, value: (r: (string | number)[]) => r[i + 2] }))
    ],
    rows: () => tables.flatMap((t) => [[humanize(t.file), 'Header', ...t.header], ...t.rows.map((r) => [humanize(t.file), 'Data', ...r])])
  };
}

/** ExportRequest for a plain table (header + rows), e.g. a list dialog's Export menu: `[source]="tableSource(file, header, () => rows)"`. */
export function tableSource(fileName: string, header: string[], rows: () => (string | number)[][], filters?: Record<string, string | number | null | undefined>): () => ExportRequest<(string | number)[]> {
  return () => {
    const fileBase = fileName.replace(/\.(csv|pdf)$/i, '');
    return {
      title: fileBase.replace(/[-_]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase()),
      fileBase,
      columns: header.map((label, i) => ({ label, value: (r: (string | number)[]) => r[i] })),
      filters,
      rows
    };
  };
}

function toast(message: string, bad = false): void {
  const el = document.createElement('div');
  el.textContent = message;
  el.setAttribute('role', 'status');
  el.style.cssText = `position:fixed;right:20px;bottom:24px;z-index:4000;max-width:380px;padding:11px 16px;border-radius:10px;color:#fff;font-size:13px;background:${bad ? '#b91c1c' : '#1f2937'}`;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 4000);
}

/** Called by the base page / widget cards so exportTable can reach the shared ExportService. */
export function bindExporter(svc: ExportService): ExportService {
  exportService = svc;
  return svc;
}

/**
 * Base for the Reports & Analytics pages: "Updated n min ago" + refresh, saved views and the toast.
 * Pages add their own filter fields.
 */
@Directive()
export abstract class AnalyticsPage implements OnDestroy {
  protected readonly exporter = bindExporter(inject(ExportService));
  readonly basePath = RA_BASE_PATH;
  organizations: string[] = RA_ORGANIZATIONS;
  locations: string[] = RA_LOCATIONS;
  readonly periods = RA_PERIODS;
  /** Filter values start empty ('' = no filter; an empty period reads as the default, Year to date). */
  period = '';
  organization = '';
  location = '';

  /** Period used for captions and exports: the selected one, or the default when none is picked. */
  get periodLabel(): string {
    return this.period || RA_PERIODS[0];
  }

  get organizationLabel(): string {
    return this.organization || RA_ORGANIZATIONS[0];
  }

  get locationLabel(): string {
    return this.location || RA_LOCATIONS[0];
  }

  /** Filters printed in the PDF header of this page's exports. */
  protected exportFilters(): Record<string, string> {
    return { Period: this.periodLabel, Organization: this.organizationLabel, Location: this.locationLabel };
  }

  /** Minutes since the data was refreshed */
  updatedMinutes = 0;
  refreshing = false;
  toast: string | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  get updatedText(): string {
    return this.updatedMinutes === 0 ? 'Updated just now' : `Updated ${this.updatedMinutes} min ago`;
  }

  /** Pages reload their data from the reporting API here once it exists. */
  refresh(): void {
    if (this.refreshing) return;
    this.updatedMinutes = 0;
  }

  saveView(name: string): void {
    this.notify(`View “${name}” saved with the current filters.`);
  }

  notify(message: string): void {
    this.toast = message;
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = null), 3200);
  }

  ngOnDestroy(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }
}
