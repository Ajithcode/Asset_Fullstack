import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, map, shareReplay, tap } from 'rxjs';
import { environment } from 'shared-ui';
import { CURRENT_USER } from '../shared/analytics-common';
import { DATA_SOURCES, FIELD_GROUPS, RA_MODULES, SCHEDULES, blankDefinition } from './custom-reports.data';
import { COUNT_KEY, fieldOf, isNumeric, outputColumns, sourceOf } from './custom-reports.engine';
import {
  Aggregate, CalcOperator, CustomReport, DataSource, FieldDef, FieldGroup, FieldKind, FilterOperator, PreviewColumn, PreviewResult,
  ReportDefinition, ReportRow, ReportStatus, ReportVisibility, VizType
} from './custom-reports.model';

// Custom reports are stored by the Report Analytics API (/api/custom-reports). The builder works on the UI model in
// custom-reports.model.ts; this service converts it to and from the API definition (data sources, selected fields,
// calculated fields, filters, grouping/aggregations, sorts, row limit and visualization).

/** Current date-time in Asia/Dubai (UTC+4) as 'YYYY-MM-DDTHH:mm'. */
export function dubaiNow(): string {
  return toDubai(new Date().toISOString());
}

/** API UTC timestamp → Asia/Dubai wall clock 'YYYY-MM-DDTHH:mm' ('' when missing). */
function toDubai(iso: string | null | undefined): string {
  const t = iso ? new Date(iso).getTime() : NaN;
  return Number.isNaN(t) ? '' : new Date(t + 4 * 3600_000).toISOString().slice(0, 16);
}

export function cloneReport(r: CustomReport): CustomReport {
  return JSON.parse(JSON.stringify(r)) as CustomReport;
}

/** Schedules are run in the UI's reporting time zone. */
const SCHEDULE_TIME_ZONE = 'Asia/Dubai';
const DEFAULT_ROW_LIMIT = 1000;
const PREVIEW_LIMIT = 50;

// ----- API shapes (System.Text.Json camelCase) -----

interface ApiCalculatedField { key: string; label: string; leftOperand: string; operator: string; rightOperand: string | null; dataType: string; decimals: number }
interface ApiFilter { field: string; operator: string; value: string | null; values?: string[]; valueTo?: string | null }
interface ApiAggregation { field: string | null; function: string; label?: string | null }
interface ApiSort { field: string; direction: string }
interface ApiVisualization {
  chartType: string; title?: string | null; xAxisField?: string | null; yAxisField?: string | null; defaultView?: string;
  showDataLabels?: boolean; maxCategories?: number;
}
export interface ApiDefinition {
  dataSources: string[];
  selectedFields: string[];
  calculatedFields: ApiCalculatedField[];
  filters: ApiFilter[];
  filterLogic: string;
  groupBy: string[];
  aggregations: ApiAggregation[];
  sorts: ApiSort[];
  rowLimit: number;
  visualization: ApiVisualization;
}
interface ApiSchedule { frequency: string; dayOfWeek?: string | null; dayOfMonth?: number | null; time?: string; timeZone?: string; recipients?: string[] }
interface ApiReport {
  reportId: string; reportName: string; description: string | null; module: string; ownerId: string | null; ownerName: string | null;
  visibility: string; status: string | null; isFavorite: boolean; canEdit?: boolean; definition?: ApiDefinition; schedule?: ApiSchedule | null;
  scheduleSummary?: string | null; chartType?: string; createdBy?: string | null; updatedAt: string | null; createdAt?: string | null;
}
interface ApiList { items: ApiReport[]; totalCount: number }
interface ApiDataSource { key: string; label: string; description: string; required: boolean; fieldCount: number }
interface ApiFieldGroup { group: string; fields: { key: string; label: string; dataType: string; dataSource: string }[] }
interface ApiOptions { modules: string[] }
interface ApiPreview {
  columns: { key: string; label: string; dataType: string }[];
  rows: Record<string, unknown>[];
  totalRows: number;
  rowLimit: number;
  isTruncated: boolean;
  chart: { title: string; xAxisField: string; yAxisField: string; points: { label: string; value: number }[] } | null;
}

