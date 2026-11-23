import { RA_COLORS } from '../shared/charts/chart-utils';
import { CountBreakdownDto, KpiDto, PeriodDto, SeriesDto, kpiChange, monthLabels, num, seriesValues } from '../shared/analytics-api.service';
import { PeriodWindow } from '../inventory-analytics/parts/ra-parts';

// Production analytics come from the Report Analytics API: GET api/production-analytics/overview.

export interface ProductionLine {
  name: string;
  site: string;
  /** Number of work centers on the line */
  workCenters: number;
  utilization: number;
  target: number;
}

/** Default utilization target until the API reports the applied one. */
export const UTILIZATION_TARGET = 85;

export interface WorkCenter {
  code: string;
  name: string;
  queue: number;
  inProgress: number;
  /** Average wait in hours */
  avgWait: number;
  capacity: number;
}

export const WIP_STAGES = ['Raw Material', 'In Process', 'Quality Inspection', 'Ready for Dispatch'];
export const WIP_STAGE_COLORS = [RA_COLORS.blue, RA_COLORS.violet, RA_COLORS.orange, RA_COLORS.pink];

export const WORK_ORDER_STATUSES = ['Not Started', 'In Progress', 'On Hold', 'Completed'];
export const WORK_ORDER_COLORS = [RA_COLORS.blue, RA_COLORS.green, RA_COLORS.orange, RA_COLORS.pink];

export const DOWNTIME_CAUSES = ['Mechanical', 'Material Shortage', 'Changeover', 'Quality Hold', 'Unplanned'];

// ===== Active production orders =====

export type OrderStatus = 'In Progress' | 'On Track' | 'At Risk' | 'Quality Hold';

export interface ProductionOrder {
  id: string;
  code: string;
  product: string;
  line: string;
  site: string;
  workCenter: string;
  planned: number;
  completed: number;
  wip: number;
  wipValue: number;
  due: string;
  status: OrderStatus;
  /** API status / stage / schedule behind `status` */
  orderStatus: string;
  stage: string;
  schedule: string;
  unit: string;
  progress: number;
  priority: string;
}

export const ALL_PLANTS = 'All plants';
export const ALL_LINES = 'All production lines';
export const ALL_WORK_CENTERS = 'All work centers';
export const ALL_PRODUCTS = 'All products';

// ===== API contracts (api/production-analytics) =====

/** ProductionOrderRow */
export interface PaOrderDto {
  id: string;
  orderNo: string | null;
  product: string;
  plant: string;
  line: string;
  workCenter: string;
  assetId: string | null;
  plannedQty: number | null;
  completedQty: number | null;
  wipQty: number | null;
  unit: string;
  progress: number;
  dueDate: string | null;
  status: string;
  stage: string;
  schedule: string;
  wipValue: number;
  priority: string | null;
}

export interface PaInsightDto {
  key: string;
  severity: string;
  title: string;
  message: string;
  count: number;
  value: number | null;
  orders: { id: string; orderNo: string | null; product: string; line: string; dueDate: string | null; progress: number; schedule: string }[];
  lines: string[];
  actions: string[];
  /** Query string for GET /orders ("schedule=At%20Risk%2CDelayed", "productionLine=…") */
  query: string;
}

export interface PaOverviewDto {
  generatedAt: string;
  filters: { utilizationTarget: number; outputTarget: number | null; atRiskGap: number };
  period: PeriodDto;
  summary: { productionOutput: KpiDto; outputUnit: string; wipValue: KpiDto; onTimeCompletion: KpiDto; equipmentUtilization: KpiDto; oee: KpiDto };
  outputTrend: SeriesDto;
  wipByStage: { currentValue: number; current: { label: string; value: number; percentage: number }[]; trend: SeriesDto };
  workOrderStatus: CountBreakdownDto;
  utilizationByLine: { line: string; workCenters: number; utilization: number | null; target: number; meetsTarget: boolean | null }[];
  insights: PaInsightDto[];
  downtimeByCause: { totalHours: number; causes: { cause: string; hours: number; percentage: number }[]; trend: SeriesDto };
  efficiencyTrend: SeriesDto;
  activeOrders: PaOrderDto[];
  activeOrdersTotal: number;
  bottlenecks: { workCenter: string; line: string; queue: number; inProgress: number; avgWaitDays: number | null; utilization: number | null; risk: string }[];
}

export interface PaFiltersDto {
  organizations: { value: string; label: string }[];
  plants: string[];
  productionLines: string[];
  workCenters: string[];
  products: string[];
}

// ===== Page view model =====

export interface ProductionView {
  window: PeriodWindow;
  hasScope: boolean;
  output: number;
  outputUnit: string;
  wipValue: number;
  onTime: number | null;
  utilization: number | null;
  oee: number | null;
  deltas: { output: number; wip: number; onTime: number; utilization: number; oee: number };
  previous: { output: number | null; wip: number | null; onTime: number | null; utilization: number | null; oee: number | null };
  utilizationTarget: number;
  actual: number[];
  planned: number[];
  target: number[];
  stageStacks: number[][];
  workOrders: number[];
  lines: ProductionLine[];
  downtime: number[];
  availability: number[];
  performance: number[];
  quality: number[];
  oeeTrend: number[];
  orders: ProductionOrder[];
  ordersTotal: number;
  workCenters: (WorkCenter & { risk: 'High' | 'Medium' | 'Low' })[];
  insights: PaInsightDto[];
}

