import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { AuthService, ExportService, environment } from 'shared-ui';
import { CURRENT_USER, money, percent } from '../shared/analytics-common';
import {
  ReportFrequency, ReportModule, ReportPeriod, ReportSchedule, ReportStatus, SR_FORMATS, SR_STATUSES, SR_TODAY, StandardReport,
  defaultReportPeriod
} from './standard-reports.data';
import { WEEKDAYS, nextRun } from './standard-reports.util';

export interface PreviewRow {
  label: string;
  value: number;
  display: string;
  /** Share of the total (count / money / hours), or the gap to the average (percent) */
  share: string;
}

/** Query parameters of GET {reportAnalyticsApiUrl}api/standard-reports. */
export interface StandardReportQuery {
  search?: string;
  category?: string;
  frequency?: string;
  status?: string;
  periodFrom?: string;
  periodTo?: string;
  timeZone?: string;
  includeTime?: boolean;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

/** Request body of POST {reportAnalyticsApiUrl}api/standard-reports. */
export interface CreateStandardReportRequest {
  reportName: string;
  description: string;
  category: string;
  reportPeriodFrom: string | null;
  reportPeriodTo: string | null;
  timeZone: string;
  includeTime: boolean;
  frequency: string;
  nextScheduledOn: string | null;
  reviewDueOn: string | null;
  status: string;
  createdBy: string;
  tenantId: string;
  clientId: string;
  reportType: string;
}

/** Request body of PUT {reportAnalyticsApiUrl}api/standard-reports/{reportId}/schedule. */
export interface ScheduleStandardReportRequest {
  frequency: string;
  nextScheduledOn: string | null;
  timeZone: string;
  updatedBy: string;
  days: (string | number)[];
  time: string;
  format: string;
  recipients: string[];
}

/** What the "New report" dialog collects. Dates are wall-clock 'YYYY-MM-DD' / 'YYYY-MM-DDTHH:mm'. */
export interface NewStandardReport {
  name: string;
  description: string;
  module: ReportModule;
  reportType: string;
  period: ReportPeriod;
  frequency: ReportFrequency;
  status: ReportStatus;
  nextScheduled: string | null;
  reviewDue: string | null;
}

/** Report record as returned by the API (the response schema is not published, so every field is optional). */
type StandardReportDto = Record<string, unknown>;

/** GET /api/standard-reports/options (the parts the page uses). */
export interface StandardReportOptions {
  categories: string[];
  reportTypes: string[];
}

/** GET /api/standard-reports/summary. */
export interface StandardReportSummary {
  totalReports: number;
  withDateFilters: number;
  scheduled: number;
  updatedThisMonth: number;
}

/** Marker counts on one calendar day (GET /api/standard-reports/calendar). */
export interface CalendarMarks {
  generated: number;
  scheduled: number;
  review: number;
}

/** One zone visit of an Asset Movement report (details data.dates[].records[]). zoneName is null when the gateway is not plotted. */
export interface MovementRecord {
  assetId: string;
  assetName: string | null;
  category: string | null;
  subCategory: string | null;
  tagId: string;
  zoneName: string | null;
  deviceId: string;
  zoneIn: string;
  zoneOut: string;
  /** Zone in / out in the report's time zone (yyyy-MM-dd HH:mm:ss) */
  zoneInLocal: string;
  zoneOutLocal: string;
  timeSpentSeconds: number;
  /** HH:mm:ss */
  timeSpent: string;
  readingCount: number;
}

interface MovementData {
  totalRecords: number;
  truncated: boolean;
  dates: { date: string; recordCount: number; records: MovementRecord[] }[];
}

/** GET /api/standard-reports/{id}/details mapped for the viewer. */
export interface StandardReportDetails {
  report: StandardReport;
  /** False for a report type without data behind it */
  hasData: boolean;
  /** Visits per date */
  rows: PreviewRow[];
  records: MovementRecord[];
  truncated: boolean;
}

const FREQUENCIES: ReportFrequency[] = ['Daily', 'Weekly', 'Monthly', 'Quarterly', 'On demand'];
const SCHEDULE_FREQUENCIES = FREQUENCIES.filter((f) => f !== 'On demand');

/** Table column key → API sortBy field. */
export const SR_SORT_FIELDS: Record<string, string> = {
  name: 'reportName',
  module: 'category',
  period: 'reportPeriod',
  generatedOn: 'generatedOn',
  nextScheduled: 'nextScheduled',
  frequency: 'frequency',
  status: 'status'
};

/** Standard reports loaded from the Report Analytics API. */
@Injectable({ providedIn: 'root' })
export class StandardReportsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly exporter = inject(ExportService);
  private readonly apiUrl = `${environment.reportAnalyticsApiUrl}api/standard-reports`;