export interface CustomReportOwner {
  id: string;
  name: string;
}

/** Library query: GET /api/custom-reports. */
export interface LibraryQuery {
  search?: string;
  owner?: string;
  module?: string;
  status?: string;
  sortBy?: 'updatedAt' | 'reportName';
  sortDirection?: 'asc' | 'desc';
}

export interface ImportResult {
  created: number;
  failed: { reportName: string | null; message: string }[];
}

const KIND_BY_TYPE: Record<string, FieldKind> = { string: 'text', number: 'number', currency: 'money', date: 'date' };
const CALC_TO_API: Record<CalcOperator, string> = { '+': '+', '-': '-', '×': '*', '÷': '/' };
const CALC_FROM_API: Record<string, CalcOperator> = { '+': '+', '-': '-', '*': '×', '/': '÷' };
const FILTER_TO_API: Record<FilterOperator, string> = {
  equals: 'equals', 'not equals': 'notEquals', contains: 'contains',
  '=': 'equals', '≠': 'notEquals', '>': 'greaterThan', '<': 'lessThan', '≥': 'greaterOrEqual', '≤': 'lessOrEqual',
  on: 'equals', before: 'lessThan', after: 'greaterThan'
};
const AGG_TO_API: Record<Aggregate, string> = { Sum: 'sum', Average: 'avg', Min: 'min', Max: 'max' };
const AGG_FROM_API: Record<string, Aggregate> = { sum: 'Sum', avg: 'Average', min: 'Min', max: 'Max' };
const VIZ_FROM_API: Record<string, VizType> = { Table: 'Table', Bar: 'Bar', 'Horizontal bar': 'Bar', Line: 'Line', Area: 'Line', Pie: 'Donut', Donut: 'Donut' };

