import {
  InsightAssetDto, KpiDto, PeriodDto, ReliabilityDto, SeriesDto, kpiChange, monthLabels, num, seriesValues
} from '../shared/analytics-api.service';
import { ChartSeries, HBarRow, RA_COLORS } from '../shared/charts';
import { RaAsset, RaSite, round1 } from './kit/asset-universe';

// Asset Performance figures come from the Report Analytics API: GET api/asset-performance/overview.

/** Default utilization target until the API reports the applied one. */
export const UTILIZATION_TARGET = 60;

// ===== API contracts (api/asset-performance) =====

/** AssetPerformanceRow */
export interface ApAssetDto {
  id: string;
  assetId: string | null;
  assetName: string | null;
  category: string | null;
  assetType: string | null;
  location: string | null;
  healthScore: number | null;
  healthStatus: string | null;
  utilization: number | null;
  availability: number | null;
  mtbfHours: number | null;
  mttrHours: number | null;
  downtimeHours: number;
  plannedDowntimeHours: number;
  unplannedDowntimeHours: number;
  failures: number;
  performanceScore: number | null;
  performanceBand: string | null;
  status: string;
}

export interface ApLocationDto {
  location: string;
  assetCount: number;
  averageHealthScore: number | null;
  utilization: number | null;
  availability: number | null;
  performanceScore: number | null;
  status: string;
}

export interface ApInsightDto {
  key: string;
  severity: string;
  title: string;
  message: string;
  count: number;
  assets: InsightAssetDto[];
  locations: string[];
  actions: string[];
  /** Query string for GET /assets ("healthBelow=60", "interventionOnly=true") */
  query: string;
}

export interface ApOverviewDto {
  generatedAt: string;
  filters: { viewId: string | null; healthThreshold: number; utilizationTarget: number };
  period: PeriodDto;
  summary: { totalAssets: KpiDto; averageHealthScore: KpiDto; utilizationRate: KpiDto; availability: KpiDto; mtbf: KpiDto; mttr: KpiDto };
  healthTrend: SeriesDto;
  utilizationByCategory: { category: string; assetCount: number; active: number; idle: number; maintenance: number; utilization: number }[];
  insights: ApInsightDto[];
  scoreDistribution: { bands: { band: string; min: number; max: number; count: number; percentage: number }[]; ratedAssets: number; notRated: number };
  reliability: ReliabilityDto;
  downtime: { plannedHours: number; unplannedHours: number; totalHours: number; events: number; trend: SeriesDto };
  topBottom: { top: ApAssetDto[]; bottom: ApAssetDto[] };
  locations: ApLocationDto[];
}

export interface ApFiltersDto {
  organizations: { value: string; label: string }[];
  locations: { value: string; label: string }[];
  assetCategories: { value: string; label: string }[];
  assetTypes: { value: string; label: string }[];
}

// ===== Page view model =====

export interface ApLocationRow {
  site: RaSite;
  name: string;
  assets: number;
  health: number;
  utilization: number;
  availability: number;
  status: string;
}

export interface ApAssetRow {
  asset: RaAsset;
  id: string;
  name: string;
  location: string;
  health: number;
  utilization: number;
  availability: number;
  status: 'Top Performer' | 'Underperforming';
}

export interface ApBands {
  excellent: number;
  good: number;
  watch: number;
  critical: number;
}

export interface AssetPerformanceView {
  total: number;
  /** null when no asset in scope has a score yet */
  health: number | null;
  utilization: number | null;
  availability: number | null;
  mtbf: number | null;
  mttr: number | null;
  deltas: { total: number; health: number; utilization: number; availability: number; mtbf: number; mttr: number };
  labels: string[];
  healthTrend: ChartSeries[];
  categoryLabels: string[];
  categoryUtilization: ChartSeries[];
  bands: ApBands;
  distribution: HBarRow[];
  reliability: ChartSeries[];
  downtime: ChartSeries[];
  downtimeHours: number;
  topBottom: ApAssetRow[];
  locations: ApLocationRow[];
  insights: ApInsightDto[];
  healthThreshold: number;
  utilizationTarget: number;
  generatedAt: string | null;
}

export function emptyAssetPerformance(): AssetPerformanceView {
  return {
    total: 0, health: null, utilization: null, availability: null, mtbf: null, mttr: null,
    deltas: { total: 0, health: 0, utilization: 0, availability: 0, mtbf: 0, mttr: 0 },
    labels: [], healthTrend: [], categoryLabels: [], categoryUtilization: [], bands: { excellent: 0, good: 0, watch: 0, critical: 0 },
    distribution: [], reliability: [], downtime: [], downtimeHours: 0, topBottom: [], locations: [], insights: [],
    healthThreshold: 60, utilizationTarget: UTILIZATION_TARGET, generatedAt: null
  };
}

const site = (name: string | null, assets = 0): RaSite => ({ name: name ?? '—', code: '', org: '', assets });

/** An API asset row in the shape the dialogs use. */
export function toRaAsset(r: Pick<ApAssetDto, 'id' | 'assetId' | 'assetName' | 'location'> & Partial<ApAssetDto>): RaAsset {
  return {
    id: r.assetId ?? r.id,
    name: r.assetName ?? r.assetId ?? r.id,
    site: site(r.location),
    category: r.category ?? '—',
    type: r.assetType ?? '—',
    owner: '—',
    health: round1(num(r.healthScore)),
    utilization: round1(num(r.utilization)),
    availability: round1(num(r.availability)),
    mtbf: Math.round(num(r.mtbfHours)),
    monitored: false,
    risk: null,
    riskScore: 0,
    rulHours: 0,
    failureWindowDays: null,
    confidence: 0,
    vibrationAnomaly: false
  };
}

