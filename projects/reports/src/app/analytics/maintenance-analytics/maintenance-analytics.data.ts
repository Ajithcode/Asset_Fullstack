import {
  CountBreakdownDto, DowntimeCauseDto, InsightAssetDto, KpiDto, PeriodDto, ReliabilityDto, SeriesDto, kpiChange, monthLabels, num, seriesValues
} from '../shared/analytics-api.service';
import { ChartSeries, ChartSlice, RA_COLORS } from '../shared/charts';
import { WO_TYPES } from '../asset-performance/kit/asset-universe';

// Maintenance Analytics figures come from the Report Analytics API: GET api/maintenance-analytics/overview.

export const ALL_MAINTENANCE_TYPES = 'All maintenance types';
export const MAINTENANCE_TYPES = [ALL_MAINTENANCE_TYPES, ...WO_TYPES];
/** Default PM compliance target until the API reports the applied one. */
export const PM_TARGET = 90;

export const PRIORITY_RANK: Record<string, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 };

const TYPE_COLORS: Record<string, string> = {
  Preventive: RA_COLORS.blue, Corrective: RA_COLORS.orange, Emergency: RA_COLORS.red, Inspection: RA_COLORS.violet, Other: RA_COLORS.grey
};
const STATUS_COLORS: Record<string, string> = {
  Open: RA_COLORS.blue, 'In Progress': RA_COLORS.orange, 'On Hold': RA_COLORS.pink, Completed: RA_COLORS.green, Overdue: RA_COLORS.red, Cancelled: RA_COLORS.grey
};
const CAUSE_COLORS = [RA_COLORS.red, RA_COLORS.orange, RA_COLORS.blue, RA_COLORS.violet, RA_COLORS.teal];

// ===== API contracts (api/maintenance-analytics) =====

/** BacklogRow */
export interface MaBacklogDto {
  id: string;
  workOrderId: string | null;
  workType: string;
  priority: string;
  assetRecordId: string | null;
  assetId: string | null;
  assetName: string | null;
  location: string | null;
  ageDays: number | null;
  assignedTo: string | null;
  status: string;
  dueDate: string | null;
  isOverdue: boolean;
  partsShortage: boolean;
}

export interface MaTechnicianDto {
  technician: string;
  technicianId: string | null;
  assigned: number;
  completed: number;
  firstTimeFixRate: number | null;
  avgResponseHours: number | null;
  slaCompliance: number | null;
}

export interface MaInsightDto {
  key: string;
  severity: string;
  title: string;
  message: string;
  count: number;
  workOrders: { id: string; workOrderId: string | null; assetId: string | null; assetName: string | null; priority: string | null; status: string; reasons: string[] }[];
  assets: InsightAssetDto[];
  parts: string[];
  actions: string[];
  query: string;
}

export interface MaOverviewDto {
  generatedAt: string;
  filters: { complianceTarget: number; emergencyCostTarget: number | null; failureHealthThreshold: number };
  period: PeriodDto;
  summary: { maintenanceCost: KpiDto; openWorkOrders: KpiDto; preventiveCompliance: KpiDto; mttr: KpiDto; mtbf: KpiDto };
  costTrend: {
    trend: SeriesDto;
    totals: { planned: number; corrective: number; emergency: number; unassigned: number; total: number; laborCost: number; sparePartsCost: number; budget: number | null };
  };
  workOrdersByType: CountBreakdownDto;
  workOrderStatus: CountBreakdownDto;
  insights: MaInsightDto[];
  preventiveCompliance: { value: number | null; target: number; meetsTarget: boolean | null; previousValue: number | null; change: number | null; completed: number; due: number; trend: SeriesDto };
  reliability: ReliabilityDto;
  downtimeByCause: DowntimeCauseDto;
  backlog: MaBacklogDto[];
  backlogTotal: number;
  technicians: MaTechnicianDto[];
}

/** GET /compare-periods: the cost trend of the comparison period. */
export interface MaCompareDto {
  compareCostTrend: SeriesDto;
}

export interface MaFiltersDto {
  organizations: { value: string; label: string }[];
  locations: { value: string; label: string }[];
  assetCategories: { value: string; label: string }[];
  assetTypes: { value: string; label: string }[];
}

// ===== Page view model =====

export interface BacklogRow {
  id: string;
  type: string;
  priority: string;
  status: string;
  assetId: string;
  asset: string;
  location: string;
  ageDays: number;
  technician: string;
  due: string;
  partsShortage: boolean;
}

export interface TechnicianRow {
  name: string;
  assigned: number;
  completed: number;
  firstTimeFix: number;
  response: number;
  sla: number;
}

export interface MaintenanceView {
  /** Work orders in the period for the current filters */
  total: number;
  cost: number;
  open: number;
  pmCompliance: number | null;
  pmTarget: number;
  mttr: number | null;
  mtbf: number | null;
  deltas: { cost: number; open: number; pm: number; mttr: number; mtbf: number };
  labels: string[];
  costTrend: ChartSeries[];
  monthlyEmergency: number[];
  monthlyTotal: number[];
  typeSeries: ChartSeries[];
  statusSlices: ChartSlice[];
  pmTrend: ChartSeries[];
  reliability: ChartSeries[];
  downtimeByCause: ChartSeries[];
  backlog: BacklogRow[];
  backlogTotal: number;
  technicians: TechnicianRow[];
  emergencyCost: number;
  emergencyTarget: number | null;
  insights: MaInsightDto[];
}