@Injectable({ providedIn: 'root' })
export class CustomReportsService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.reportAnalyticsApiUrl}api/custom-reports`;

  private items: CustomReport[] = [];
  private summary = { total: 0, published: 0, scheduled: 0, drafts: 0 };
  private catalog$: Observable<void> | null = null;

  /** The library (current server filters). */
  get reports(): CustomReport[] {
    return this.items;
  }

  /** Tiles: GET /api/custom-reports/summary. */
  get stats(): { total: number; published: number; scheduled: number; drafts: number } {
    return this.summary;
  }

  get(id: string): CustomReport | undefined {
    return this.items.find((r) => r.id === id);
  }

  isNameTaken(name: string, exceptId = ''): boolean {
    const n = name.trim().toLowerCase();
    return this.items.some((r) => r.id !== exceptId && r.name.trim().toLowerCase() === n);
  }

  blank(): CustomReport {
    return {
      // New reports start with every dropdown empty: the user picks the source, aggregate, row limit, visibility and schedule
      ...blankDefinition(''),
      aggregate: '' as Aggregate,
      rowLimit: null,
      id: '',
      name: '',
      description: '',
      owner: CURRENT_USER.name,
      ownerId: '',
      canEdit: true,
      loaded: true,
      module: 'Assets',
      status: 'Draft',
      favourite: false,
      updatedAt: dubaiNow(),
      visibility: '' as ReportVisibility,
      schedule: ''
    };
  }

  // ----- Catalogue -----

  /** GET /data-sources, /fields and /options once; fills DATA_SOURCES, FIELD_GROUPS and RA_MODULES in place. */
  loadCatalog(): Observable<void> {
    this.catalog$ ??= forkJoin({
      sources: this.http.get<ApiDataSource[]>(`${this.api}/data-sources`),
      fields: this.http.get<ApiFieldGroup[]>(`${this.api}/fields`),
      options: this.http.get<ApiOptions>(`${this.api}/options`)
    }).pipe(
      tap(({ sources, fields, options }) => {
        RA_MODULES.splice(0, RA_MODULES.length, ...(options?.modules ?? []));
        FIELD_GROUPS.splice(0, FIELD_GROUPS.length, ...buildGroups(fields ?? []));
        DATA_SOURCES.splice(0, DATA_SOURCES.length, ...buildSources(sources ?? [], options?.modules ?? []));
      }),
      map(() => undefined),
      shareReplay(1)
    );
    return this.catalog$;
  }

  // ----- Library -----

  /** GET /api/custom-reports (every page the API allows: 200 rows). */
  loadLibrary(query: LibraryQuery): Observable<CustomReport[]> {
    let params = this.scope().set('page', 1).set('pageSize', 200).set('timeZone', SCHEDULE_TIME_ZONE);
    for (const [key, value] of Object.entries(query)) {
      if (value) params = params.set(key, String(value));
    }
    return this.http.get<ApiList>(this.api, { params }).pipe(
      map((body) => (body?.items ?? []).map((item) => this.fromApi(item))),
      tap((list) => (this.items = list))
    );
  }

  /** GET /api/custom-reports/summary. */
  loadSummary(): Observable<void> {
    return this.http.get<{ customReports: number; published: number; scheduled: number; drafts: number }>(`${this.api}/summary`, { params: this.scope() }).pipe(
      tap((s) => (this.summary = { total: s?.customReports ?? 0, published: s?.published ?? 0, scheduled: s?.scheduled ?? 0, drafts: s?.drafts ?? 0 })),
      map(() => undefined)
    );
  }

  /** GET /api/custom-reports/owners: the Owner filter. */
  owners(): Observable<CustomReportOwner[]> {
    return this.http.get<{ ownerId: string; ownerName: string | null }[]>(`${this.api}/owners`, { params: this.scope() }).pipe(
      map((list) => (list ?? []).filter((o) => !!o.ownerId).map((o) => ({ id: o.ownerId, name: o.ownerName || o.ownerId })))
    );
  }

  // ----- Report CRUD and actions -----

  /** GET /api/custom-reports/{id}: the full definition for the builder. */
  load(id: string): Observable<CustomReport> {
    return this.http.get<ApiReport>(`${this.api}/${encodeURIComponent(id)}`).pipe(map((dto) => this.fromApi(dto)));
  }

  /** POST (new) or PUT (existing) the report; emits the stored copy. */
  save(report: CustomReport, step = 1): Observable<CustomReport> {
    const body = {
      reportName: report.name.trim(),
      description: report.description.trim(),
      module: sourceOf(report.sourceKey).module,
      visibility: visibilityToApi(report.visibility),
      currentStep: step,
      definition: toApiDefinition(report),
      schedule: scheduleToApi(report.schedule)
    };
    const save$ = report.id
      ? this.http.put<ApiReport>(`${this.api}/${encodeURIComponent(report.id)}`, body)
      : this.http.post<ApiReport>(this.api, { ...body, tenantId: environment.tenantId, clientId: environment.clientId });
    return save$.pipe(map((dto) => this.fromApi(dto)), tap((stored) => this.replace(stored)));
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/${encodeURIComponent(id)}`).pipe(tap(() => (this.items = this.items.filter((r) => r.id !== id))));
  }

  /** POST /{id}/publish: Published, or Scheduled when the report has a schedule. */
  publish(id: string): Observable<CustomReport> {
    return this.http.post<ApiReport>(`${this.api}/${encodeURIComponent(id)}/publish`, {}).pipe(map((dto) => this.fromApi(dto)), tap((r) => this.replace(r)));
  }

  /** POST /{id}/unpublish: back to Draft. */
  unpublish(id: string): Observable<CustomReport> {
    return this.http.post<ApiReport>(`${this.api}/${encodeURIComponent(id)}/unpublish`, {}).pipe(map((dto) => this.fromApi(dto)), tap((r) => this.replace(r)));
  }

  /** POST /{id}/duplicate: a Draft copy owned by the current user. */
  duplicate(id: string): Observable<CustomReport> {
    return this.http.post<ApiReport>(`${this.api}/${encodeURIComponent(id)}/duplicate`, {}).pipe(
      map((dto) => this.fromApi(dto)),
      tap((copy) => (this.items = [copy, ...this.items]))
    );
  }

  /** PUT /{id}/favorite. */
  setFavourite(id: string, favourite: boolean): Observable<void> {
    return this.http.put<unknown>(`${this.api}/${encodeURIComponent(id)}/favorite`, { isFavorite: favourite }).pipe(
      tap(() => {
        const r = this.get(id);
        if (r) r.favourite = favourite;
      }),
      map(() => undefined)
    );
  }

  // ----- Preview, export, import -----

  /**
   * Preview rows: GET /{id}/preview for a saved report whose definition is unchanged, otherwise POST /preview with the
   * builder's current definition.
   */
  preview(def: ReportDefinition, savedId: string | null, limit = PREVIEW_LIMIT): Observable<PreviewResult> {
    const req$ = savedId
      ? this.http.get<ApiPreview>(`${this.api}/${encodeURIComponent(savedId)}/preview`, { params: new HttpParams().set('limit', limit) })
      : this.http.post<ApiPreview>(`${this.api}/preview`, {
          definition: toApiDefinition(def),
          limit,
          tenantId: environment.tenantId,
          clientId: environment.clientId
        });
    return req$.pipe(map((body) => toPreview(body)));
  }

  /** GET /{id}/export: the report's data (every row up to its row limit). */
  exportData(id: string, format: 'xlsx' | 'csv'): Observable<Blob> {
    return this.http.get(`${this.api}/${encodeURIComponent(id)}/export`, { params: new HttpParams().set('format', format), responseType: 'blob' });
  }

  /** GET /export?format=json: the listed reports as importable templates. */
  exportTemplates(ids: string[]): Observable<Blob> {
    const params = this.scope().set('format', 'json').set('reportIds', ids.join(','));
    return this.http.get(`${this.api}/export`, { params, responseType: 'blob' });
  }

  /** POST /import (multipart "file"): each template becomes a Draft. */
  importFile(file: File): Observable<ImportResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http
      .post<{ created: ApiReport[]; failed: { reportName: string | null; errors: Record<string, string[]> }[] }>(`${this.api}/import`, form)
      .pipe(
        map((res) => ({
          created: res?.created?.length ?? 0,
          failed: (res?.failed ?? []).map((f) => ({ reportName: f.reportName, message: Object.values(f.errors ?? {}).flat()[0] ?? 'Invalid template' }))
        }))
      );
  }

  // ----- Mapping -----

  private scope(): HttpParams {
    return new HttpParams().set('tenantId', environment.tenantId).set('clientId', environment.clientId);
  }

  private replace(stored: CustomReport): void {
    const i = this.items.findIndex((r) => r.id === stored.id);
    this.items = i < 0 ? [stored, ...this.items] : this.items.map((r) => (r.id === stored.id ? stored : r));
  }

  /** API report (full or library item) → UI model. Library items carry no definition (loaded = false). */
  private fromApi(dto: ApiReport): CustomReport {
    const def = dto.definition ? fromApiDefinition(dto.definition) : { ...blankDefinition(''), viz: VIZ_FROM_API[dto.chartType ?? ''] ?? 'Table' };
    return {
      ...def,
      id: dto.reportId,
      name: dto.reportName ?? '',
      description: dto.description ?? '',
      owner: dto.ownerName || dto.createdBy || '—',
      ownerId: dto.ownerId ?? '',
      canEdit: dto.canEdit ?? true,
      loaded: !!dto.definition,
      module: dto.module || 'Assets',
      status: (['Draft', 'Published', 'Scheduled'] as ReportStatus[]).find((s) => s === dto.status) ?? 'Draft',
      favourite: !!dto.isFavorite,
      updatedAt: toDubai(dto.updatedAt ?? dto.createdAt),
      visibility: visibilityFromApi(dto.visibility),
      schedule: dto.definition ? scheduleFromApi(dto.schedule ?? null) : dto.scheduleSummary ? scheduleFromApi(null, dto.scheduleSummary) : 'Not scheduled'
    };
  }
}

