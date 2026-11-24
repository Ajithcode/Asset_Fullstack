import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, map, tap } from 'rxjs';
import { environment } from 'shared-ui';
import { money, percent } from '../shared/analytics-common';
import { RA_COLORS, RA_SERIES_COLORS, compactNumber } from '../shared/charts';
import {
  AttentionAsset, DashFilters, Dashboard, DashboardRole, METRICS, Sharing, WIDGET_SIZES, WidgetConfig, WidgetData, WidgetSpan, WidgetType
} from './interactive-dashboards.data';

// Interactive dashboards are stored by the Report Analytics API (/api/interactive-dashboards). Widgets sit on a 12-column
// grid there (x, y, w, h); the page flows them in order with a column span, so positions are worked out from the order.

const SPANS = WIDGET_SIZES.map((s) => s.span).concat([5, 7]);
const GRID_COLUMNS = 12;

const TYPE_TO_API: Record<WidgetType, string> = {
  kpi: 'kpi_card', line: 'line_chart', bar: 'bar_chart', donut: 'donut_chart', table: 'table', map: 'map', heatmap: 'heatmap'
};
const TYPE_FROM_API: Record<string, WidgetType> = Object.fromEntries(Object.entries(TYPE_TO_API).map(([k, v]) => [v, k as WidgetType]));
const DEFAULT_HEIGHT: Record<WidgetType, number> = { kpi: 2, line: 4, bar: 4, donut: 4, table: 4, map: 4, heatmap: 4 };

const VISIBILITY_TO_API: Record<Sharing, string> = { private: 'Private', people: 'Shared', organization: 'Organization' };
const VISIBILITY_FROM_API: Record<string, Sharing> = { Private: 'private', Shared: 'people', Organization: 'organization' };

/** Colour per map state (worst state first). */
const STATE_COLORS: Record<string, string> = {
  Critical: RA_COLORS.pink, Maintenance: RA_COLORS.orange, Inspection: RA_COLORS.blue, Active: RA_COLORS.green
};
const KPI_TONES: Record<string, string> = {
  total_assets: 'purple', active_work_orders: 'blue', preventive_compliance: 'green', inventory_value: 'orange', maintenance_cost: 'pink'
};
/** KPIs where a rise is bad news */
const UP_IS_BAD = new Set(['active_work_orders', 'maintenance_cost']);

// ----- API shapes -----

interface ApiWidget {
  widgetId: string; widgetType: string; metric: string; title: string;
  position?: { x: number; y: number; w: number; h: number };
}
interface ApiShareEntry { id: string; name?: string | null; permission?: string }
interface ApiDashboard {
  dashboardId: string; dashboardName: string; description?: string | null; ownerName?: string | null; visibility: string;
  isFavorite: boolean; accessRole: string; canEdit?: boolean; canShare?: boolean; widgetCount?: number; updatedAt?: string | null;
  sharedWith?: { users: ApiShareEntry[]; groups: ApiShareEntry[] }; defaultFilters?: Record<string, unknown> | null;
  widgets?: ApiWidget[]; information?: { lastUpdated?: string | null } | null; audienceRole?: string | null;
}
interface ApiWidgetData { widgetId: string; widgetType: string; metric: string; title: string; shape: string; data: unknown; error: string | null }
interface ApiOptions {
  datePresets: string[]; defaultDatePreset: string;
  metrics: { key: string; label: string; description: string; widgetTypes: string[] }[];
}
interface Option { value: string; label: string }

/** Values for the filter bar: GET /options (date presets) and GET /filters. */
export interface DashboardFilterOptions {
  datePresets: string[];
  defaultDatePreset: string;
  organizations: Option[];
  locations: Option[];
  categories: Option[];
}