export function emptyMaintenance(): MaintenanceView {
  return {
    total: 0, cost: 0, open: 0, pmCompliance: null, pmTarget: PM_TARGET, mttr: null, mtbf: null,
    deltas: { cost: 0, open: 0, pm: 0, mttr: 0, mtbf: 0 },
    labels: [], costTrend: [], monthlyEmergency: [], monthlyTotal: [], typeSeries: [], statusSlices: [], pmTrend: [], reliability: [], downtimeByCause: [],
    backlog: [], backlogTotal: 0, technicians: [], emergencyCost: 0, emergencyTarget: null, insights: []
  };
}

const round1 = (v: number) => Math.round(v * 10) / 10;

export function toBacklogRow(r: MaBacklogDto): BacklogRow {
  return {
    id: r.workOrderId ?? r.id,
    type: r.workType,
    priority: r.priority,
    status: r.status,
    assetId: r.assetId ?? '—',
    asset: r.assetName ?? r.assetId ?? '—',
    location: r.location ?? '—',
    ageDays: num(r.ageDays),
    technician: r.assignedTo ?? 'Unassigned',
    due: r.dueDate ? new Date(r.dueDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—',
    partsShortage: r.partsShortage
  };
}

/** Maps GET /overview (and, for Compare Periods, the comparison period's cost trend) to the page. */
export function mapMaintenance(o: MaOverviewDto, compare: MaCompareDto | null): MaintenanceView {
  const s = o.summary;
  const cost = (name: string) => seriesValues(o.costTrend.trend, name);
  const costTrend: ChartSeries[] = [
    { name: 'Planned', values: cost('Planned'), color: RA_COLORS.blue, type: 'bar' },
    { name: 'Corrective', values: cost('Corrective'), color: RA_COLORS.orange, type: 'bar' },
    { name: 'Emergency', values: cost('Emergency'), color: RA_COLORS.red, type: 'bar' },
    { name: 'Total Cost', values: cost('Total Cost'), color: RA_COLORS.purple, type: 'line' }
  ];
  if (compare) costTrend.push({ name: 'Total (previous period)', values: seriesValues(compare.compareCostTrend, 'Total Cost'), color: RA_COLORS.purple, type: 'line', dashed: true });

  // One coloured column per type: each series only fills its own column
  const types = o.workOrdersByType.items;
  const typeSeries: ChartSeries[] = types.map((t, i) => ({
    name: t.label,
    values: types.map((_, j) => (i === j ? t.count : 0)),
    color: TYPE_COLORS[t.label] ?? RA_COLORS.grey
  }));

  const pc = o.preventiveCompliance;
  const pmTrend: ChartSeries[] = [
    { name: 'PM Compliance', values: seriesValues(pc.trend, 'Compliance (%)'), color: RA_COLORS.green, area: true },
    { name: `Target ${pc.target}%`, values: seriesValues(pc.trend, 'Target (%)'), color: RA_COLORS.grey, dashed: true }
  ];

  const kpi = (k: KpiDto) => (typeof k.value === 'number' ? k.value : null);
  return {
    total: o.workOrdersByType.total,
    cost: num(s.maintenanceCost.value),
    open: num(s.openWorkOrders.value),
    pmCompliance: kpi(s.preventiveCompliance),
    pmTarget: pc.target,
    mttr: kpi(s.mttr),
    mtbf: kpi(s.mtbf),
    deltas: {
      cost: kpiChange(s.maintenanceCost), open: kpiChange(s.openWorkOrders), pm: kpiChange(s.preventiveCompliance), mttr: kpiChange(s.mttr), mtbf: kpiChange(s.mtbf)
    },
    labels: monthLabels(o.costTrend.trend),
    costTrend,
    monthlyEmergency: cost('Emergency'),
    monthlyTotal: cost('Total Cost'),
    typeSeries,
    statusSlices: o.workOrderStatus.items.map((i) => ({ name: i.label, value: i.count, color: STATUS_COLORS[i.label] ?? RA_COLORS.grey })),
    pmTrend,
    reliability: [
      { name: 'MTTR (hrs)', values: seriesValues(o.reliability.trend, 'MTTR (hrs)'), color: RA_COLORS.purple },
      { name: 'MTBF (hrs)', values: seriesValues(o.reliability.trend, 'MTBF (hrs)'), color: RA_COLORS.blue, axis: 'right' }
    ],
    downtimeByCause: o.downtimeByCause.trend.series.map((line, i) => ({ name: line.name, values: line.values.map(num), color: CAUSE_COLORS[i % CAUSE_COLORS.length] })),
    backlog: o.backlog.map(toBacklogRow),
    backlogTotal: o.backlogTotal,
    technicians: o.technicians.map((t) => ({
      name: t.technician,
      assigned: t.assigned,
      completed: t.completed,
      firstTimeFix: round1(num(t.firstTimeFixRate)),
      response: round1(num(t.avgResponseHours)),
      sla: round1(num(t.slaCompliance))
    })),
    emergencyCost: num(o.costTrend.totals.emergency),
    emergencyTarget: o.filters.emergencyCostTarget,
    insights: o.insights
  };
}