// ----- Catalogue builders -----

function groupKey(source: string, group: string): string {
  return `${source}|${group}`;
}

/** One UI group per API group and data source, so combining sources shows exactly their fields. */
function buildGroups(groups: ApiFieldGroup[]): FieldGroup[] {
  const out: FieldGroup[] = [];
  for (const g of groups) {
    const bySource = new Map<string, FieldDef[]>();
    for (const f of g.fields ?? []) {
      const key = groupKey(f.dataSource, g.group);
      bySource.set(key, [...(bySource.get(key) ?? []), { key: f.key, label: f.label, kind: KIND_BY_TYPE[f.dataType] ?? 'text', group: key }]);
    }
    for (const [key, fields] of bySource) out.push({ key, label: g.group, fields });
  }
  return out;
}

/** Required source alone and with every combination of the optional sources. */
function buildSources(sources: ApiDataSource[], modules: string[]): DataSource[] {
  const required = sources.filter((s) => s.required);
  const optional = sources.filter((s) => !s.required);
  const out: DataSource[] = [];
  for (let mask = 0; mask < 1 << optional.length; mask++) {
    const extra = optional.filter((_, i) => mask & (1 << i));
    const picked = [...required, ...extra];
    if (!picked.length) continue;
    const keys = picked.map((s) => s.key);
    const moduleName = extra.length === 1 ? modules.find((m) => m.toLowerCase() === extra[0].label.toLowerCase()) : undefined;
    out.push({
      key: keys.join('+'),
      label: picked.map((s) => s.label).join(' + '),
      module: moduleName ?? modules.find((m) => m === 'Assets') ?? modules[0] ?? 'Assets',
      groups: FIELD_GROUPS.filter((g) => keys.includes(g.key.split('|')[0])).map((g) => g.key),
      sources: keys,
      description: picked.map((s) => s.description).join(' · ')
    });
  }
  return out.sort((a, b) => a.sources.length - b.sources.length);
}