function nowText(iso?: string | null): string {
  const d = iso ? new Date(iso) : new Date();
  if (Number.isNaN(d.getTime())) return '—';
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return `${String(d.getDate()).padStart(2, '0')} ${months[d.getMonth()]} ${d.getFullYear()}, ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

/** Dashboards, widgets and widget data from /api/interactive-dashboards. */
@Injectable({ providedIn: 'root' })
export class DashboardStore {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.reportAnalyticsApiUrl}api/interactive-dashboards`;

  /** The selector list (every dashboard the user can see). */
  dashboards: Dashboard[] = [];

  get(id: string): Dashboard | undefined {
    return this.dashboards.find((d) => d.id === id);
  }

  nameTaken(name: string): boolean {
    const n = name.trim().toLowerCase();
    return this.dashboards.some((d) => d.name.toLowerCase() === n);
  }

  static spanOk(span: number): span is WidgetSpan {
    return SPANS.includes(span as WidgetSpan);
  }

  // ----- Options -----

  /** GET /options (metrics, date presets) and /filters; fills METRICS in place. */
  loadOptions(): Observable<DashboardFilterOptions> {
    return forkJoin({
      options: this.http.get<ApiOptions>(`${this.api}/options`),
      filters: this.http.get<{ organizations: Option[]; locations: Option[]; assetCategories: Option[] }>(`${this.api}/filters`, { params: this.scope() })
    }).pipe(
      tap(({ options }) =>
        METRICS.splice(0, METRICS.length, ...(options?.metrics ?? []).map((m) => ({
          key: m.key,
          label: m.label,
          description: m.description,
          types: m.widgetTypes.map((t) => TYPE_FROM_API[t]).filter((t): t is WidgetType => !!t)
        })))
      ),
      map(({ options, filters }) => ({
        datePresets: (options?.datePresets ?? []).filter((p) => p !== 'Custom'),
        defaultDatePreset: options?.defaultDatePreset ?? '',
        organizations: filters?.organizations ?? [],
        locations: filters?.locations ?? [],
        categories: filters?.assetCategories ?? []
      }))
    );
  }

  // ----- Dashboards -----

  /** GET /api/interactive-dashboards?view=all. */
  loadList(): Observable<Dashboard[]> {
    const params = this.scope().set('view', 'all').set('page', 1).set('pageSize', 200);
    return this.http.get<{ items: ApiDashboard[] }>(this.api, { params }).pipe(
      map((body) => (body?.items ?? []).map((d) => this.fromApi(d))),
      tap((list) => (this.dashboards = list))
    );
  }

  /** GET /{id}: the dashboard with its widgets. */
  load(id: string): Observable<Dashboard> {
    return this.http.get<ApiDashboard>(`${this.api}/${encodeURIComponent(id)}`).pipe(map((d) => this.remember(this.fromApi(d))));
  }

  /** POST /api/interactive-dashboards. */
  create(name: string, description: string, sharing: Sharing, widgets: WidgetConfig[]): Observable<Dashboard> {
    return this.http
      .post<ApiDashboard>(this.api, {
        dashboardName: name,
        description,
        visibility: VISIBILITY_TO_API[sharing],
        templateKey: widgets.length ? null : 'blank',
        widgets: toApiWidgets(widgets, false),
        tenantId: environment.tenantId,
        clientId: environment.clientId
      })
      .pipe(map((d) => this.remember(this.fromApi(d))));
  }

  /** PUT /{id}: replaces the information and the whole widget list (used to discard layout edits). */
  replace(d: Dashboard, widgets: WidgetConfig[]): Observable<Dashboard> {
    return this.http
      .put<ApiDashboard>(`${this.api}/${encodeURIComponent(d.id)}`, {
        dashboardName: d.name,
        description: d.description,
        defaultFilters: d.defaultFilters,
        widgets: toApiWidgets(widgets, true)
      })
      .pipe(map((x) => this.remember(this.fromApi(x))));
  }

  remove(d: Dashboard): Observable<void> {
    return this.http.delete<void>(`${this.api}/${encodeURIComponent(d.id)}`).pipe(tap(() => (this.dashboards = this.dashboards.filter((x) => x.id !== d.id))));
  }

  /** POST /{id}/duplicate: an editable copy owned by the user. */
  duplicate(d: Dashboard): Observable<Dashboard> {
    return this.http.post<ApiDashboard>(`${this.api}/${encodeURIComponent(d.id)}/duplicate`, {}).pipe(map((x) => this.remember(this.fromApi(x))));
  }

  /** PUT /{id}/favorite. */
  setFavorite(d: Dashboard, favorite: boolean): Observable<void> {
    return this.http.put<unknown>(`${this.api}/${encodeURIComponent(d.id)}/favorite`, { isFavorite: favorite }).pipe(
      tap(() => {
        d.favorite = favorite;
        const listed = this.get(d.id);
        if (listed) listed.favorite = favorite;
      }),
      map(() => undefined)
    );
  }

  /** PUT /{id}/share. People and groups have no directory ids yet, so their names are sent as ids. */
  share(d: Dashboard, sharing: Sharing, people: string[], groups: string[]): Observable<void> {
    const entry = (name: string) => ({ id: name, name, permission: 'View' });
    return this.http
      .put<unknown>(`${this.api}/${encodeURIComponent(d.id)}/share`, {
        visibility: VISIBILITY_TO_API[sharing],
        users: sharing === 'people' ? people.map(entry) : [],
        groups: sharing === 'people' ? groups.map(entry) : []
      })
      .pipe(map(() => undefined));
  }

  // ----- Widgets -----

  /** PUT /{id}/layout: positions for every widget, in the page order. */
  saveLayout(d: Dashboard, widgets: WidgetConfig[]): Observable<void> {
    const positions = layout(widgets);
    return this.http
      .put<unknown>(`${this.api}/${encodeURIComponent(d.id)}/layout`, {
        widgets: widgets.map((w, i) => ({ widgetId: w.id, ...positions[i] }))
      })
      .pipe(map(() => undefined));
  }

  /** POST /{id}/widgets: the new widget is placed below the others (the page then saves the layout for its slot). */
  addWidget(d: Dashboard, w: Omit<WidgetConfig, 'id'>): Observable<WidgetConfig> {
    return this.http
      .post<ApiWidget>(`${this.api}/${encodeURIComponent(d.id)}/widgets`, {
        widgetType: TYPE_TO_API[w.type],
        metric: w.metric,
        title: w.title
      })
      .pipe(map((x) => ({ ...fromApiWidget(x), span: w.span })));
  }

  /** DELETE /{id}/widgets/{widgetId}. */
  removeWidget(d: Dashboard, w: WidgetConfig): Observable<void> {
    return this.http.delete<void>(`${this.api}/${encodeURIComponent(d.id)}/widgets/${encodeURIComponent(w.id)}`);
  }

  /** PUT /{id}/widgets/{widgetId}: title, type and size of one widget. */
  updateWidget(d: Dashboard, w: WidgetConfig): Observable<void> {
    return this.http
      .put<unknown>(`${this.api}/${encodeURIComponent(d.id)}/widgets/${encodeURIComponent(w.id)}`, {
        widgetId: w.id,
        widgetType: TYPE_TO_API[w.type],
        metric: w.metric,
        title: w.title
      })
      .pipe(map(() => undefined));
  }

  // ----- Data -----

  /** GET /{id}/data: every widget's figures for the filter bar, keyed by widget id. */
  data(d: Dashboard, f: DashFilters): Observable<Record<string, WidgetData>> {
    return this.http
      .get<{ widgets: ApiWidgetData[] }>(`${this.api}/${encodeURIComponent(d.id)}/data`, { params: filterParams(f) })
      .pipe(map((body) => this.toDataMap(d, body?.widgets ?? [])));
  }

  /** GET /{id}/widgets/{widgetId}/data: one widget (after it is added). */
  widgetData(d: Dashboard, w: WidgetConfig, f: DashFilters): Observable<Record<string, WidgetData>> {
    return this.http
      .get<{ widgets: ApiWidgetData[] }>(`${this.api}/${encodeURIComponent(d.id)}/widgets/${encodeURIComponent(w.id)}/data`, { params: filterParams(f) })
      .pipe(map((body) => this.toDataMap(d, body?.widgets ?? [])));
  }

  /** POST /widgets/preview: data for a widget before it is added. */
  preview(type: WidgetType, metric: string, title: string, f: DashFilters): Observable<WidgetData> {
    const filters = { datePreset: f.period || null, organization: f.organization || null, location: f.location || null, assetCategory: f.category || null };
    return this.http
      .post<ApiWidgetData>(`${this.api}/widgets/preview`, {
        widget: { widgetType: TYPE_TO_API[type], metric, title },
        filters,
        tenantId: environment.tenantId,
        clientId: environment.clientId
      })
      .pipe(map((x) => toWidgetData(type, x)));
  }

  // ----- Mapping -----

  private scope(): HttpParams {
    return new HttpParams().set('tenantId', environment.tenantId).set('clientId', environment.clientId);
  }

  private remember(d: Dashboard): Dashboard {
    const i = this.dashboards.findIndex((x) => x.id === d.id);
    const listed = { ...d, widgets: [] as WidgetConfig[], widgetCount: d.widgets.length };
    this.dashboards = i < 0 ? [...this.dashboards, listed] : this.dashboards.map((x) => (x.id === d.id ? listed : x));
    return d;
  }

  private toDataMap(d: Dashboard, list: ApiWidgetData[]): Record<string, WidgetData> {
    const out: Record<string, WidgetData> = {};
    for (const x of list) {
      const type = d.widgets.find((w) => w.id === x.widgetId)?.type ?? TYPE_FROM_API[x.widgetType] ?? 'table';
      out[x.widgetId] = toWidgetData(type, x);
    }
    return out;
  }

  private fromApi(d: ApiDashboard): Dashboard {
    const widgets = [...(d.widgets ?? [])]
      .sort((a, b) => (a.position?.y ?? 0) - (b.position?.y ?? 0) || (a.position?.x ?? 0) - (b.position?.x ?? 0))
      .map(fromApiWidget)
      .filter((w) => !!w.type);
    const role = (['Owner', 'Editor', 'Viewer'] as DashboardRole[]).find((r) => r === d.accessRole) ?? 'Viewer';
    return {
      id: d.dashboardId,
      name: d.dashboardName,
      description: d.description ?? '',
      owner: d.ownerName || '—',
      sharing: VISIBILITY_FROM_API[d.visibility] ?? 'private',
      sharedWith: (d.sharedWith?.users ?? []).map((u) => u.name || u.id),
      groups: (d.sharedWith?.groups ?? []).map((g) => g.name || g.id),
      role,
      favorite: !!d.isFavorite,
      updatedAt: nowText(d.information?.lastUpdated ?? d.updatedAt),
      widgets,
      widgetCount: d.widgetCount ?? widgets.length,
      canShare: d.canShare ?? role === 'Owner',
      defaultFilters: d.defaultFilters ?? null
    };
  }
}