export function insightAsset(a: InsightAssetDto): RaAsset {
  return toRaAsset({ id: a.id, assetId: a.assetId, assetName: a.assetName, location: a.location, healthScore: a.healthScore });
}

const kpi = (k: KpiDto | undefined) => (typeof k?.value === 'number' ? k.value : null);

/** Maps GET /overview (and, for Compare Periods, the previous period's overview) to the page. */
export function mapAssetPerformance(o: ApOverviewDto, previous: ApOverviewDto | null): AssetPerformanceView {
  const s = o.summary;
  const health = (name: string) => seriesValues(o.healthTrend, name);
  const healthTrend: ChartSeries[] = [
    { name: 'Healthy', values: health('Healthy'), color: RA_COLORS.green, area: true },
    { name: 'At Risk', values: health('At Risk'), color: RA_COLORS.orange },
    { name: 'Critical', values: health('Critical'), color: RA_COLORS.red }
  ];
  if (previous) healthTrend.push({ name: 'Healthy (previous period)', values: seriesValues(previous.healthTrend, 'Healthy'), color: RA_COLORS.green, dashed: true });

  const cats = o.utilizationByCategory;
  const categoryUtilization: ChartSeries[] = [
    { name: 'Active', values: cats.map((c) => round1(c.active)), color: RA_COLORS.purple },
    { name: 'Idle', values: cats.map((c) => round1(c.idle)), color: RA_COLORS.sky },
    { name: 'Maintenance', values: cats.map((c) => round1(c.maintenance)), color: RA_COLORS.orange }
  ];

  const bandColors: Record<string, string> = { Excellent: RA_COLORS.green, Good: RA_COLORS.blue, Watch: RA_COLORS.orange, Critical: RA_COLORS.red };
  const band = (name: string) => o.scoreDistribution.bands.find((b) => b.band === name)?.count ?? 0;
  const distribution: HBarRow[] = o.scoreDistribution.bands.map((b) => ({
    label: `${b.band} (${b.min}–${b.max})`,
    value: b.count,
    display: `${b.count} (${Math.round(b.percentage)}%)`,
    color: bandColors[b.band] ?? RA_COLORS.grey
  }));

  const reliability: ChartSeries[] = [
    { name: 'MTBF (hrs)', values: seriesValues(o.reliability.trend, 'MTBF (hrs)'), color: RA_COLORS.purple, area: true },
    { name: 'MTTR (hrs)', values: seriesValues(o.reliability.trend, 'MTTR (hrs)'), color: RA_COLORS.blue, axis: 'right' }
  ];
  if (previous) reliability.push({ name: 'MTBF (previous period)', values: seriesValues(previous.reliability.trend, 'MTBF (hrs)'), color: RA_COLORS.purple, dashed: true });

  const row = (r: ApAssetDto, status: ApAssetRow['status']): ApAssetRow => {
    const asset = toRaAsset(r);
    return { asset, id: asset.id, name: asset.name, location: asset.site.name, health: asset.health, utilization: asset.utilization, availability: asset.availability, status };
  };
  const topIds = new Set(o.topBottom.top.map((r) => r.id));
  const topBottom = [
    ...o.topBottom.top.map((r) => row(r, 'Top Performer')),
    ...o.topBottom.bottom.filter((r) => !topIds.has(r.id)).map((r) => row(r, 'Underperforming'))
  ];

  return {
    total: num(kpi(s.totalAssets)),
    health: kpi(s.averageHealthScore),
    utilization: kpi(s.utilizationRate),
    availability: kpi(s.availability),
    mtbf: kpi(s.mtbf),
    mttr: kpi(s.mttr),
    deltas: {
      total: kpiChange(s.totalAssets), health: kpiChange(s.averageHealthScore), utilization: kpiChange(s.utilizationRate),
      availability: kpiChange(s.availability), mtbf: kpiChange(s.mtbf), mttr: kpiChange(s.mttr)
    },
    labels: monthLabels(o.healthTrend),
    healthTrend,
    categoryLabels: cats.map((c) => `${c.category}\n${Math.round(c.utilization)}%`),
    categoryUtilization,
    bands: { excellent: band('Excellent'), good: band('Good'), watch: band('Watch'), critical: band('Critical') },
    distribution,
    reliability,
    downtime: [
      { name: 'Planned', values: seriesValues(o.downtime.trend, 'Planned'), color: RA_COLORS.blue },
      { name: 'Unplanned', values: seriesValues(o.downtime.trend, 'Unplanned'), color: RA_COLORS.pink }
    ],
    downtimeHours: Math.round(num(o.downtime.totalHours)),
    topBottom,
    locations: o.locations.map((l) => ({
      site: site(l.location, l.assetCount),
      name: l.location,
      assets: l.assetCount,
      health: Math.round(num(l.averageHealthScore)),
      utilization: Math.round(num(l.utilization)),
      availability: round1(num(l.availability)),
      status: l.status
    })),
    insights: o.insights,
    healthThreshold: o.filters.healthThreshold,
    utilizationTarget: o.filters.utilizationTarget,
    generatedAt: o.generatedAt
  };
}