// ----- Definition mapping -----

/** Output column key → API column key (aggregations are named <function>_<field>, the record count is "count"). */
function columnToApi(def: ReportDefinition, key: string): string {
  if (!key) return '';
  if (key === COUNT_KEY) return 'count';
  if (def.groupBy && key !== def.groupBy) return `${AGG_TO_API[def.aggregate || 'Sum']}_${key}`;
  return key;
}

function columnFromApi(key: string | null | undefined): string {
  if (!key) return '';
  if (key === 'count') return COUNT_KEY;
  const m = /^(sum|avg|min|max)_(.+)$/.exec(key);
  return m ? m[2] : key;
}

export function toApiDefinition(def: ReportDefinition): ApiDefinition {
  const source = sourceOf(def.sourceKey);
  const cols = outputColumns(def);
  const group = def.groupBy ? fieldOf(def, def.groupBy) : undefined;
  const numeric = cols.filter((c) => isNumeric(c.kind) && c.key !== COUNT_KEY && c.key !== def.groupBy);
  const labelCol = group ? cols[0] : cols.find((c) => !isNumeric(c.kind));
  const valueCol = cols.find((c) => c.key === def.chartValue && isNumeric(c.kind)) ?? cols.find((c) => isNumeric(c.kind));
  const chart = def.viz !== 'Table' && !!labelCol && !!valueCol;
  return {
    dataSources: def.sourceKey ? def.sourceKey.split('+') : source.sources,
    selectedFields: [...def.fields],
    calculatedFields: def.calcFields.map((c) => ({
      key: c.key, label: c.label, leftOperand: c.left, operator: CALC_TO_API[c.op], rightOperand: c.right, dataType: 'number', decimals: 2
    })),
    filters: def.filters
      .filter((f) => f.field && f.op && f.value.trim())
      .map((f) => ({ field: f.field, operator: FILTER_TO_API[f.op] ?? 'equals', value: f.value.trim() })),
    filterLogic: 'And',
    groupBy: group ? [group.key] : [],
    aggregations: group
      ? [{ field: null, function: 'count' }, ...numeric.map((c) => ({ field: c.key, function: AGG_TO_API[def.aggregate || 'Sum'] }))]
      : [],
    sorts: def.sortBy ? [{ field: columnToApi(def, def.sortBy), direction: def.sortDir }] : [],
    rowLimit: def.rowLimit ?? DEFAULT_ROW_LIMIT,
    visualization: {
      // The API only accepts a chart with both axes; without them the report stays a table
      chartType: chart ? def.viz : 'Table',
      xAxisField: chart ? columnToApi(def, labelCol!.key) : null,
      yAxisField: chart ? columnToApi(def, valueCol!.key) : null,
      defaultView: chart ? 'Chart' : 'Table',
      showDataLabels: true,
      maxCategories: 6
    }
  };
}

