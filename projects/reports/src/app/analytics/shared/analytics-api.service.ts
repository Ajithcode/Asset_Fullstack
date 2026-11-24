import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { CURRENT_USER, RA_PERIODS } from './analytics-common';

// Report Analytics service (port 5480): the analytics pages that share the options / filters / views / overview / export
// endpoint shape. Paths: {reportAnalyticsApiUrl}api/{group}/...

export type AnalyticsGroup = 'asset-performance' | 'maintenance-analytics' | 'inspection-analytics' | 'production-analytics';

export type QueryValue = string | number | boolean | null | undefined;
export type AnalyticsQuery = Record<string, QueryValue>;

/** KpiWidgetData: a headline figure with its previous-period value. */
export interface KpiDto {
  value: number | null;
  format: string;
  previousValue: number | null;
  change: number | null;
  /** 'percent' (relative change) or 'points' */
  changeUnit: string;
  trend: string;
  comparisonLabel: string;
}

/** PerformanceSeries: monthly categories ("Jan 2026") with one or more named series. */
export interface SeriesDto {
  categories: string[];
  series: { name: string; values: (number | null)[] }[];
}

export interface CountBreakdownDto {
  total: number;
  items: { label: string; count: number; percentage: number }[];
}

export interface DowntimeCauseDto {
  totalHours: number;
  causes: { cause: string; hours: number; percentage: number }[];
  trend: SeriesDto;
}

export interface ReliabilityDto {
  mtbf: KpiDto;
  mttr: KpiDto;
  trend: SeriesDto;
}

export interface InsightAssetDto {
  id: string;
  assetId: string | null;
  assetName: string | null;
  location: string | null;
  healthScore: number | null;
  reasons: string[];
}

/** DashboardPeriodResponse */
export interface PeriodDto {
  preset: string;
  from: string;
  to: string;
  timeZone: string;
  fromUtc: string;
  toUtc: string;
  previousFromUtc: string;
  previousToUtc: string;
}

export interface FilterOptionDto {
  value: string;
  label: string;
}

/** A saved view; `filters` holds the page's filter fields plus the date range. */
export interface AnalyticsViewDto {
  viewId: string;
  viewName: string;
  isBuiltIn: boolean;
  isDefault: boolean;
  filters: Record<string, string | null> & { datePreset: string; dateFrom: string | null; dateTo: string | null; timeZone: string };
  updatedAt: string | null;
}

/** Date filter of the analytics endpoints: a preset, or from / to (yyyy-MM-dd) which the API reads as Custom. */
export interface DateQuery {
  datePreset?: string;
  from?: string;
  to?: string;
}