export function emptyProductionView(caption: string): ProductionView {
  return {
    window: { labels: [], months: [], weekly: false, monthCount: 0, caption },
    hasScope: false, output: 0, outputUnit: 'units', wipValue: 0, onTime: null, utilization: null, oee: null,
    deltas: { output: 0, wip: 0, onTime: 0, utilization: 0, oee: 0 },
    previous: { output: null, wip: null, onTime: null, utilization: null, oee: null },
    utilizationTarget: UTILIZATION_TARGET,
    actual: [], planned: [], target: [], stageStacks: WIP_STAGES.map(() => []), workOrders: WORK_ORDER_STATUSES.map(() => 0), lines: [],
    downtime: DOWNTIME_CAUSES.map(() => 0), availability: [], performance: [], quality: [], oeeTrend: [], orders: [], ordersTotal: 0, workCenters: [], insights: []
  };
}

const round1 = (v: number) => Math.round(v * 10) / 10;

/** The page's order status: quality hold, else the API schedule (Delayed counts as At Risk). */
function orderStatus(r: PaOrderDto): OrderStatus {
  if (r.stage === 'Quality Inspection' && r.status === 'On Hold') return 'Quality Hold';
  if (r.schedule === 'At Risk' || r.schedule === 'Delayed') return 'At Risk';
  if (r.schedule === 'On Track') return 'On Track';
  return 'In Progress';
}

export function toOrder(r: PaOrderDto): ProductionOrder {
  return {
    id: r.id,
    code: r.orderNo ?? r.id,
    product: r.product,
    line: r.line,
    site: r.plant,
    workCenter: r.workCenter,
    planned: num(r.plannedQty),
    completed: num(r.completedQty),
    wip: num(r.wipQty),
    wipValue: num(r.wipValue),
    due: r.dueDate ? r.dueDate.slice(0, 10) : '',
    status: orderStatus(r),
    orderStatus: r.status,
    stage: r.stage,
    schedule: r.schedule,
    unit: r.unit,
    progress: num(r.progress),
    priority: r.priority ?? '—'
  };
}

const kpi = (k: KpiDto) => (typeof k.value === 'number' ? k.value : null);
const prev = (k: KpiDto) => (typeof k.previousValue === 'number' ? k.previousValue : null);

export function mapProduction(o: PaOverviewDto, caption: string): ProductionView {
  const s = o.summary;
  const labels = monthLabels(o.outputTrend);
  const risk = (r: string): 'High' | 'Medium' | 'Low' => (r === 'High' || r === 'Medium' ? r : 'Low');
  return {
    window: { labels, months: labels.map((_, i) => i), weekly: false, monthCount: labels.length, caption },
    hasScope: o.workOrderStatus.total > 0 || o.utilizationByLine.length > 0 || o.activeOrdersTotal > 0,
    output: num(s.productionOutput.value),
    outputUnit: s.outputUnit || 'units',
    wipValue: num(s.wipValue.value),
    onTime: kpi(s.onTimeCompletion),
    utilization: kpi(s.equipmentUtilization),
    oee: kpi(s.oee),
    deltas: {
      output: kpiChange(s.productionOutput), wip: kpiChange(s.wipValue), onTime: kpiChange(s.onTimeCompletion),
      utilization: kpiChange(s.equipmentUtilization), oee: kpiChange(s.oee)
    },
    previous: { output: prev(s.productionOutput), wip: prev(s.wipValue), onTime: prev(s.onTimeCompletion), utilization: prev(s.equipmentUtilization), oee: prev(s.oee) },
    utilizationTarget: o.filters.utilizationTarget,
    actual: seriesValues(o.outputTrend, 'Actual'),
    planned: seriesValues(o.outputTrend, 'Planned'),
    target: seriesValues(o.outputTrend, 'Target'),
    stageStacks: WIP_STAGES.map((stage) => seriesValues(o.wipByStage.trend, stage)),
    workOrders: WORK_ORDER_STATUSES.map((st) => o.workOrderStatus.items.find((i) => i.label === st)?.count ?? 0),
    lines: o.utilizationByLine.map((l) => ({ name: l.line, site: '', workCenters: l.workCenters, utilization: round1(num(l.utilization)), target: l.target })),
    downtime: DOWNTIME_CAUSES.map((c) => round1(o.downtimeByCause.causes.find((x) => x.cause === c)?.hours ?? 0)),
    availability: seriesValues(o.efficiencyTrend, 'Availability'),
    performance: seriesValues(o.efficiencyTrend, 'Performance'),
    quality: seriesValues(o.efficiencyTrend, 'Quality'),
    oeeTrend: seriesValues(o.efficiencyTrend, 'OEE'),
    orders: o.activeOrders.map(toOrder),
    ordersTotal: o.activeOrdersTotal,
    workCenters: o.bottlenecks.map((b) => ({
      code: b.workCenter,
      name: b.line,
      queue: b.queue,
      inProgress: b.inProgress,
      avgWait: round1(num(b.avgWaitDays) * 24),
      capacity: round1(num(b.utilization)),
      risk: risk(b.risk)
    })),
    insights: o.insights
  };
}