function fromApiDefinition(d: ApiDefinition): ReportDefinition {
  const order = DATA_SOURCES.length ? DATA_SOURCES[DATA_SOURCES.length - 1].sources : ['assets', 'maintenance', 'inspection'];
  const sources = [...(d.dataSources ?? [])].sort((a, b) => order.indexOf(a) - order.indexOf(b));
  const calcFields = (d.calculatedFields ?? []).map((c) => ({
    key: c.key, label: c.label, left: c.leftOperand, op: CALC_FROM_API[c.operator] ?? '+', right: c.rightOperand ?? ''
  }));
  const def: ReportDefinition = {
    ...blankDefinition(sources.join('+')),
    fields: [...(d.selectedFields ?? [])],
    calcFields,
    groupBy: d.groupBy?.[0] ?? '',
    aggregate: AGG_FROM_API[(d.aggregations ?? []).find((a) => a.field && AGG_FROM_API[a.function])?.function ?? 'sum'] ?? 'Sum',
    rowLimit: d.rowLimit ?? null,
    viz: VIZ_FROM_API[d.visualization?.chartType ?? 'Table'] ?? 'Table'
  };
  const sort = d.sorts?.[0];
  def.sortBy = columnFromApi(sort?.field);
  def.sortDir = sort?.direction === 'asc' ? 'asc' : 'desc';
  def.chartValue = columnFromApi(d.visualization?.yAxisField);
  def.filters = (d.filters ?? []).map((f) => {
    const kind = fieldOf(def, f.field)?.kind ?? 'text';
    return { field: f.field, op: filterFromApi(f.operator, kind), value: f.value ?? (f.values ?? []).join(', ') };
  });
  return def;
}

function filterFromApi(op: string, kind: FieldKind): FilterOperator {
  if (kind === 'date') return op === 'lessThan' || op === 'lessOrEqual' ? 'before' : op === 'greaterThan' || op === 'greaterOrEqual' ? 'after' : 'on';
  if (isNumeric(kind)) {
    const numeric: Record<string, FilterOperator> = {
      equals: '=', notEquals: '≠', greaterThan: '>', lessThan: '<', greaterOrEqual: '≥', lessOrEqual: '≤'
    };
    return numeric[op] ?? '=';
  }
  return op === 'notEquals' ? 'not equals' : op === 'contains' ? 'contains' : 'equals';
}

function visibilityToApi(v: ReportVisibility): string {
  return v === 'Only me' ? 'Private' : v || 'My team';
}

function visibilityFromApi(v: string): ReportVisibility {
  return v === 'Private' ? 'Only me' : v === 'Organization' ? 'Organization' : 'My team';
}