function fromApiWidget(x: ApiWidget): WidgetConfig {
  const w = x.position?.w ?? 6;
  // Nearest span the page supports (the API allows any width 1-12)
  const span = [...SPANS].sort((a, b) => Math.abs(a - w) - Math.abs(b - w) || b - a)[0] as WidgetSpan;
  return { id: x.widgetId, type: TYPE_FROM_API[x.widgetType], metric: x.metric, title: x.title, span, h: x.position?.h };
}

/** Flows widgets left to right on the 12-column grid, wrapping when a row is full. */
function layout(widgets: WidgetConfig[]): { x: number; y: number; w: number; h: number }[] {
  let x = 0;
  let y = 0;
  let rowH = 0;
  return widgets.map((wd) => {
    const w = Math.min(GRID_COLUMNS, wd.span);
    const h = wd.h ?? DEFAULT_HEIGHT[wd.type] ?? 4;
    if (x + w > GRID_COLUMNS) {
      x = 0;
      y += rowH;
      rowH = 0;
    }
    const pos = { x, y, w, h };
    x += w;
    rowH = Math.max(rowH, h);
    return pos;
  });
}

function toApiWidgets(widgets: WidgetConfig[], keepIds: boolean) {
  const positions = layout(widgets);
  return widgets.map((w, i) => ({
    widgetId: keepIds ? w.id : null,
    widgetType: TYPE_TO_API[w.type],
    metric: w.metric,
    title: w.title,
    position: positions[i]
  }));
}

