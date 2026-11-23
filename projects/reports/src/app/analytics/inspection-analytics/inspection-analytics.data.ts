import {
  CountBreakdownDto, InsightAssetDto, KpiDto, PeriodDto, SeriesDto, monthLabels, num, seriesValues
} from '../shared/analytics-api.service';

// Inspection figures come from the Report Analytics API: GET api/inspection-analytics/overview.

export const ALL_INSPECTION_TYPES = 'All inspection types';
export const ALL_INSPECTORS = 'All inspectors';

/** Default compliance target until the API reports the applied one. */
export const COMPLIANCE_TARGET = 90;

export type Severity = 'Critical' | 'High' | 'Medium' | 'Low';
export const SEVERITY_RANK: Record<Severity, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 };
export const SEVERITY_TONE: Record<Severity, string> = { Critical: 'red', High: 'pink', Medium: 'orange', Low: 'blue' };

// ===== API contracts (api/inspection-analytics) =====

/** FindingRow */
export interface IaFindingDto {
  id: string;
  findingId: string | null;
  assetRecordId: string | null;
  assetId: string | null;
  assetName: string | null;
  location: string | null;
  category: string;
  severity: string;
  description: string | null;
  raisedAt: string | null;
  dueDate: string | null;
  owner: string | null;
  status: string;
  isOverdue: boolean;
  ageDays: number | null;
  inspectionId: string | null;
}

export interface IaInsightDto {
  key: string;
  severity: string;
  title: string;
  message: string;
  count: number;
  findings: { id: string; findingId: string | null; assetId: string | null; assetName: string | null; severity: string; category: string | null; dueDate: string | null; reasons: string[] }[];
  assets: InsightAssetDto[];
  locations: string[];
  actions: string[];
  query: string;
}

export interface IaOverviewDto {
  generatedAt: string;
  filters: { complianceTarget: number; dueWithinDays: number; repeatWindowDays: number };
  period: PeriodDto;
  summary: { totalInspections: KpiDto; completionRate: KpiDto; openFindings: KpiDto; criticalFindings: KpiDto; correctiveActionsDue: KpiDto };
  completionTrend: SeriesDto;
  findingsBySeverity: SeriesDto;
  inspectionStatus: CountBreakdownDto;
  findingsByCategory: CountBreakdownDto;
  insights: IaInsightDto[];
  compliance: { value: number | null; target: number; meetsTarget: boolean | null; previousValue: number | null; change: number | null; compliantAssets: number; scheduledAssets: number; trend: SeriesDto };
  repeatFindingsTrend: SeriesDto;
  openFindings: IaFindingDto[];
  openFindingsTotal: number;
  inspectors: { inspector: string; inspections: number; completed: number; avgDurationHours: number | null; findingRate: number | null; closureRate: number | null }[];
}

export interface IaFiltersDto {
  organizations: { value: string; label: string }[];
  locations: { value: string; label: string }[];
  assetCategories: { value: string; label: string }[];
  assetTypes: { value: string; label: string }[];
  inspectionTypes: { value: string; label: string }[];
  inspectors: { value: string; label: string }[];
}

// ===== Page view model =====

export interface InspectionFinding {
  id: string;
  assetId: string;
  assetName: string;
  site: string;
  /** Asset category (not reported by the findings API) */
  category: string;
  /** Finding category */
  findingType: string;
  inspectionType: string;
  severity: Severity;
  description: string;
  dueInDays: number;
  due: string;
  owner: string;
  inspector: string;
  status: string;
}

export interface InspectionFilters {
  period: string;
  organization: string;
  location: string;
  category: string;
  type: string;
  inspector: string;
}

export interface InspectorRow {
  name: string;
  inspections: number;
  avgHours: number;
  findingRate: number;
  closureRate: number;
}

export interface InspectionView {
  labels: string[];
  planned: number[];
  completed: number[];
  completedPrevious: number[];
  overdue: number[];
  status: { completed: number; inProgress: number; scheduled: number; overdue: number };
  totalInspections: number;
  /** null when nothing was due in the period */
  completionRate: number | null;
  complianceTarget: number;
  rateSeries: number[];
  severity: Record<Severity, number[]>;
  findingTypes: string[];
  byFindingType: number[];
  repeated: number[];
  resolved: number[];
  findings: InspectionFinding[];
  openFindings: number;
  critical: number;
  actionsDue: number;
  inspectors: InspectorRow[];
  kpis: IaOverviewDto['summary'] | null;
  insights: IaInsightDto[];
}