/** 'Weekly – Monday, 08:00' → API schedule; 'Not scheduled' → frequency None. */
function scheduleToApi(s: string): ApiSchedule {
  const m = /^(Daily|Weekly|Monthly|Quarterly)\b(?:\s*–\s*([^,]+?))?(?:,\s*)?(\d{2}:\d{2})?$/.exec(s.trim());
  if (!m) return { frequency: 'None' };
  const [, frequency, part, timeOnly] = m;
  const time = timeOnly ?? (/\d{2}:\d{2}/.exec(s)?.[0] ?? '08:00');
  const schedule: ApiSchedule = { frequency, time, timeZone: SCHEDULE_TIME_ZONE, recipients: [] };
  if (frequency === 'Weekly') schedule.dayOfWeek = (part ?? 'Monday').trim();
  if (frequency === 'Monthly' || frequency === 'Quarterly') schedule.dayOfMonth = parseInt(part ?? '1', 10) || 1;
  return schedule;
}

/** API schedule (or its summary) → one of SCHEDULES; an unlisted schedule is added to the list so the select can show it. */
function scheduleFromApi(s: ApiSchedule | null, summary?: string): string {
  let text = 'Not scheduled';
  if (s && s.frequency && s.frequency !== 'None') {
    const time = s.time || '08:00';
    if (s.frequency === 'Daily') text = `Daily – ${time}`;
    else if (s.frequency === 'Weekly') text = `Weekly – ${s.dayOfWeek || 'Monday'}, ${time}`;
    else text = `${s.frequency} – ${ordinal(s.dayOfMonth ?? 1)} day, ${time}`;
  } else if (summary) {
    text = SCHEDULES.find((x) => x.split(' ')[0] === summary.split(' ')[0]) ?? summary;
  }
  if (!SCHEDULES.includes(text)) SCHEDULES.push(text);
  return text;
}

function ordinal(n: number): string {
  const s = n % 100 >= 11 && n % 100 <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th';
  return `${n}${s}`;
}

function toPreview(body: ApiPreview): PreviewResult {
  const columns: PreviewColumn[] = (body?.columns ?? []).map((c) => ({ key: c.key, label: c.label, kind: KIND_BY_TYPE[c.dataType] ?? 'text' }));
  const rows: ReportRow[] = (body?.rows ?? []).map((r) => {
    const row: ReportRow = {};
    for (const c of columns) {
      const v = r[c.key];
      row[c.key] = v === null || v === undefined ? '' : typeof v === 'number' ? v : String(v);
    }
    return row;
  });
  const chart = body?.chart ?? null;
  const labelKey = chart?.xAxisField ?? columns.find((c) => !isNumeric(c.kind))?.key ?? '';
  const valueKey = chart?.yAxisField ?? columns.find((c) => isNumeric(c.kind))?.key ?? '';
  const labelCol = columns.find((c) => c.key === labelKey);
  const valueCol = columns.find((c) => c.key === valueKey);
  return {
    columns,
    rows,
    matched: body?.totalRows ?? rows.length,
    labelKey,
    valueKey,
    title: chart?.title ?? (labelCol && valueCol ? `${valueCol.label} by ${labelCol.label}` : 'Report preview'),
    points: chart?.points ?? null
  };
}

/** Shared by the page for API error toasts. */
export function apiError(err: { status?: number; error?: unknown }, fallback: string): string {
  if (err?.status === 0) return `${fallback} The Report Analytics service is not reachable.`;
  if (err?.status === 403) return 'Only the report owner can change this report.';
  if (err?.status === 404) return 'The report no longer exists.';
  const body = err?.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | string | null;
  if (typeof body === 'string' && body) return body;
  if (body && typeof body === 'object') {
    const first = body.errors ? Object.values(body.errors).flat()[0] : '';
    return first || body.detail || body.title || fallback;
  }
  return fallback;
}

