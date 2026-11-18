import { AfterViewInit, Component, DestroyRef, ElementRef, HostListener, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import L from 'leaflet';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  Archive,
  Bell,
  Box,
  Building2,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleDollarSign,
  CirclePause,
  CirclePlay,
  ClipboardList,
  Cog,
  Database,
  ExternalLink,
  Factory,
  FileText,
  FileUp,
  HardHat,
  Heart,
  HeartPulse,
  House,
  Lightbulb,
  MapPin,
  Monitor,
  PieChart,
  Plus,
  Power,
  Settings,
  Trash2,
  TriangleAlert,
  Truck,
  Upload,
  Users,
  Radio,
  Wifi,
  WifiOff,
  Wrench,
} from 'lucide-angular';
import { AGE_COLORS, CATEGORIES, HEALTH_COLORS, HealthBand, PortfolioStatus, STATUSES, STATUS_COLORS, isoDate } from './asset-overview-data';
import { DrillDialog } from '../shared/drill-dialog/drill-dialog';
import { Drill, DrillColumn, DrillRow, pct } from '../shared/drill-dialog/drill';
import { AssetQuickView, QuickViewAsset, qvDate, qvDateTime, statusKey } from '../shared/asset-quick-view/quick-view';
import {
  AssetAging, AssetDashboardCategories, AssetDashboardKpis, AssetPerformanceSummary, AssetStatusCounts, AssetTypeOption, ConnectedAssetDetail, Dashboard,
  HealthScores, InsightAsset, InsightAssetList, InsightEndpoint, LocationUpdateTag, LocationUpdates, MaintenanceDue, MaintenanceDueAsset, SiteOption, TopSite,
} from '../services/dashboard/dashboard';

/** KPI window: "Last 30 days" for api/asset-performance/summary (vs. previous 30 days) and days=30 for api/asset-dashboard/kpis. */
const KPI_DAYS = 30;

type KpiKind = 'total' | 'active' | 'maintenance' | 'critical' | 'health';
type KpiState = 'loading' | 'ok' | 'error';
/** A widget source: null while loading, 'error' when the call failed. */
type Source<T> = T | null | 'error';

type Insight = '' | 'critical' | 'maintenance' | 'owner' | 'documents';
type QuickTab = 'overview' | 'location' | 'maintenance' | 'financial' | 'documents';

/** A site on "Asset Distribution by Site": counts from GET api/asset-dashboard/locations, movement from
 * GET api/asset-dashboard/location-updates?siteId=, position from the master-data site (null when it has none). */
interface SiteMapRow {
  siteId: string;
  name: string;
  lat: number | null;
  lng: number | null;
  totalAssets: number;
  onlinePercent: number;
  /** Location updates in the last KPI_DAYS days. */
  updates: number;
  tags: LocationUpdateTag[];
  /** Which side of the pin the card sits on. */
  card: 'left' | 'right';
}

/** A row of the assets table: an asset from one of the Quick Insights APIs. */
interface TableRow {
  id: string;
  tag: string;
  name: string;
  category: string | null;
  site: string | null;
  criticality: string | null;
  status: string | null;
  /** Due date (due-for-maintenance list only). */
  due: string | null;
  open: () => Drill;
}

/** [lat, lng] when both are real coordinates (0,0 counts as unset). */
function point(lat: number | null | undefined, lng: number | null | undefined): [number, number] | null {
  if (lat == null || lng == null) return null;
  const a = Number(lat);
  const b = Number(lng);
  return Number.isFinite(a) && Number.isFinite(b) && Math.abs(a) <= 90 && Math.abs(b) <= 180 && !(a === 0 && b === 0) ? [a, b] : null;
}