export function emptyInspectionView(): InspectionView {
  return {
    labels: [], planned: [], completed: [], completedPrevious: [], overdue: [],
    status: { completed: 0, inProgress: 0, scheduled: 0, overdue: 0 },
    totalInspections: 0, completionRate: null, complianceTarget: COMPLIANCE_TARGET, rateSeries: [],
    severity: { Critical: [], High: [], Medium: [], Low: [] }, findingTypes: [], byFindingType: [], repeated: [], resolved: [],
    findings: [], openFindings: 0, critical: 0, actionsDue: 0, inspectors: [], kpis: null, insights: []
  };
}

const DAY = 86_400_000;
const round1 = (v: number) => Math.round(v * 10) / 10;

function toSeverity(s: string): Severity {
  return (['Critical', 'High', 'Medium', 'Low'] as Severity[]).find((x) => x.toLowerCase() === (s ?? '').toLowerCase()) ?? 'Low';
}

export function toFinding(r: IaFindingDto, today = new Date()): InspectionFinding {
  const due = r.dueDate ? new Date(r.dueDate) : null;
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime();
  return {
    id: r.findingId ?? r.id,
    assetId: r.assetId ?? '—',
    assetName: r.assetName ?? r.assetId ?? '—',
    site: r.location ?? '—',
    category: '—',
    findingType: r.category,
    inspectionType: '—',
    severity: toSeverity(r.severity),
    description: r.description ?? '',
    dueInDays: due ? Math.floor((due.getTime() - start) / DAY) : 0,
    due: due ? due.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '—',
    owner: r.owner ?? '—',
    inspector: '—',
    status: r.status
  };
}

/** Maps GET /overview (and, for Compare Periods, the previous period's overview) to the page. */
export function mapInspection(o: IaOverviewDto, previous: IaOverviewDto | null): InspectionView {
  const status = (label: string) => o.inspectionStatus.items.find((i) => i.label === label)?.count ?? 0;
  const sev = (name: Severity) => seriesValues(o.findingsBySeverity, name);
  const s = o.summary;
  return {
    labels: monthLabels(o.completionTrend),
    planned: seriesValues(o.completionTrend, 'Planned'),
    completed: seriesValues(o.completionTrend, 'Completed'),
    completedPrevious: previous ? seriesValues(previous.completionTrend, 'Completed') : [],
    overdue: seriesValues(o.completionTrend, 'Overdue'),
    status: { completed: status('Completed'), inProgress: status('In Progress'), scheduled: status('Scheduled'), overdue: status('Overdue') },
    totalInspections: num(s.totalInspections.value),
    completionRate: typeof s.completionRate.value === 'number' ? s.completionRate.value : null,
    complianceTarget: o.compliance.target,
    rateSeries: seriesValues(o.compliance.trend, 'Compliance (%)'),
    severity: { Critical: sev('Critical'), High: sev('High'), Medium: sev('Medium'), Low: sev('Low') },
    findingTypes: o.findingsByCategory.items.map((i) => i.label),
    byFindingType: o.findingsByCategory.items.map((i) => i.count),
    repeated: seriesValues(o.repeatFindingsTrend, 'Repeated Defects'),
    resolved: seriesValues(o.repeatFindingsTrend, 'Resolved Defects'),
    findings: o.openFindings.map((f) => toFinding(f)),
    openFindings: num(s.openFindings.value),
    critical: num(s.criticalFindings.value),
    actionsDue: num(s.correctiveActionsDue.value),
    inspectors: o.inspectors.map((i) => ({
      name: i.inspector,
      inspections: i.completed,
      avgHours: round1(num(i.avgDurationHours)),
      findingRate: round1(num(i.findingRate)),
      closureRate: round1(num(i.closureRate))
    })),
    kpis: s,
    insights: o.insights
  };
}