  /** Reports on the current page. */
  reports: StandardReport[] = [];
  /** Total matching reports on the server (all pages). */
  totalCount = 0;
  /** The applied reporting period (sent as periodFrom / periodTo). */
  period: ReportPeriod = defaultReportPeriod();

  get today(): string {
    return SR_TODAY;
  }

  find(id: string): StandardReport | undefined {
    return this.reports.find((r) => r.id === id);
  }

  /** GET /api/standard-reports with filters, sorting and paging. */
  load(query: StandardReportQuery): Observable<StandardReport[]> {
    let params = new HttpParams().set('tenantId', environment.tenantId).set('clientId', environment.clientId);
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue;
      params = params.set(key, String(value));
    }
    return this.http.get<unknown>(this.apiUrl, { params }).pipe(
      map((body) => {
        const { items, total } = unwrapList(body);
        return { reports: items.map((dto) => this.toReport(dto)), total };
      }),
      tap(({ reports, total }) => {
        this.reports = reports;
        this.totalCount = total ?? reports.length;
      }),
      map(({ reports }) => reports)
    );
  }

  /**
   * Category / report type dropdowns: GET /api/standard-reports/options. The API only accepts its own category list
   * (create, update and the list filter reject other names), so the dropdowns use it rather than the asset Category master.
   */
  options(): Observable<StandardReportOptions> {
    return this.http.get<Partial<StandardReportOptions>>(`${this.apiUrl}/options`).pipe(
      map((o) => ({ categories: o?.categories ?? [], reportTypes: o?.reportTypes ?? [] })),
      catchError((err) => {
        console.warn('[standard-reports] options failed', err);
        return of({ categories: [], reportTypes: [] });
      })
    );
  }

  /** GET /api/standard-reports/summary: the four tiles, across every report (not just the current page). */
  summary(timeZone: string): Observable<StandardReportSummary> {
    const params = this.scope().set('timeZone', timeZone);
    return this.http.get<Partial<StandardReportSummary>>(`${this.apiUrl}/summary`, { params }).pipe(
      map((s) => ({
        totalReports: Number(s?.totalReports ?? 0),
        withDateFilters: Number(s?.withDateFilters ?? 0),
        scheduled: Number(s?.scheduled ?? 0),
        updatedThisMonth: Number(s?.updatedThisMonth ?? 0)
      }))
    );
  }

  /** GET /api/standard-reports/calendar for a month (1-12): marker counts keyed by 'YYYY-MM-DD'. */
  calendar(year: number, month: number, timeZone: string): Observable<Record<string, CalendarMarks>> {
    const params = this.scope().set('year', year).set('month', month).set('timeZone', timeZone);
    return this.http.get<{ days?: { date: string; generated: number; scheduled: number; reviewDue: number }[] }>(`${this.apiUrl}/calendar`, { params }).pipe(
      map((body) => {
        const marks: Record<string, CalendarMarks> = {};
        for (const d of body?.days ?? []) {
          marks[String(d.date).slice(0, 10)] = { generated: d.generated ?? 0, scheduled: d.scheduled ?? 0, review: d.reviewDue ?? 0 };
        }
        return marks;
      })
    );
  }

  /** PUT /api/standard-reports/report-period: saves the period on the given reports (Apply Dates). */
  applyPeriodTo(reportIds: string[], period: ReportPeriod): Observable<{ updatedCount: number; notFound: string[] }> {
    return this.http.put<{ updatedCount: number; notFound: string[] }>(`${this.apiUrl}/report-period`, {
      reportIds,
      reportPeriodFrom: toApiDate(period.start, period.startTime ?? '00:00'),
      reportPeriodTo: toApiDate(period.end, period.endTime ?? '23:59'),
      timeZone: period.timeZone,
      includeTime: !!(period.startTime && period.endTime),
      updatedBy: this.userEmail()
    });
  }

  /** GET /api/standard-reports/{id}/details: the report plus its rows for the saved period (rows only for data-backed types). */
  details(report: StandardReport): Observable<StandardReportDetails> {
    return this.http.get<{ report?: StandardReportDto; data?: MovementData | null }>(`${this.apiUrl}/${encodeURIComponent(report.id)}/details`).pipe(
      map((body) => {
        const fresh = body?.report ? this.toReport(body.report) : report;
        const data = body?.data ?? null;
        const total = data?.totalRecords ?? 0;
        const rows: PreviewRow[] = (data?.dates ?? []).map((d) => ({
          label: d.date,
          value: d.recordCount,
          display: d.recordCount.toLocaleString('en-US'),
          share: total ? `${((d.recordCount / total) * 100).toFixed(1)}%` : '0%'
        }));
        const records = (data?.dates ?? []).flatMap((d) => d.records ?? []).sort((x, y) => x.zoneIn.localeCompare(y.zoneIn));
        const withPreview: StandardReport = {
          ...fresh,
          preview: { dimension: 'date', measure: data ? 'Visits' : fresh.preview.measure, unit: 'count', total }
        };
        return { report: withPreview, hasData: !!data, rows, records, truncated: !!data?.truncated };
      })
    );
  }

  /** POST /api/standard-reports/{id}/generate: records a run (Generated On = now, status Ready). */
  generate(report: StandardReport): Observable<StandardReport> {
    return this.http.post<StandardReportDto>(`${this.apiUrl}/${encodeURIComponent(report.id)}/generate`, {}).pipe(
      map((dto) => (dto && typeof dto === 'object' ? this.toReport(dto) : report)),
      tap((updated) => this.update(report.id, () => updated))
    );
  }

  /** PUT /api/standard-reports/{id}: saves the edited definition. */
  edit(report: StandardReport, form: NewStandardReport): Observable<StandardReport> {
    const { createdBy: _c, tenantId: _t, clientId: _cl, ...body } = this.toRequest(form);
    return this.http.put<StandardReportDto>(`${this.apiUrl}/${encodeURIComponent(report.id)}`, { ...body, updatedBy: this.userEmail() }).pipe(
      map((dto) => (dto && typeof dto === 'object' ? this.toReport(dto) : report)),
      tap((updated) => this.update(report.id, () => updated))
    );
  }

  /** DELETE /api/standard-reports/{id}. */
  remove(report: StandardReport): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(report.id)}`);
  }

  /** Every report matching the query (all pages, same filters and sort as the table) for CSV / PDF export. */
  loadAll(query: StandardReportQuery): Observable<StandardReport[]> {
    return this.exporter.allPages<StandardReport>((page, size) => {
      let params = new HttpParams().set('tenantId', environment.tenantId).set('clientId', environment.clientId);
      for (const [key, value] of Object.entries({ ...query, page, pageSize: size })) {
        if (value === undefined || value === null || value === '') continue;
        params = params.set(key, String(value));
      }
      return this.http.get<unknown>(this.apiUrl, { params }).pipe(
        map((body) => {
          const { items, total } = unwrapList(body);
          return { items: items.map((dto) => this.toReport(dto)), total: total ?? undefined };
        })
      );
    }, 100);
  }

  private scope(): HttpParams {
    return new HttpParams().set('tenantId', environment.tenantId).set('clientId', environment.clientId);
  }

  private userEmail(): string {
    return this.auth.getUserEmail() || CURRENT_USER.email || 'unknown';
  }

  /** POST /api/standard-reports; emits the created report. */
  create(form: NewStandardReport): Observable<StandardReport> {
    const request = this.toRequest(form);
    return this.http.post<StandardReportDto>(this.apiUrl, request).pipe(
      map((dto) => this.toReport({ ...request, ...(dto && typeof dto === 'object' ? dto : {}) }))
    );
  }

  private toRequest(form: NewStandardReport): CreateStandardReportRequest {
    const p = form.period;
    const includeTime = !!(p.startTime && p.endTime);
    return {
      reportName: form.name.trim(),
      description: form.description.trim(),
      category: form.module,
      reportPeriodFrom: toApiDate(p.start, p.startTime ?? '00:00'),
      reportPeriodTo: toApiDate(p.end, p.endTime ?? '23:59'),
      timeZone: p.timeZone,
      includeTime,
      frequency: form.frequency,
      nextScheduledOn: form.nextScheduled ? toApiDate(form.nextScheduled.slice(0, 10), form.nextScheduled.slice(11, 16) || '00:00') : null,
      reviewDueOn: form.reviewDue ? toApiDate(form.reviewDue, '00:00') : null,
      status: form.status,
      createdBy: this.userEmail(),
      tenantId: environment.tenantId,
      clientId: environment.clientId,
      reportType: form.reportType.trim()
    };
  }

  /** Remember the applied reporting period; the page reloads with it. */
  applyPeriod(period: ReportPeriod): void {
    this.period = { ...period };
  }

  /**
   * PUT /api/standard-reports/{reportId}/schedule; emits the updated report.
   * days: weekday name for Weekly, day of month for Monthly / Quarterly, empty for Daily.
   */
  saveSchedule(report: StandardReport, schedule: ReportSchedule): Observable<StandardReport> {
    const next = nextRun(schedule, SR_TODAY);
    const request: ScheduleStandardReportRequest = {
      frequency: schedule.frequency,
      nextScheduledOn: toApiDate(next, schedule.time),
      timeZone: report.period.timeZone || defaultReportPeriod().timeZone,
      updatedBy: this.userEmail(),
      days: schedule.frequency === 'Daily' ? [] : schedule.frequency === 'Weekly' ? [WEEKDAYS[schedule.day]] : [schedule.day],
      time: schedule.time,
      format: schedule.format,
      recipients: [...schedule.recipients]
    };
    return this.http.put<unknown>(`${this.apiUrl}/${encodeURIComponent(report.id)}/schedule`, request).pipe(
      map((body) => {
        const dto = body && typeof body === 'object' && !Array.isArray(body) ? (body as StandardReportDto) : null;
        // Keep the local row when the API answers without the report (200 with no body / a message only)
        const fromApi = dto && (dto['reportId'] ?? dto['id'] ?? dto['reportName']) ? this.toReport(dto) : null;
        const updated: StandardReport = {
          ...report,
          ...(fromApi ?? {}),
          id: report.id,
          schedule: { ...schedule, recipients: [...schedule.recipients] },
          frequency: fromApi?.frequency ?? schedule.frequency,
          nextScheduled: fromApi?.nextScheduled ?? `${next}T${schedule.time}`,
          status: fromApi?.status ?? (report.status === 'Needs review' ? report.status : 'Scheduled'),
          updatedOn: SR_TODAY
        };
        return updated;
      }),
      tap((updated) => this.update(report.id, () => updated))
    );
  }

  formatValue(report: StandardReport, value: number): string {
    switch (report.preview.unit) {
      case 'money':
        return money(value);
      case 'percent':
        return percent(value);
      case 'hours':
        return `${value.toLocaleString('en-US')} h`;
      default:
        return value.toLocaleString('en-US');
    }
  }

  private update(id: string, change: (r: StandardReport) => StandardReport): StandardReport | undefined {
    let updated: StandardReport | undefined;
    this.reports = this.reports.map((r) => {
      if (r.id !== id) return r;
      updated = change(r);
      return updated;
    });
    return updated;
  }

  /** Saved schedule from the report record (schedule object or top-level fields); null when it has none. */
  private toSchedule(dto: StandardReportDto): ReportSchedule | null {
    const src = (dto['schedule'] && typeof dto['schedule'] === 'object' ? dto['schedule'] : dto) as StandardReportDto;
    const recipients = Array.isArray(src['recipients']) ? (src['recipients'] as unknown[]).filter((x): x is string => typeof x === 'string') : [];
    const time = str(src, 'time', 'scheduleTime').slice(0, 5);
    const frequency = SCHEDULE_FREQUENCIES.find((f) => f.toLowerCase() === str(src, 'frequency').toLowerCase());
    if (!frequency || !time || !recipients.length) return null;
    const first = Array.isArray(src['days']) ? (src['days'] as unknown[])[0] : undefined;
    let day = 1;
    if (frequency === 'Weekly') {
      const i = typeof first === 'number' ? first : WEEKDAYS.findIndex((w) => w.toLowerCase() === String(first ?? '').toLowerCase());
      day = i >= 0 && i <= 6 ? i : 1;
    } else if (frequency !== 'Daily') {
      const n = Number(first);
      day = Number.isInteger(n) && n >= 1 && n <= 28 ? n : 1;
    }
    const format = SR_FORMATS.find((f) => f.toLowerCase() === str(src, 'format').toLowerCase()) ?? 'PDF';
    return { frequency, day, time, recipients, format };
  }

  private toReport(dto: StandardReportDto): StandardReport {
    const from = str(dto, 'reportPeriodFrom', 'periodFrom');
    const to = str(dto, 'reportPeriodTo', 'periodTo');
    const includeTime = dto['includeTime'] === true;
    const updatedOn = str(dto, 'updatedAt', 'updatedOn', 'modifiedAt', 'createdAt', 'createdOn');
    return {
      id: str(dto, 'reportId', 'id', 'standardReportId'),
      name: str(dto, 'reportName', 'name') || 'Untitled report',
      module: str(dto, 'category', 'categoryName') || '—',
      description: str(dto, 'description'),
      reportType: str(dto, 'reportType'),
      owner: str(dto, 'owner', 'createdBy') || '—',
      period: {
        start: (from || this.period.start).slice(0, 10),
        end: (to || this.period.end).slice(0, 10),
        startTime: includeTime && from.length > 10 ? from.slice(11, 16) : null,
        endTime: includeTime && to.length > 10 ? to.slice(11, 16) : null,
        timeZone: str(dto, 'timeZone') || this.period.timeZone
      },
      generatedOn: wallClock(str(dto, 'generatedOn', 'lastGeneratedOn', 'generatedAt')) ?? '',
      nextScheduled: wallClock(str(dto, 'nextScheduledOn', 'nextScheduled')),
      reviewDue: wallClock(str(dto, 'reviewDueOn', 'reviewDue'))?.slice(0, 10) ?? null,
      frequency: pick(FREQUENCIES, str(dto, 'frequency'), 'On demand'),
      status: pick(SR_STATUSES, str(dto, 'status'), 'Draft'),
      hasDateFilter: !!(from || to),
      updatedOn: (wallClock(updatedOn) ?? '').slice(0, 10),
      schedule: this.toSchedule(dto),
      preview: { dimension: 'category', measure: str(dto, 'reportType') || 'Records', unit: 'count', total: 0 }
    };
  }
}

/** Accepts a bare array or a paged envelope ({ items | data | results | records, totalCount | total }). */
function unwrapList(body: unknown): { items: StandardReportDto[]; total: number | null } {
  if (Array.isArray(body)) return { items: body as StandardReportDto[], total: null };
  if (body && typeof body === 'object') {
    const o = body as Record<string, unknown>;
    for (const key of ['items', 'data', 'results', 'records', 'reports']) {
      const v = o[key];
      if (Array.isArray(v)) {
        const total = Number(o['totalCount'] ?? o['total'] ?? o['totalItems'] ?? o['totalRecords'] ?? NaN);
        return { items: v as StandardReportDto[], total: Number.isFinite(total) ? total : null };
      }
      // { data: { items, totalCount } }
      if (v && typeof v === 'object' && !Array.isArray(v)) return unwrapList(v);
    }
  }
  return { items: [], total: 0 };
}

function str(dto: StandardReportDto, ...keys: string[]): string {
  for (const k of keys) {
    const v = dto[k];
    if (typeof v === 'string' && v) return v;
  }
  return '';
}

/** Case-insensitive match against a known list ("needs_review" / "NeedsReview" → "Needs review"). */
function pick<T extends string>(list: readonly T[], value: string, fallback: T): T {
  const norm = (s: string) => s.toLowerCase().replace(/[^a-z0-9]/g, '');
  return list.find((x) => norm(x) === norm(value)) ?? fallback;
}

/** API timestamp → 'YYYY-MM-DDTHH:mm' (or 'YYYY-MM-DD'), as the wall-clock time that was stored. */
function wallClock(value: string): string | null {
  if (!value || value.startsWith('0001-01-01')) return null;
  return value.length > 10 ? value.slice(0, 16) : value.slice(0, 10);
}

/**
 * Wall-clock date/time → ISO string for the API. The report's time zone is sent separately (timeZone),
 * so the wall-clock value is written as-is with a 'Z' suffix and read back the same way.
 */
export function toApiDate(day: string, time: string): string {
  return `${day}T${time}:00.000Z`;
}