/** Average of the given points; null when there are none. */
function centre(points: ([number, number] | null)[]): [number, number] | null {
  const ok = points.filter((p): p is [number, number] => !!p);
  if (!ok.length) return null;
  return [ok.reduce((s, p) => s + p[0], 0) / ok.length, ok.reduce((s, p) => s + p[1], 0) / ok.length];
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DUE_DAYS = 30;
// Aging chart geometry (SVG user units); the width follows the card's width (1 unit = 1px)
const AW = 230;
const AW_MIN = 180;
const AH = 210;
const AP = { l: 36, r: 6, t: 22, b: 8 };
const HEALTH_BANDS: HealthBand[] = ['Healthy', 'Warning', 'Critical', 'Unknown'];
const CRIT_PILLS: Record<string, string> = { Critical: '#dc2626', High: '#f59e0b', Medium: '#2563eb', Low: '#16a34a' };

@Component({
  selector: 'app-asset-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, DrillDialog],
  templateUrl: './asset-overview.html',
  // header, filters, KPI cards, menus, tables and quick view come from the IoT overview
  styleUrls: ['../iot-tracking-overview/iot-tracking-overview.css', '../iot-tracking-overview/iot-tracking-overview-panels.css', './asset-overview.css'],
})
export class AssetOverview implements AfterViewInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, Archive, Bell, Box, Building2, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleDollarSign, CirclePause, CirclePlay, ClipboardList, Cog, Database,
    ExternalLink, Factory, FileText, FileUp, HardHat, Heart, HeartPulse, House, Lightbulb, MapPin, Monitor, PieChart, Plus, Power, Radio, Settings, Trash2, TriangleAlert, Truck, Upload,
    Users, Wifi, WifiOff, Wrench,
  };
  readonly categoryIcons: Record<string, typeof Factory> = { factory: Factory, truck: Truck, building: Building2, monitor: Monitor, wrench: Wrench, 'hard-hat': HardHat };
  private readonly statusIconMap: Record<string, typeof Factory> = {
    Active: CirclePlay, Idle: CirclePause, 'In Transit': Truck, 'Under Maintenance': Wrench, Retired: Archive, Disposed: Trash2,
  };
  /** "Site" dropdown, from GET api/master-data/sites?clientId=. */
  sites: string[] = [];
  /** Master-data sites, to turn the Site dropdown's name into the site id the dashboard APIs filter by. */
  private masterSites: SiteOption[] = [];
  /** Rows of GET api/master-data/asset-types behind the "Asset Type" dropdown. */
  private assetTypes: AssetTypeOption[] = [];
  readonly healthColors = HEALTH_COLORS;
  aw = AW;
  readonly ah = AH;
  readonly ap = AP;

  // Filters
  asOfOpen = false;
  site = '';
  category = '';
  type = '';
  status = '';

  // Cards
  hoverHealth: HealthBand | null = null;
  hoverAge: number | null = null;
  insight: Insight = '';
  showAllRows = false;
  rowPage = 0;
  readonly rowPageSize = 8;
  showAllSites = false;
  quickTab: QuickTab = 'overview';
  menuOpen = '';
  /** Drill-down popup for the clicked widget element. */
  drill: Drill | null = null;

  /** Registered assets (from GET api/asset-dashboard/statuses) that pass the filters; rebuilt by apply(). */
  assets: InsightAsset[] = [];

  @ViewChild('mapEl') private mapEl?: ElementRef<HTMLDivElement>;
  @ViewChild('agingEl') private agingEl?: ElementRef<HTMLDivElement>;
  private map?: L.Map;
  private siteLayer?: L.LayerGroup;
  private mapResize?: ResizeObserver;
  private agingResize?: ResizeObserver;
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly today = new Date();
  private readonly dashboardApi = inject(Dashboard);
  private readonly destroyRef = inject(DestroyRef);
  /** Asset Quick View: registered assets from GET api/asset-registrations, one at a time. */
  readonly qv = new AssetQuickView(this.dashboardApi, this.destroyRef);
  readonly statusKey = statusKey;
  readonly qvDate = qvDate;
  readonly qvDateTime = qvDateTime;

  readonly kpiDays = KPI_DAYS;
  readonly dueDays = DUE_DAYS;
  // Each widget's source; null until it answers and 'error' when it failed.
  /** Total / Active / Under Maintenance and the asset list behind the filters and "Asset Status Overview": GET api/asset-dashboard/statuses (Asset service). */
  statusData: Source<AssetStatusCounts> = null;
  /** Total Assets change and Avg Asset Health Score: GET api/asset-performance/summary?datePreset=Last 30 days (Report Analytics). */
  performance: Source<AssetPerformanceSummary> = null;
  /** GET api/asset-dashboard/kpis: only its connected assets are used here (Quick View connectivity). */
  kpiData: AssetDashboardKpis | null = null;
  /** "Asset Portfolio by Category" from GET api/asset-dashboard/categories. */
  categoryData: Source<AssetDashboardCategories> = null;
  /** Quick Insights lists from the API, by insight; missing until loaded. */
  insightData: Partial<Record<Insight, InsightAssetList>> = {};
  /** Insight lists whose API call failed. */
  insightFailed = new Set<Insight>();
  /** "Asset Health Distribution": GET api/asset-dashboard/health-scores?siteId=&assetType=. */
  healthData: Source<HealthScores> = null;
  private healthSub?: Subscription;
  /** "Asset Lifecycle Aging": GET api/asset-dashboard/aging. */
  agingData: Source<AssetAging> = null;
  /** Quick Insights "due for maintenance": GET api/asset-dashboard/assets-due-maintenance?days=30. */
  dueData: Source<MaintenanceDue> = null;
  /** "Asset Distribution by Site", one row per site the dashboard API knows. */
  siteRows: SiteMapRow[] = [];
  sitesLoading = false;
  readonly siteDays = KPI_DAYS;
  private siteSub?: Subscription;
  /** Asset type the site rows were fetched for; null before the first fetch. */
  private siteMapType: string | null = null;
  /** Site + type the health donut was fetched for; null before the first fetch. */
  private healthKey: string | null = null;

  constructor() {
    this.loadKpiSources();
    this.loadConnected();
    this.loadFilterOptions();
    this.loadCategories();
    this.loadInsights();
    this.loadAging();
    this.loadMaintenanceDue();
    this.qv.load();
    this.apply();
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.observeAging();
  }

  ngOnDestroy(): void {
    this.mapResize?.disconnect();
    this.agingResize?.disconnect();
    this.map?.remove();
  }

  // ===== Filters =====

  /** Only "today" exists: no API keeps the portfolio's history, so the earlier dates are disabled in the menu. */
  readonly asOfLabel = 'As of Today';

  /** Site dropdown: master-data sites plus any site the registered assets are mapped to. */
  get siteOptions(): string[] {
    const names = [...this.sites, ...this.portfolio.map((a) => a.siteName ?? '')].filter(Boolean);
    return [...new Set(names)].sort((a, b) => a.localeCompare(b));
  }

  /** Category dropdown: the categories the API counted plus those of the master-data asset types. */
  get categories(): string[] {
    const names = [
      ...(this.ok(this.categoryData)?.categories ?? []).map((c) => c.category),
      ...this.assetTypes.map((t) => t.category?.name ?? ''),
    ].filter(Boolean);
    return [...new Set(names)].sort((a, b) => a.localeCompare(b));
  }

  /** Types offered in the dropdown: the API types of the chosen category, or all when none belong to it. */
  get types(): string[] {
    const inCategory = this.assetTypes.filter((t) => t.category?.name === this.category);
    const list = this.category && inCategory.length ? inCategory : this.assetTypes;
    return [...new Set(list.map((t) => t.assetTypeName))].sort((a, b) => a.localeCompare(b));
  }

  /** Statuses for the dropdown and "Asset Status Overview": the standard ones plus any other status the API returned. */
  get statuses(): string[] {
    const extra = (this.ok(this.statusData)?.statuses ?? []).map((s) => s.status).filter((s) => !STATUSES.some((x) => this.same(x, s)));
    return [...STATUSES, ...new Set(extra)];
  }

  onCategory(): void {
    if (this.type && !this.types.includes(this.type)) this.type = '';
    this.apply();
  }

  apply(): void {
    this.assets = this.portfolio.filter((a) => this.inScope(a));
    this.rowPage = 0;
    // location-updates filters by asset type; site / category / status only highlight on the map
    if (this.siteMapType !== this.type) {
      this.siteMapType = this.type;
      this.loadSiteMap();
    }
    const healthKey = `${this.site}|${this.type}`;
    if (this.healthKey !== healthKey) {
      this.healthKey = healthKey;
      this.loadHealth();
    }
    this.drawSites();
  }

  /** Every registered asset, from GET api/asset-dashboard/statuses (empty until it loads). */
  private get portfolio(): InsightAsset[] {
    return (this.ok(this.statusData)?.statuses ?? []).flatMap((s) => s.assets ?? []);
  }

  private inScope(a: { siteName: string | null; category: string | null; assetType?: string | null; assetStatus: string | null }): boolean {
    const eq = (v: string | null | undefined, f: string) => !f || this.same(v ?? '', f);
    return eq(a.siteName, this.site) && eq(a.category, this.category) && eq(a.assetType, this.type) && eq(a.assetStatus, this.status);
  }

  /** Ids of the assets passing the filters; null when no filter is set or the asset list has not loaded (lists are then shown whole). */
  private get scopeIds(): Set<string> | null {
    if (!this.filtered || !this.ok(this.statusData)) return null;
    return new Set(this.assets.map((a) => a.assetRegistrationId));
  }

  private inScopeId(id: string): boolean {
    const ids = this.scopeIds;
    return !ids || ids.has(id);
  }

  get filtered(): boolean {
    return !!(this.site || this.category || this.type || this.status);
  }

  private same(a: string, b: string): boolean {
    return a.trim().toLowerCase() === b.trim().toLowerCase();
  }

  /** The value of a loaded source, or null while loading / on error. */
  ok<T>(s: Source<T>): T | null {
    return s === 'error' ? null : s;
  }

  // ===== Loading =====

  /** Fills the Site and Asset Type dropdowns from master data (active, non-deleted rows). */
  loadFilterOptions(): void {
    const live = <T extends { status: string; isDeleted: boolean }>(rows: T[] | null) => (rows ?? []).filter((r) => !r.isDeleted && r.status !== 'Inactive');
    this.dashboardApi
      .getSites()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.masterSites = live(res);
          this.sites = [...new Set(this.masterSites.map((s) => s.siteName).filter(Boolean))].sort((a, b) => a.localeCompare(b));
        },
        error: () => (this.sites = []),
      });
    this.dashboardApi
      .getAssetTypes()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => (this.assetTypes = live(res).filter((t) => !!t.assetTypeName)),
        error: () => (this.assetTypes = []),
      });
  }

  /** Fetches the Quick Insights lists: critical assets, assets without custodian, assets without documents. */
  loadInsights(): void {
    const endpoints: [Insight, InsightEndpoint][] = [['critical', 'critical-assets'], ['owner', 'assets-without-custodian'], ['documents', 'assets-without-documents']];
    for (const [key, endpoint] of endpoints) {
      this.dashboardApi
        .getInsightAssets(endpoint)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res) => (this.insightData = { ...this.insightData, [key]: res }),
          error: () => (this.insightFailed = new Set([...this.insightFailed, key])),
        });
    }
  }

  /** Fetches the assets due for maintenance in the next DUE_DAYS days (Quick Insights). */
  loadMaintenanceDue(): void {
    this.dashboardApi
      .getMaintenanceDue(DUE_DAYS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => (this.dueData = res), error: () => (this.dueData = 'error') });
  }

  /** Fetches the assets by age band. */
  loadAging(): void {
    this.dashboardApi
      .getAging()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => (this.agingData = res), error: () => (this.agingData = 'error') });
  }

  /** Fetches the health donut for the Site and Asset Type filters (the API cannot narrow by category or asset status). */
  loadHealth(): void {
    this.healthSub?.unsubscribe();
    this.healthData = null;
    const siteId = this.site ? this.siteIdOf(this.site) : undefined;
    // a site with no id in master data has no assets the API could count
    if (this.site && !siteId) {
      this.healthData = { total: 0, centrePercent: 0, bands: [] };
      return;
    }
    this.healthSub = this.dashboardApi
      .getHealthScores({ siteId, assetType: this.type || undefined })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => (this.healthData = res), error: () => (this.healthData = 'error') });
  }

  private siteIdOf(name: string): string | undefined {
    return this.masterSites.find((s) => this.same(s.siteName ?? '', name))?.id ?? this.siteRows.find((s) => this.same(s.name, name))?.siteId;
  }

  /** Fetches the asset counts per category once, when the page opens. */
  loadCategories(): void {
    this.dashboardApi
      .getCategories()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => (this.categoryData = res), error: () => (this.categoryData = 'error') });
  }

  /** Fetches the KPI cards' sources: asset statuses with their assets (Asset service) and the asset performance summary (Report Analytics). */
  loadKpiSources(): void {
    this.dashboardApi
      .getStatusCounts(undefined, true)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.statusData = res;
          this.apply();
        },
        error: () => (this.statusData = 'error'),
      });
    this.dashboardApi
      .getAssetPerformanceSummary(`Last ${KPI_DAYS} days`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (res) => (this.performance = res), error: () => (this.performance = 'error') });
  }

  /** Connected assets (GET api/asset-dashboard/kpis) for the Quick View's connectivity; not shown as cards here. */
  loadConnected(): void {
    this.dashboardApi
      .getKpis(KPI_DAYS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.kpiData = res;
          this.qv.setConnected(res?.connectedAssetDetails ?? []);
        },
        error: () => (this.kpiData = null),
      });
  }

  // ===== KPIs =====

  /**
   * The KPI cards' figures from the existing APIs. A figure is null while its source loads or when it failed;
   * the matching *State says which ('loading' shows …, 'error' shows – with "Could not load").
   * Lifecycle Value has no API in any service yet, so it is not here.
   */
  get kpis() {
    const st = this.ok(this.statusData);
    const perf = this.ok(this.performance)?.data ?? null;
    const crit = this.insightData.critical;
    const state = (src: unknown, failed: boolean): KpiState => (src ? 'ok' : failed ? 'error' : 'loading');
    const statusState = state(st, this.statusData === 'error');
    const perfState = state(perf, this.performance === 'error');
    const count = (status: string) => st?.statuses.find((s) => s.status.toLowerCase() === status.toLowerCase())?.count ?? 0;
    const round = (v: number | null | undefined) => (v === null || v === undefined ? null : Math.round(v * 10) / 10);
    const active = st ? count('Active') : null;
    const maintenance = st ? count('Under Maintenance') : null;
    const health = perf?.averageHealthScore;
    return {
      total: st?.totalAssets ?? null, totalState: statusState,
      // Report Analytics: assets in service vs. the previous 30 days
      totalChange: round(perf?.totalAssets.change),
      active, activePct: st && active !== null ? pct(active, st.totalAssets) : 0, activeState: statusState,
      maintenance, maintenancePct: st && maintenance !== null ? pct(maintenance, st.totalAssets) : 0, maintenanceState: statusState,
      critical: crit?.count ?? null, criticalPct: crit ? pct(crit.count, crit.totalAssets) : 0, criticalState: state(crit, this.insightFailed.has('critical')),
      // null value with the summary loaded = no asset has a condition assessment yet
      health: round(health?.value), healthState: perfState,
      healthChange: round(health?.change), healthUnit: health?.changeUnit === 'points' ? ' pts' : '%',
    };
  }

  /** Placeholder for a figure that is not there: … while loading, – when it could not be loaded or has no data. */
  blank(s: KpiState): string {
    return s === 'loading' ? '…' : '–';
  }

  /** Connected assets returned with the KPIs. */
  get connectedAssets(): ConnectedAssetDetail[] {
    return this.kpiData?.connectedAssetDetails ?? [];
  }

  // ===== Portfolio, health, status =====

  get byCategory() {
    const rows = this.ok(this.categoryData)?.categories ?? [];
    const max = Math.max(1, ...rows.map((r) => r.count));
    return rows.map((r, i) => {
      // icon and colour from the local catalogue when the name matches, else cycle through it
      const local = CATEGORIES.find((c) => c.name.toLowerCase() === r.category.toLowerCase()) ?? CATEGORIES[i % CATEGORIES.length];
      return {
        name: r.category, color: local.color, icon: local.icon, count: r.count,
        pct: Math.round(r.percent * 10) / 10, bar: (r.count / max) * 100, subCategories: r.subCategories ?? [],
        // sub-category lines under the row: bar on the same scale as categories, % is the share of this category
        subs: (r.subCategories ?? []).map((sc) => ({ name: sc.subCategory, count: sc.count, pct: pct(sc.count, r.count), bar: (sc.count / max) * 100 })),
      };
    });
  }

  pickCategory(name: string): void {
    this.category = this.category === name ? '' : name;
    this.onCategory();
  }

  /** Health donut from GET api/asset-dashboard/health-scores; null while loading or on error. */
  get health() {
    const api = this.ok(this.healthData);
    if (!api) return null;
    const total = api.total;
    const C = 2 * Math.PI * 46;
    let acc = 0;
    const rows = HEALTH_BANDS.map((b) => {
      const count = api.bands.find((x) => this.same(x.name, b))?.count ?? 0;
      const len = total ? (count / total) * C : 0;
      const mid = (acc + len / 2) / C;
      const row = {
        band: b, count, pct: pct(count, total), color: HEALTH_COLORS[b], dash: `${Math.max(0, len - (count ? 2 : 0))} ${C}`, offset: -acc,
        // label position just outside the ring, at the segment's middle
        lx: 70 + 64 * Math.sin(mid * 2 * Math.PI), ly: 70 - 64 * Math.cos(mid * 2 * Math.PI) + 3,
      };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  statusIcon(s: string): typeof Factory {
    return this.statusIconMap[STATUSES.find((x) => this.same(x, s)) ?? ''] ?? Box;
  }

  statusColor(s: string): string {
    return STATUS_COLORS[STATUSES.find((x) => this.same(x, s)) as PortfolioStatus] ?? '#94a3b8';
  }

  private statusCount(s: string): number {
    return this.assets.filter((a) => this.same(a.assetStatus ?? '', s)).length;
  }

  get byStatus() {
    const total = this.assets.length;
    return this.statuses.map((s) => {
      const count = this.statusCount(s);
      return { status: s, count, pct: pct(count, total), color: this.statusColor(s) };
    });
  }

  pickStatus(s: string): void {
    this.status = this.status === s ? '' : s;
    this.apply();
  }

  // ===== Insights =====

  /** The insight's assets from its API, narrowed to the filters. */
  private insightAssets(key: Insight): InsightAsset[] {
    return (this.insightData[key]?.assets ?? []).filter((a) => this.inScopeId(a.assetRegistrationId));
  }

  private dueAssets(): MaintenanceDueAsset[] {
    return (this.ok(this.dueData)?.assets ?? []).filter((a) => this.inScopeId(a.assetRegistrationId));
  }

  /** Count shown in Quick Insights; null while loading or when the API failed. */
  private insightCount(key: Insight): number | null {
    if (key === 'maintenance') return this.ok(this.dueData) ? this.dueAssets().length : null;
    return this.insightData[key] ? this.insightAssets(key).length : null;
  }

  private insightFailedFor(key: Insight): boolean {
    return key === 'maintenance' ? this.dueData === 'error' : this.insightFailed.has(key);
  }

  get insights(): { key: Insight; count: number | null; failed: boolean; label: string; icon: typeof Factory; tone: string }[] {
    const row = (key: Insight, label: string, icon: typeof Factory, tone: string) => ({ key, count: this.insightCount(key), failed: this.insightFailedFor(key), label, icon, tone });
    return [
      row('critical', 'critical assets', TriangleAlert, 'red'),
      row('maintenance', `due for maintenance (${DUE_DAYS} days)`, Wrench, 'amber'),
      row('owner', 'assets without custodian', Users, 'blue'),
      row('documents', 'assets with missing documents', FileText, 'violet'),
    ];
  }

  openInsight(key: Insight): void {
    this.insight = this.insight === key ? '' : key;
    this.showAllRows = !!this.insight;
    this.rowPage = 0;
  }

  get tableTitle(): string {
    return {
      '': 'Critical Assets', critical: 'Critical Assets', maintenance: `Due for Maintenance (next ${DUE_DAYS} days)`, owner: 'Assets Without Custodian', documents: 'Assets With Missing Documents',
    }[this.insight];
  }

  /** The table's source: the selected insight's API (the critical-assets API by default). */
  private get tableKey(): Insight {
    return this.insight || 'critical';
  }

  get tableLoading(): boolean {
    const key = this.tableKey;
    return this.insightCount(key) === null && !this.insightFailedFor(key);
  }

  get tableEmpty(): string {
    const key = this.tableKey;
    if (this.insightFailedFor(key)) return 'Could not load the assets.';
    if (this.tableLoading) return 'Loading…';
    return 'No data available';
  }

  get tableRows(): TableRow[] {
    if (this.tableKey === 'maintenance') {
      return this.dueAssets().map((a) => ({
        id: a.assetRegistrationId, tag: a.assetTag ?? a.assetRegistrationId, name: a.assetName ?? '-', category: a.category, site: a.siteName,
        criticality: a.criticality, status: a.assetStatus, due: a.dueDate, open: () => this.dueDrill(a),
      }));
    }
    return this.insightAssets(this.tableKey).map((a) => ({
      id: a.assetRegistrationId, tag: a.assetTag, name: a.assetName, category: a.category, site: a.siteName,
      criticality: a.criticality, status: a.assetStatus, due: null, open: () => this.insightAssetDrill(a),
    }));
  }

  get pageRows(): TableRow[] {
    const list = this.tableRows;
    return this.showAllRows ? list.slice(this.rowPage * this.rowPageSize, (this.rowPage + 1) * this.rowPageSize) : list.slice(0, 5);
  }

  get rowPages(): number {
    return Math.max(1, Math.ceil(this.tableRows.length / this.rowPageSize));
  }

  /** Pill class for an API criticality (Critical / High / Medium / Low). */
  critClass(c: string | null): string {
    return { critical: 'sev--critical', high: 'sev--warning', medium: 'sev--info' }[(c ?? '').toLowerCase()] ?? 'sev--low';
  }

  openRow(r: TableRow): void {
    this.drill = r.open();
  }

  /** Details of one dashboard-API asset row (also opened from the KPI card lists); shows it in Quick View too. */
  private insightAssetDrill(a: InsightAsset): Drill {
    this.showQuick(a.assetRegistrationId);
    return {
      title: `${a.assetTag} · ${a.assetName}`,
      subtitle: [[a.category, a.subCategory].filter(Boolean).join(' / '), a.assetType, a.siteName].filter(Boolean).join(' · ') || 'Asset',
      stats: [
        { label: 'Criticality', value: a.criticality ?? '-', color: (a.criticality ?? '').toLowerCase() === 'critical' ? '#dc2626' : undefined },
        { label: 'Status', value: a.assetStatus ?? '-' },
        { label: 'Documents', value: a.documentCount },
      ],
      columns: [{ key: 'field', label: 'Field' }, { key: 'value', label: 'Value' }],
      rows: [
        ['Asset Tag', a.assetTag], ['Asset Name', a.assetName], ['Category', a.category], ['Sub-category', a.subCategory], ['Asset Type', a.assetType],
        ['Site', a.siteName], ['Location', a.locationPath],
        ['Custodian', a.custodianName ? `${a.custodianName}${a.custodianDepartment ? ' (' + a.custodianDepartment + ')' : ''}` : 'Not assigned'],
      ].map(([field, value]) => ({ cells: { field: field as string, value: (value as string | null) || '-' } })),
    };
  }

  /** Details of an asset due for maintenance. */
  private dueDrill(a: MaintenanceDueAsset): Drill {
    this.showQuick(a.assetRegistrationId);
    const overdue = new Date(a.dueDate).getTime() < this.today.getTime();
    return {
      title: `${a.assetTag ?? a.assetRegistrationId} · ${a.assetName ?? ''}`,
      subtitle: [[a.category, a.subCategory].filter(Boolean).join(' / '), a.assetType, a.siteName].filter(Boolean).join(' · ') || 'Asset',
      stats: [
        { label: overdue ? 'Overdue since' : 'Due', value: this.fmtDate(a.dueDate), color: overdue ? '#dc2626' : '#d97706' },
        { label: 'Open work orders', value: a.openWorkOrders },
        { label: 'Criticality', value: a.criticality ?? '-' },
      ],
      fields: [
        { label: 'Due from', value: a.dueSource }, { label: 'Work order', value: a.workOrderId ?? '-' },
        { label: 'Status', value: a.assetStatus ?? '-' }, { label: 'Site', value: a.siteName ?? '-' },
      ],
    };
  }

  // ===== Aging =====

  /** Age bands from GET api/asset-dashboard/aging, narrowed to the filters; null while loading or on error. */
  get aging() {
    const api = this.ok(this.agingData);
    if (!api) return null;
    const bands = api.bands.map((b) => ({ ...b, assets: b.assets.filter((a) => this.inScopeId(a.assetRegistrationId)) }));
    const counts = bands.map((b) => b.assets.length);
    const total = counts.reduce((s, n) => s + n, 0);
    const max = Math.max(1, ...counts);
    const step = max > 1200 ? 400 : max > 600 ? 200 : max > 120 ? 50 : 10;
    const top = Math.ceil(max / step) * step;
    const plotH = AH - AP.t - AP.b;
    const slot = (this.aw - AP.l - AP.r) / Math.max(1, bands.length);
    const barW = slot * 0.66;
    const bars = bands.map((b, i) => {
      const h = (counts[i] / top) * plotH;
      return {
        band: b.band, label: b.label, color: AGE_COLORS[b.band] ?? '#94a3b8', assets: b.assets,
        count: counts[i], pct: pct(counts[i], total), x: AP.l + slot * i + (slot - barW) / 2, y: AH - AP.b - h, h,
      };
    });
    const ticks = Array.from({ length: top / step + 1 }, (_, i) => ({ y: AH - AP.b - (i * step / top) * plotH, label: (i * step).toLocaleString() }));
    return { bars, ticks, barW, total, undated: api.withoutPurchaseDate };
  }

  /** Redraws the aging chart at its card's width as the grid columns change (window, breakpoints, sidebar). */
  private observeAging(): void {
    const el = this.agingEl?.nativeElement;
    if (!el) return;
    this.agingResize = new ResizeObserver(() => {
      if (!el.clientWidth) return;
      const aw = Math.max(AW_MIN, Math.round(el.clientWidth));
      // ResizeObserver callbacks run outside Angular's zone
      if (aw !== this.aw) this.zone.run(() => (this.aw = aw));
    });
    this.agingResize.observe(el);
  }


  // ===== Sites & map =====

  /** Sites that can be pinned on the map. */
  get pinnedSites(): SiteMapRow[] {
    return this.siteRows.filter((r) => r.lat !== null && r.lng !== null);
  }

  /**
   * Sites (GET api/asset-dashboard/locations) with their location updates for the last KPI_DAYS days
   * (GET api/asset-dashboard/location-updates?siteId=, one call per site) and map position (master-data sites).
   */
  loadSiteMap(): void {
    this.siteSub?.unsubscribe();
    this.sitesLoading = true;
    const assetType = this.type || undefined;
    this.siteSub = forkJoin({
      master: this.dashboardApi.getSites().pipe(catchError(() => of([] as SiteOption[]))),
      locations: this.dashboardApi.getLocations().pipe(catchError(() => of(null))),
    })
      .pipe(
        switchMap(({ master, locations }) => {
          const sites = locations?.topSites ?? [];
          if (!sites.length) return of([] as SiteMapRow[]);
          return forkJoin(
            sites.map((site) =>
              this.dashboardApi.getLocationUpdates(KPI_DAYS, { siteId: site.siteId, assetType }).pipe(
                catchError(() => of(null)),
                map((u) => this.siteRow(site, u, master ?? [])),
              ),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((rows) => {
        // cards sit right of their pin, except the easternmost site's, which opens to the left
        const east = rows.filter((r) => r.lng !== null).sort((a, b) => b.lng! - a.lng!)[0];
        if (east && rows.filter((r) => r.lng !== null).length > 1) east.card = 'left';
        this.siteRows = rows.sort((a, b) => b.totalAssets - a.totalAssets || a.name.localeCompare(b.name));
        this.sitesLoading = false;
        this.fitSites();
        this.drawSites();
      });
  }

  private siteRow(site: TopSite, u: LocationUpdates | null, master: SiteOption[]): SiteMapRow {
    const name = site.siteName || site.siteId;
    const m = master.find((x) => x.id === site.siteId) ?? master.find((x) => x.siteName?.trim().toLowerCase() === name.trim().toLowerCase());
    const tags = u?.tags ?? [];
    // the master-data site's position; without one, the centre of where the site's tags last moved to
    const pos = point(m?.latitude, m?.longitude) ?? centre(tags.map((t) => point(t.lastToLatitude, t.lastToLongitude)));
    return {
      siteId: site.siteId, name, lat: pos?.[0] ?? null, lng: pos?.[1] ?? null,
      totalAssets: site.totalAssets ?? 0, onlinePercent: Math.round((site.onlinePercent ?? 0) * 10) / 10,
      updates: u?.totalCount ?? 0, tags, card: 'right',
    };
  }

  private initMap(): void {
    const el = this.mapEl?.nativeElement;
    if (!el) return;
    this.map = L.map(el, { zoomControl: false, attributionControl: true, scrollWheelZoom: false });
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    }).addTo(this.map);
    this.siteLayer = L.layerGroup().addTo(this.map);
    this.fitSites();
    this.drawSites();
    // Leaflet only measures on init: refit whenever the card is resized or un-hidden.
    this.mapResize = new ResizeObserver(() => {
      if (!el.clientWidth || !el.clientHeight) return;
      this.map?.invalidateSize();
      this.fitSites();
    });
    this.mapResize.observe(el);
  }

  /** Fits all pinned sites, with padding scaled to the map so small cards still get a usable zoom. */
  private fitSites(): void {
    const size = this.map?.getSize();
    if (!this.map || !size?.x || !size.y) return;
    const pins = this.pinnedSites;
    // No sites with coordinates: show a world view instead of fitting empty (invalid) bounds
    if (!pins.length) {
      this.map.setView([20, 0], 2);
      return;
    }
    const pad: [number, number] = [Math.min(120, size.x * 0.2), Math.min(70, size.y * 0.2)];
    this.map.fitBounds(L.latLngBounds(pins.map((s) => [s.lat!, s.lng!] as [number, number])), { padding: pad, maxZoom: 12 });
  }

  /** Each site: a pin plus a card with its asset count and online share (location updates are in the site popup). */
  private drawSites(): void {
    if (!this.map || !this.siteLayer) return;
    this.siteLayer.clearLayers();
    const esc = (v: string) => v.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
    for (const s of this.pinnedSites) {
      const on = this.site === s.name;
      const dim = !!this.site && !on;
      const C = 2 * Math.PI * 9;
      const ring = `<svg viewBox="0 0 24 24" width="24" height="24"><circle cx="12" cy="12" r="9" fill="none" stroke="#e5e7eb" stroke-width="4"/><circle cx="12" cy="12" r="9" fill="none" stroke="#16a34a" stroke-width="4" stroke-dasharray="${(C * s.onlinePercent) / 100} ${C}" transform="rotate(-90 12 12)"/></svg>`;
      const html = `<div class="ao-site-card ao-site-card--${s.card}${on ? ' is-on' : ''}${dim ? ' is-dim' : ''}">
          <strong>${esc(s.name)}</strong><span>${s.totalAssets.toLocaleString()} assets</span>
          <span class="ao-site-health">${ring}<b>${s.onlinePercent}%</b> Online</span></div><i class="ao-site-pin"></i>`;
      L.marker([s.lat!, s.lng!], { icon: L.divIcon({ className: 'ao-site', html, iconSize: [0, 0] }), keyboard: true, title: s.name })
        .on('click', () => this.zone.run(() => this.openSite(s)))
        .addTo(this.siteLayer);
    }
  }

  pickSite(name: string): void {
    this.site = this.site === name ? '' : name;
    this.apply();
  }
  // ===== Quick view =====

  /** Shows a registered asset in the Quick View. */
  showQuick(assetRegistrationId: string): void {
    this.quickTab = 'overview';
    this.qv.show(assetRegistrationId);
  }

  // ===== Drill-down popup =====

  private get scope(): string {
    return [this.site, this.category, this.type, this.status].filter(Boolean).join(' · ') || 'All sites, categories, types and statuses';
  }

  private readonly assetColumns: DrillColumn[] = [
    { key: 'tag', label: 'Asset Tag' }, { key: 'name', label: 'Asset Name' }, { key: 'category', label: 'Category' }, { key: 'type', label: 'Type' },
    { key: 'site', label: 'Site' }, { key: 'criticality', label: 'Criticality', pill: CRIT_PILLS }, { key: 'status', label: 'Status' },
  ];

  private assetRows(list: InsightAsset[]): DrillRow[] {
    return list.map((a) => ({
      cells: {
        tag: a.assetTag, name: a.assetName, category: [a.category, a.subCategory].filter(Boolean).join(' / ') || '-', type: a.assetType ?? '-',
        site: a.siteName ?? '-', criticality: a.criticality ?? '-', status: a.assetStatus ?? '-',
      },
      open: () => this.insightAssetDrill(a),
    }));
  }

  /** Drill-down of a KPI card; asset lists come from GET api/asset-dashboard/statuses and /critical-assets. */
  openKpi(kind: KpiKind): void {
    const k = this.kpis;
    const change = (v: number | null, unit = '%') => (v === null ? [] : [{ label: `vs. previous ${KPI_DAYS} days`, value: `${v > 0 ? '+' : ''}${v}${unit}` }]);
    const st = this.ok(this.statusData);
    const byStatus = (title: string, status: string, stats: Drill['stats']) => {
      const assets = st?.statuses.find((s) => this.same(s.status, status))?.assets ?? [];
      this.drill = {
        title, subtitle: 'Registered assets by status', stats, tableTitle: `${status} assets`, columns: this.assetColumns, rows: this.assetRows(assets),
        empty: st ? `No ${status.toLowerCase()} assets.` : this.statusData === 'error' ? 'Could not load the assets.' : 'Loading assets…',
      };
    };

    if (kind === 'total') {
      this.drill = {
        title: 'Total Assets', subtitle: 'Registered assets by status',
        stats: [{ label: 'Registered', value: k.total ?? '-' }, ...change(k.totalChange)],
        tableTitle: 'Assets by status',
        columns: [{ key: 'status', label: 'Status' }, { key: 'count', label: 'Assets', num: true }, { key: 'pct', label: 'Of total', num: true }],
        rows: (st?.statuses ?? []).map((s) => ({ cells: { status: s.status, count: s.count, pct: `${s.percent}%` } })),
        empty: st ? 'No assets.' : this.statusData === 'error' ? 'Could not load the statuses.' : 'Loading statuses…',
      };
    } else if (kind === 'active') {
      byStatus('Active Assets', 'Active', [{ label: 'Active', value: k.active ?? '-', color: '#16a34a' }, { label: 'Of total assets', value: `${k.activePct}%` }]);
    } else if (kind === 'maintenance') {
      byStatus('Assets Under Maintenance', 'Under Maintenance', [{ label: 'Under maintenance', value: k.maintenance ?? '-', color: '#d97706' }, { label: 'Of total assets', value: `${k.maintenancePct}%` }]);
    } else if (kind === 'critical') {
      this.drill = {
        title: 'Critical Assets', subtitle: 'Assets with criticality Critical',
        stats: [{ label: 'Critical', value: k.critical ?? '-', color: '#dc2626' }, { label: 'Of total assets', value: `${k.criticalPct}%` }],
        tableTitle: 'Critical assets', columns: this.assetColumns, rows: this.assetRows(this.insightData.critical?.assets ?? []), empty: 'No critical assets.',
      };
    } else {
      this.drill = {
        title: 'Avg Asset Health Score', subtitle: `Latest condition assessment of each asset · last ${KPI_DAYS} days`,
        stats: [{ label: 'Average score', value: k.health === null ? '-' : `${k.health}%` }, ...change(k.healthChange, k.healthUnit)],
      };
    }
  }

  openCategory(name: string): void {
    const c = this.byCategory.find((r) => r.name === name);
    if (!c) return;
    const on = this.category === name;
    this.drill = {
      title: name, subtitle: 'Asset category',
      stats: [{ label: 'Assets', value: c.count }, { label: 'Of portfolio', value: `${c.pct}%` }, { label: 'Sub-categories', value: c.subCategories.length }],
      tableTitle: 'Sub-categories',
      columns: [{ key: 'sub', label: 'Sub-category' }, { key: 'count', label: 'Assets', num: true }, { key: 'pct', label: 'Of category', num: true }],
      rows: c.subCategories.map((s) => ({ cells: { sub: s.subCategory, count: s.count, pct: `${pct(s.count, c.count)}%` } })),
      empty: 'No sub-categories.',
      actions: [{ label: on ? 'Clear category filter' : `Filter dashboard to ${name}`, run: () => this.pickCategory(name) }],
    };
  }

  /** Health band popup: the API returns counts per band only (no asset list). */
  openHealthBand(band: HealthBand): void {
    const h = this.health;
    const row = h?.rows.find((r) => r.band === band);
    if (!h || !row) return;
    this.drill = {
      title: `${band} Assets`,
      subtitle: `Latest condition assessment per asset · ${[this.site, this.type].filter(Boolean).join(' · ') || 'All sites and types'}`,
      stats: [
        { label: band, value: row.count, color: HEALTH_COLORS[band] }, { label: 'Of assets', value: `${row.pct}%` },
        { label: band === 'Unknown' ? 'Rule' : 'Score', value: { Healthy: '70-100', Warning: '40-69', Critical: 'below 40', Unknown: 'never assessed' }[band] },
      ],
      actions: [{ label: 'Open Asset Health Review', run: () => this.go('/assets/health-score') }],
    };
  }

  openStatus(s: string): void {
    const list = this.assets.filter((a) => this.same(a.assetStatus ?? '', s));
    const on = this.status === s;
    this.drill = {
      title: `${s} Assets`, subtitle: `Asset status · ${this.scope}`,
      stats: [{ label: s, value: list.length, color: this.statusColor(s) }, { label: 'Of portfolio', value: `${pct(list.length, this.assets.length)}%` }],
      columns: this.assetColumns, rows: this.assetRows(list), empty: 'No assets.',
      actions: [{ label: on ? 'Clear status filter' : `Filter dashboard to ${s}`, run: () => this.pickStatus(s) }],
    };
  }

  openInsightDrill(key: Insight): void {
    const i = this.insights.find((x) => x.key === key);
    const total = this.assets.length || this.ok(this.statusData)?.totalAssets || 0;
    if (key === 'maintenance') {
      const list = this.dueAssets();
      this.drill = {
        title: this.capitalize(i?.label ?? ''), subtitle: `Next maintenance date or open work order due within ${DUE_DAYS} days (overdue included) · ${this.scope}`,
        stats: [{ label: 'Assets', value: list.length }, { label: 'Of portfolio', value: `${pct(list.length, total)}%` }],
        columns: [...this.assetColumns.filter((c) => c.key !== 'type'), { key: 'due', label: 'Due' }, { key: 'source', label: 'From' }],
        rows: list.map((a) => ({
          cells: {
            tag: a.assetTag ?? a.assetRegistrationId, name: a.assetName ?? '-', category: [a.category, a.subCategory].filter(Boolean).join(' / ') || '-',
            site: a.siteName ?? '-', criticality: a.criticality ?? '-', status: a.assetStatus ?? '-', due: this.fmtDate(a.dueDate), source: a.dueSource,
          },
          open: () => this.dueDrill(a),
        })),
        empty: this.dueData === 'error' ? 'Could not load the assets.' : 'No data available',
        actions: [{ label: this.insight === key ? 'Clear table filter' : 'Show in table', run: () => this.openInsight(key) }],
      };
      return;
    }
    const list = this.insightAssets(key);
    this.drill = {
      title: this.capitalize(i?.label ?? ''), subtitle: this.scope,
      stats: [{ label: 'Assets', value: list.length }, { label: 'Of portfolio', value: `${pct(list.length, total)}%` }],
      columns: [...this.assetColumns, { key: 'custodian', label: 'Custodian' }, { key: 'docs', label: 'Documents', num: true }],
      rows: list.map((a) => ({
        ...this.assetRows([a])[0],
        cells: {
          ...this.assetRows([a])[0].cells,
          custodian: a.custodianName ? `${a.custodianName}${a.custodianDepartment ? ' (' + a.custodianDepartment + ')' : ''}` : 'Not assigned', docs: a.documentCount,
        },
      })),
      empty: this.insightFailed.has(key) ? 'Could not load the assets.' : 'No data available',
      actions: [{ label: this.insight === key ? 'Clear table filter' : 'Show in table', run: () => this.openInsight(key) }],
    };
  }

  openSite(s: SiteMapRow): void {
    const on = this.site === s.name;
    const fmt = (iso: string | null) => (iso ? new Date(iso).toLocaleString() : '-');
    this.drill = {
      title: s.name, subtitle: `Site · last ${KPI_DAYS} days${this.type ? ` · ${this.type}` : ''}`,
      stats: [
        { label: 'Assets', value: s.totalAssets }, { label: 'Online', value: `${s.onlinePercent}%`, color: HEALTH_COLORS.Healthy },
        { label: 'Location Updates', value: s.updates }, { label: 'Tags Moved', value: s.tags.length },
      ],
      tableTitle: 'Location updates per tag',
      columns: [
        { key: 'tag', label: 'Tag ID' }, { key: 'count', label: 'Updates', num: true }, { key: 'at', label: 'Last Moved' },
        { key: 'from', label: 'From Gateway' }, { key: 'to', label: 'To Gateway' }, { key: 'pos', label: 'Last Position' },
      ],
      rows: [...s.tags].sort((a, b) => b.count - a.count).map((t) => ({
        cells: {
          tag: t.tagId, count: t.count, at: fmt(t.lastChangedAt), from: t.lastFromGatewayId ?? '-', to: t.lastToGatewayId ?? '-',
          pos: point(t.lastToLatitude, t.lastToLongitude)?.map((v) => v.toFixed(5)).join(', ') ?? '-',
        },
      })),
      empty: `No location updates in the last ${KPI_DAYS} days.`,
      actions: [{ label: on ? 'Clear site filter' : `Filter dashboard to ${s.name}`, run: () => this.pickSite(s.name) }],
    };
  }

  openAge(i: number): void {
    const b = this.aging?.bars[i];
    if (!b) return;
    const total = this.aging?.total ?? 0;
    this.drill = {
      title: `${b.band} Assets (${b.label})`, subtitle: `Asset lifecycle aging, from purchase date · ${this.scope}`,
      stats: [{ label: 'Assets', value: b.count, color: b.color }, { label: 'Of dated assets', value: `${pct(b.count, total)}%` }],
      columns: [
        { key: 'tag', label: 'Asset Tag' }, { key: 'name', label: 'Asset Name' }, { key: 'category', label: 'Category' }, { key: 'site', label: 'Site' },
        { key: 'status', label: 'Status' }, { key: 'purchased', label: 'Purchased' }, { key: 'age', label: 'Age (yrs)', num: true },
      ],
      rows: b.assets.map((a) => ({
        cells: {
          tag: a.assetTag ?? a.assetRegistrationId, name: a.assetName ?? '-', category: a.category ?? '-', site: a.siteName ?? '-',
          status: a.assetStatus ?? '-', purchased: this.fmtDate(a.purchaseDate), age: a.ageYears,
        },
      })),
      empty: 'No assets in this band.',
    };
  }

  private capitalize(v: string): string {
    return v.charAt(0).toUpperCase() + v.slice(1);
  }

  // ===== Navigation, export =====

  go(path: string): void {
    this.router.navigate([path]);
  }

  /** Opens the asset's detail page in the Assets app. */
  openDetails(a: QuickViewAsset): void {
    this.router.navigate(['/assets/overview'], { queryParams: { id: a.registrationId, tag: a.tag } });
  }

  exportReport(): void {
    const k = this.kpis;
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet([
      { Metric: 'As of', Value: isoDate(this.today) },
      { Metric: 'Filters', Value: `Site: ${this.site || 'All'}; Category: ${this.category || 'All'}; Type: ${this.type || 'All'}; Status: ${this.status || 'All'}` },
      { Metric: `KPI window`, Value: `Last ${KPI_DAYS} days` },
      { Metric: 'Total Assets', Value: k.total ?? '' }, { Metric: 'Active Assets', Value: k.active ?? '' },
      { Metric: 'Assets Under Maintenance', Value: k.maintenance ?? '' }, { Metric: 'Critical Assets', Value: k.critical ?? '' },
      { Metric: 'Avg Asset Health Score %', Value: k.health ?? '' },
    ]), 'Summary');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.byCategory.map((c) => ({ Category: c.name, Assets: c.count, '%': c.pct }))), 'By Category');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.byStatus.map((s) => ({ Status: s.status, Assets: s.count, '%': s.pct }))), 'By Status');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet((this.health?.rows ?? []).map((r) => ({ Health: r.band, Assets: r.count, '%': r.pct }))), 'By Health');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet((this.aging?.bars ?? []).map((b) => ({ Age: `${b.band} (${b.label})`, Assets: b.count, '%': b.pct }))), 'By Age');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.siteRows.map((s) => ({ Site: s.name, Assets: s.totalAssets, 'Online %': s.onlinePercent, [`Location Updates (${KPI_DAYS}d)`]: s.updates }))), 'By Site');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.tableRows.map((r) => ({
      'Asset Tag': r.tag, 'Asset Name': r.name, Category: r.category ?? '', Site: r.site ?? '', Criticality: r.criticality ?? '', Status: r.status ?? '',
      ...(r.due ? { Due: r.due.slice(0, 10) } : {}),
    }))), this.tableTitle.slice(0, 31));
    XLSX.writeFile(book, `Asset_Overview_${isoDate(this.today)}.xlsx`);
  }

  // ===== Formatting =====

  /** "Nov 18, 2026" from an ISO date or date-time. */
  fmtDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}, ${iso.slice(0, 4)}`;
  }

  stClass(s: string): string {
    return 'st--' + s.toLowerCase().replace(/ /g, '-');
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.asOfOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
  }
}