function ymd(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Maps the page Period filter (RA_PERIODS) to the API's date filter. */
export function periodQuery(period: string, today = new Date()): DateQuery {
  switch (RA_PERIODS.indexOf(period)) {
    case 1:
      return { datePreset: 'This month' };
    case 2: {
      const q = Math.floor(today.getMonth() / 3);
      return { from: ymd(new Date(today.getFullYear(), q * 3 - 3, 1)), to: ymd(new Date(today.getFullYear(), q * 3, 0)) };
    }
    case 3:
      return { datePreset: 'Last 30 days' };
    case 4:
      return { from: ymd(new Date(today.getFullYear(), today.getMonth() - 11, 1)), to: ymd(today) };
    default:
      return { datePreset: 'This year' };
  }
}

/** The RA_PERIODS entry a saved view's date filter stands for ('' = the default, Year to date). */
export function periodFromView(f: { datePreset: string; dateFrom: string | null; dateTo: string | null }): string {
  if (f.dateFrom || f.dateTo) {
    const match = RA_PERIODS.find((p) => {
      const q = periodQuery(p);
      return q.from === f.dateFrom?.slice(0, 10) && q.to === f.dateTo?.slice(0, 10);
    });
    if (match) return match;
  }
  const preset = RA_PERIODS.find((p) => periodQuery(p).datePreset === f.datePreset);
  return preset && preset !== RA_PERIODS[0] ? preset : '';
}

/** A saved view's filters as page filter values: `period` plus each filter field ('' = not set). */
export function viewFilters(v: AnalyticsViewDto): Record<string, string> {
  const out: Record<string, string> = { period: periodFromView(v.filters) };
  for (const [k, value] of Object.entries(v.filters)) {
    if (!['datePreset', 'dateFrom', 'dateTo', 'timeZone'].includes(k)) out[k] = value ?? '';
  }
  return out;
}

/** The period before `p` as a from / to query (for Compare Periods). */
export function previousQuery(p: PeriodDto): DateQuery {
  const from = p.previousFromUtc.slice(0, 10);
  const end = new Date(p.previousToUtc);
  end.setUTCDate(end.getUTCDate() - 1);
  return { from, to: end.toISOString().slice(0, 10) };
}

/** Body date fields of a Save View request for the page Period filter. */
export function viewDates(period: string): { datePreset: string | null; dateFrom: string | null; dateTo: string | null } {
  const q = periodQuery(period);
  return { datePreset: q.datePreset ?? 'Custom', dateFrom: q.from ?? null, dateTo: q.to ?? null };
}

/** '' and "All …" page values mean no filter. */
export function filterValue(value: string | null | undefined): string | undefined {
  const v = (value ?? '').trim();
  return !v || /^all /i.test(v) ? undefined : v;
}

/** "All …" option followed by the API's labels (organization ids are not needed: the API matches names too). */
export function optionLabels(all: string, options: (FilterOptionDto | string)[] | null | undefined): string[] {
  const labels = (options ?? []).map((o) => (typeof o === 'string' ? o : o.label || o.value)).filter(Boolean);
  return [all, ...[...new Set(labels)]];
}

export const num = (v: number | null | undefined): number => (typeof v === 'number' && isFinite(v) ? v : 0);

/** KPI change for the cards: relative % or percentage points, 0 when there is no previous value. */
export const kpiChange = (k: KpiDto | null | undefined): number => Math.round(num(k?.change) * 10) / 10;

/** Values of the named series (nulls as 0); an empty array when the series is missing. */
export function seriesValues(s: SeriesDto | null | undefined, name: string): number[] {
  const line = s?.series.find((x) => x.name === name);
  return line ? line.values.map(num) : [];
}

/** "Jan 2026" → "Jan\n2026" for the chart axis. */
export function monthLabels(s: SeriesDto | null | undefined): string[] {
  return (s?.categories ?? []).map((c) => c.replace(' ', '\n'));
}

/** User-facing message for a failed request. */
export function apiMessage(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;
  if (err.status === 0) return `${fallback} The Report Analytics service is not reachable.`;
  if (err.status === 401) return 'Sign in to continue.';
  if (err.status === 403) return 'You do not have permission for this action.';
  const body = err.error as { message?: string; title?: string; errors?: Record<string, string[]> } | string | null;
  if (typeof body === 'string' && body) return body;
  if (body && typeof body === 'object') {
    const first = body.errors ? Object.values(body.errors).flat()[0] : '';
    return first || body.message || body.title || fallback;
  }
  return fallback;
}

/** One work order raised from an analytics page (POST {maintenanceApiUrl}api/work-orders). */
export interface AnalyticsWorkOrder {
  assetId: string;
  assetName: string;
  title: string;
  workType: string;
  priority: string;
  assignedTechnician?: string;
  dueDate?: string;
  notes?: string;
  site?: string;
  sourceReference?: string;
}

/** Report Analytics API for the asset / maintenance / inspection / production analytics pages. */
@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.reportAnalyticsApiUrl}api/`;
  private readonly workOrdersUrl = `${environment.maintenanceApiUrl}api/work-orders`;

  private params(query: AnalyticsQuery): HttpParams {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    }
    return p;
  }

  /** GET api/{group}/{path} */
  get<T>(group: AnalyticsGroup, path: string, query: AnalyticsQuery = {}): Observable<T> {
    return this.http.get<T>(`${this.base}${group}/${path}`, { params: this.params(query) });
  }

  /** GET api/{group}/filters: the values the filter bar can choose from. */
  filters<T>(group: AnalyticsGroup): Observable<T> {
    return this.get<T>(group, 'filters');
  }

  /** GET api/{group}/overview: every section of the page in one call. */
  overview<T>(group: AnalyticsGroup, query: AnalyticsQuery): Observable<T> {
    return this.get<T>(group, 'overview', query);
  }

  /** GET api/{group}/views: the built-in view first, then the signed-in user's saved views. */
  views(group: AnalyticsGroup): Observable<AnalyticsViewDto[]> {
    return this.http.get<AnalyticsViewDto[]>(`${this.base}${group}/views`);
  }

  /** POST api/{group}/views (Save View). */
  createView(group: AnalyticsGroup, viewName: string, filters: Record<string, string | null>): Observable<AnalyticsViewDto> {
    return this.http.post<AnalyticsViewDto>(`${this.base}${group}/views`, {
      viewName,
      isDefault: false,
      filters,
      tenantId: environment.tenantId,
      clientId: environment.clientId
    });
  }

  /** Names work orders can be assigned to: Maintenance technicians plus technicians already assigned on work orders. */
  assignees(): Observable<string[]> {
    const none = catchError(() => of([]));
    return forkJoin([
      this.http.get<{ name?: string | null }[]>(`${environment.maintenanceApiUrl}api/technicians`).pipe(none),
      this.http.get<{ assignedTechnician?: string | null }[]>(this.workOrdersUrl).pipe(none)
    ]).pipe(
      map(([techs, orders]) => {
        const names = [
          ...(techs as { name?: string | null }[]).map((t) => t.name),
          ...(orders as { assignedTechnician?: string | null }[]).map((w) => w.assignedTechnician)
        ];
        return [...new Set(names.map((n) => (n ?? '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
      })
    );
  }

  /**
   * Creates one Maintenance work order per asset. Codes continue the WO-{year}-nnn sequence of the existing work orders
   * (the Maintenance API expects the caller to supply workOrderId).
   */
  createWorkOrders(orders: AnalyticsWorkOrder[]): Observable<{ workOrderId: string }[]> {
    if (!orders.length) return of([]);
    const user = this.auth.getUserEmail() || CURRENT_USER.email || 'unknown';
    return this.http.get<{ workOrderId?: string | null }[]>(this.workOrdersUrl).pipe(
      switchMap((existing) => {
        const head = `WO-${new Date().getFullYear()}-`;
        let next = (existing ?? []).reduce((m, w) => {
          const id = w.workOrderId ?? '';
          const n = id.startsWith(head) ? parseInt(id.slice(head.length), 10) : NaN;
          return isNaN(n) ? m : Math.max(m, n);
        }, 0);
        return forkJoin(
          orders.map((o) =>
            this.http.post<{ workOrderId: string }>(this.workOrdersUrl, {
              workOrderId: `${head}${String(++next).padStart(3, '0')}`,
              assetId: o.assetId,
              assetName: o.assetName,
              workType: o.workType,
              title: o.title,
              description: o.notes || o.title,
              priority: o.priority,
              assignedTechnician: o.assignedTechnician || null,
              dueDate: o.dueDate || null,
              notes: o.notes || null,
              site: o.site || null,
              source: 'Reports & Analytics',
              sourceReference: o.sourceReference || null,
              status: 'Open',
              category: 'Routine',
              createdBy: user,
              tenantId: environment.tenantId,
              clientId: environment.clientId
            })
          )
        );
      })
    );
  }
}