/** Filter bar → query: empty values are left out so the dashboard's default filters apply. */
function filterParams(f: DashFilters): HttpParams {
  let p = new HttpParams();
  if (f.period) p = p.set('datePreset', f.period);
  if (f.organization) p = p.set('organization', f.organization);
  if (f.location) p = p.set('location', f.location);
  if (f.category) p = p.set('assetCategory', f.category);
  return p;
}

function formatter(format: string | undefined): (v: number) => string {
  if (format === 'currency') return (v) => money(v);
  if (format === 'percent') return (v) => percent(v);
  return compactNumber;
}

function cell(v: unknown): string | number {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'number') return Math.round(v * 100) / 100;
  if (Array.isArray(v)) return v.join(', ');
  return String(v);
}

/** API widget data (by shape) → what the widget component renders for its type. */
function toWidgetData(type: WidgetType, x: ApiWidgetData): WidgetData {
  if (x.error || x.data === null || x.data === undefined) return { empty: true, message: x.error ?? undefined };
  const data = x.data as Record<string, unknown>;
  switch (x.shape) {
    case 'kpi': {
      const k = data as { value: number | null; format: string; previousValue: number | null; change: number | null; trend: string };
      const fmt = formatter(k.format);
      const up = k.trend === 'up';
      return {
        empty: k.value === null,
        kpi: {
          value: k.value === null ? '—' : fmt(k.value),
          prev: k.previousValue === null ? '—' : fmt(k.previousValue),
          delta: k.change ?? 0,
          good: k.trend === 'flat' || (UP_IS_BAD.has(x.metric) ? !up : up),
          tone: KPI_TONES[x.metric] ?? 'purple'
        }
      };
    }
    case 'category': {
      const c = data as { total: number; format: string; items: { label: string; value: number; percentage: number }[] };
      const items = c.items ?? [];
      const fmt = formatter(c.format);
      if (!items.length) return { empty: true };
      if (type === 'table') return { empty: false, table: { columns: ['Item', 'Value', 'Share %'], rows: items.map((i) => [i.label, cell(i.value), cell(i.percentage)]) } };
      if (type === 'donut') {
        return {
          empty: false,
          format: fmt,
          slices: items.map((i, n) => ({ name: i.label, value: i.value, color: RA_SERIES_COLORS[n % RA_SERIES_COLORS.length] })),
          centerValue: fmt(c.total),
          centerLabel: 'Total'
        };
      }
      return { empty: false, format: fmt, labels: items.map((i) => i.label), series: [{ name: x.title, values: items.map((i) => i.value), color: RA_COLORS.purple }] };
    }
    case 'series': {
      const s = data as { categories: string[]; series: { name: string; values: number[] }[] };
      if (!s.categories?.length) return { empty: true };
      if (type === 'table') {
        return { empty: false, table: { columns: ['Period', ...s.series.map((l) => l.name)], rows: s.categories.map((c, i) => [c, ...s.series.map((l) => cell(l.values[i]))]) } };
      }
      return {
        empty: false,
        labels: s.categories,
        series: s.series.map((l, i) => ({ name: l.name, values: l.values, color: RA_SERIES_COLORS[i % RA_SERIES_COLORS.length] })),
        stacked: type === 'bar' && s.series.length > 1
      };
    }
    case 'table': {
      const t = data as { columns: { key: string; label: string }[]; rows: Record<string, unknown>[] };
      if (!t.rows?.length) return { empty: true };
      if (x.metric === 'assets_requiring_attention') {
        const attention: AttentionAsset[] = t.rows.map((r) => ({
          id: String(r['asset_id'] ?? r['id'] ?? ''),
          name: String(r['asset_name'] ?? ''),
          category: String(r['category'] ?? '—'),
          site: String(r['location'] ?? '—'),
          score: Number(r['health_score'] ?? 0),
          issue: String(r['issue'] ?? ''),
          lastInspection: String(r['last_inspection'] ?? '—'),
          openWorkOrders: Number(r['open_work_orders'] ?? 0)
        }));
        return { empty: false, attention };
      }
      return { empty: false, table: { columns: t.columns.map((c) => c.label), rows: t.rows.map((r) => t.columns.map((c) => cell(r[c.key]))) } };
    }
    case 'map': {
      const m = data as {
        locations: { location: string; latitude: number | null; longitude: number | null; state: string; totalAssets: number }[];
        legend: { label: string; value: number }[];
      };
      const locations = m.locations ?? [];
      if (!locations.length) return { empty: true };
      const legend = (m.legend ?? []).map((l) => ({ label: l.label, color: STATE_COLORS[l.label] ?? RA_COLORS.purple, count: l.value }));
      const pinned = locations.filter((l) => l.latitude !== null && l.longitude !== null);
      // No service stores coordinates yet: without them the map widget lists the locations instead
      if (type === 'map' && pinned.length) {
        return {
          empty: false,
          legend,
          points: pinned.map((l) => ({ lat: l.latitude!, lng: l.longitude!, color: STATE_COLORS[l.state] ?? RA_COLORS.purple, label: `${l.location}: ${l.totalAssets} assets (${l.state})` }))
        };
      }
      return { empty: false, table: { columns: ['Location', 'State', 'Assets'], rows: locations.map((l) => [l.location, l.state, l.totalAssets]) } };
    }
    case 'matrix': {
      const h = data as { rows: string[]; columns: string[]; values: number[][]; max: number };
      if (!h.rows?.length) return { empty: true };
      if (type === 'table') return { empty: false, table: { columns: ['Location', ...h.columns], rows: h.rows.map((r, i) => [r, ...h.values[i].map(cell)]) } };
      return { empty: false, max: h.max, heat: { rows: h.rows, cols: h.columns, values: h.values } };
    }
    default:
      return { empty: true };
  }
}

