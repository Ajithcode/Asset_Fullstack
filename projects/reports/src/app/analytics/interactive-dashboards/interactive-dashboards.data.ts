import { ChartSeries, ChartSlice, HBarRow } from '../shared/charts';
import { MapPoint } from '../iot-utilization-analytics/iot-site-map/iot-site-map';

// Dashboard definitions and widget data. Dashboards, metrics and widget figures come from /api/interactive-dashboards
// (see DashboardStore, which maps the API shapes to these view models).

export type WidgetType = 'kpi' | 'line' | 'bar' | 'donut' | 'table' | 'map' | 'heatmap';
export type WidgetSpan = 3 | 4 | 5 | 6 | 7 | 8 | 12;

/** API metric key (GET /api/interactive-dashboards/options metrics), e.g. total_assets */
export type MetricKey = string;

export interface WidgetConfig {
  id: string;
  type: WidgetType;
  metric: MetricKey;
  title: string;
  span: WidgetSpan;
  /** Grid rows (API position h); the default height of the widget type when not set */
  h?: number;
}

export type Sharing = 'private' | 'people' | 'organization';
export type DashboardRole = 'Owner' | 'Editor' | 'Viewer';

export interface Dashboard {
  id: string;
  name: string;
  description: string;
  owner: string;
  sharing: Sharing;
  sharedWith: string[];
  groups: string[];
  role: DashboardRole;
  favorite: boolean;
  updatedAt: string;
  widgets: WidgetConfig[];
  /** Widget count from the list (widgets are only loaded for the open dashboard) */
  widgetCount: number;
  /** Owner: may share and delete */
  canShare: boolean;
  /** Default filters as saved (sent back unchanged when the whole dashboard is replaced) */
  defaultFilters: Record<string, unknown> | null;
}

export interface DashFilters {
  period: string;
  organization: string;
  location: string;
  category: string;
  compare: boolean;
}

export interface MetricDef {
  key: MetricKey;
  label: string;
  description: string;
  types: WidgetType[];
}

export interface WidgetTypeDef {
  type: WidgetType;
  label: string;
  span: WidgetSpan;
  /** Grid rows (API position h); the default height of the widget type when not set */
  h?: number;
}

export const WIDGET_TYPES: WidgetTypeDef[] = [
  { type: 'kpi', label: 'KPI Card', span: 3 },
  { type: 'line', label: 'Line Chart', span: 6 },
  { type: 'bar', label: 'Bar Chart', span: 6 },
  { type: 'donut', label: 'Donut Chart', span: 4 },
  { type: 'table', label: 'Table', span: 12 },
  { type: 'map', label: 'Map', span: 4 },
  { type: 'heatmap', label: 'Heatmap', span: 12 }
];

export const WIDGET_SIZES: { span: WidgetSpan; label: string }[] = [
  { span: 3, label: 'Small (1/4 width)' },
  { span: 4, label: 'Medium (1/3 width)' },
  { span: 6, label: 'Half width' },
  { span: 8, label: 'Large (2/3 width)' },
  { span: 12, label: 'Full width' }
];

/** Metrics a widget can show, with the widget types that suit each one: GET /api/interactive-dashboards/options (filled in place). */
export const METRICS: MetricDef[] = [];

export function metricDef(key: MetricKey): MetricDef | undefined {
  return METRICS.find((m) => m.key === key);
}

// ===== Saved dashboards =====

/** Groups a dashboard can be shared with (populated from the API). */
export const DASHBOARD_GROUPS: string[] = [];

// ===== Widget data =====

export interface KpiData {
  value: string;
  prev: string;
  delta: number;
  good: boolean;
  tone: string;
}

export interface AttentionAsset {
  id: string;
  name: string;
  category: string;
  site: string;
  score: number;
  issue: string;
  lastInspection: string;
  openWorkOrders: number;
}

export interface WidgetData {
  empty: boolean;
  /** Why the widget is empty (API error); default text otherwise */
  message?: string;
  kpi?: KpiData;
  labels?: string[];
  series?: ChartSeries[];
  format?: (v: number) => string;
  max?: number;
  stacked?: boolean;
  slices?: ChartSlice[];
  centerValue?: string;
  centerLabel?: string;
  hbars?: HBarRow[];
  table?: { columns: string[]; rows: (string | number)[][] };
  attention?: AttentionAsset[];
  points?: MapPoint[];
  legend?: { label: string; color: string; count: number }[];
  heat?: { rows: string[]; cols: string[]; values: number[][] };
}

/** Rows for a widget's CSV export. */
export function widgetCsv(config: WidgetConfig, data: WidgetData): { header: string[]; rows: (string | number)[][] } {
  if (data.kpi) return { header: ['Metric', 'Value', 'Previous Period', 'Change %'], rows: [[config.title, data.kpi.value, data.kpi.prev, data.kpi.delta]] };
  if (data.table) return { header: data.table.columns, rows: data.table.rows };
  if (data.attention) {
    return { header: ['Asset ID', 'Asset Name', 'Category', 'Location', 'Health Score', 'Issue'], rows: data.attention.map((a) => [a.id, a.name, a.category, a.site, a.score, a.issue]) };
  }
  if (data.slices) return { header: ['Item', 'Value'], rows: data.slices.map((x) => [x.name, x.value]) };
  if (data.hbars) return { header: ['Item', 'Value'], rows: data.hbars.map((x) => [x.label, x.value]) };
  if (data.legend) return { header: ['Status', 'Assets'], rows: data.legend.map((x) => [x.label, x.count]) };
  if (data.heat) {
    const heat = data.heat;
    return { header: ['Site', ...heat.cols], rows: heat.rows.map((r, i) => [r, ...heat.values[i]]) };
  }
  if (data.labels && data.series) {
    const series = data.series;
    return { header: ['Label', ...series.map((x) => x.name)], rows: data.labels.map((l, i) => [l.replace('\n', ' '), ...series.map((x) => x.values[i])]) };
  }
  return { header: ['Message'], rows: [['No data for the current filters']] };
}
