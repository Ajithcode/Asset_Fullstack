import { AfterViewInit, Component, DestroyRef, ElementRef, HostListener, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, forkJoin, from, map, mergeMap, of, switchMap, timer, toArray } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import L from 'leaflet';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  Bell,
  BellRing,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Cog,
  Database,
  Download,
  Ellipsis,
  ExternalLink,
  FileText,
  Heart,
  House,
  Maximize,
  MapPin,
  Minus,
  Plus,
  Power,
  Radio,
  TriangleAlert,
  Warehouse,
  Wifi,
  WifiOff,
  Zap,
} from 'lucide-angular';
import { DeviceStatus, EventDay, EventSeries, HEALTH_COLORS, HealthBand, MapAsset, SERIES, STATUSES, STATUS_COLORS, dailyLocationUpdates } from './iot-overview-data';
import { ModuleWidgetsApi, RealTimeTags } from '../services/dashboard/module-widgets';
import { DrillDialog } from '../shared/drill-dialog/drill-dialog';
import { Drill, DrillColumn, DrillRow, pct } from '../shared/drill-dialog/drill';
import { AssetQuickView, QuickViewAsset, connectedAssetDrill, qvDate, qvDateTime, statusKey } from '../shared/asset-quick-view/quick-view';
import {
  AllSitesAssets, AssetDashboardKpis, ConnectedAssetDetail, Dashboard, DashboardAlert, DashboardFilters, DeviceConnectivity, EventsTimeline, FirmwareStatus, HealthScores,
  SensorsAttention, SiteAsset, SiteOption, TopSite, UtilizationByType,
} from '../services/dashboard/dashboard';

type Period = '7' | '30' | '90';
type QuickTab = 'overview' | 'location' | 'telemetry' | 'maintenance';
type AlertSort = 'time' | 'asset';
/** A widget source: null while loading, 'error' when the call failed. */
type Source<T> = T | null | 'error';

/** A row of "Recent Alerts & Exceptions": an alert from GET api/asset-dashboard/alerts with its asset's tag. */
interface AlertRow extends DashboardAlert {
  tag: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
// Event timeline geometry (SVG user units); the width follows the card's width (1 unit = 1px)
const TW = 520;
const TW_MIN = 280;
const TH = 170;
const TP = { l: 30, r: 8, t: 10, b: 22 };
/** Coverage: located within the last day. Freshness: reporting devices that updated within the hour. */
const COVERAGE_MIN = 24 * 60;
const FRESH_MIN = 60;
/** Connected / Online / Freshness cards use the Real-Time Location window and rule: 24 h, Online = status online and heard within 10 min. */
const TAG_WINDOW_MIN = 24 * 60;
const ONLINE_THRESHOLD_SEC = 10 * 60;
const TAG_POLL_MS = 10_000;
/** Alerts fetched for the table, KPIs and map (newest first). */
const ALERT_LIMIT = 500;
const SEVERITY_COLORS: Record<string, string> = { Critical: '#dc2626', Warning: '#d97706', Info: '#2563eb' };
const YES_NO: Record<string, string> = { Yes: '#16a34a', No: '#dc2626' };
const HEALTH_BANDS: HealthBand[] = ['Healthy', 'Warning', 'Critical', 'Unknown'];

@Component({
  selector: 'app-iot-tracking-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, DrillDialog],
  templateUrl: './iot-tracking-overview.html',
  styleUrls: ['./iot-tracking-overview.css', './iot-tracking-overview-panels.css'],
})
export class IotTrackingOverview implements AfterViewInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, ArrowUpDown, Bell, BellRing, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ClipboardList, Cog, Database, Download, Ellipsis, ExternalLink, FileText,
    Heart, House, Maximize, MapPin, Minus, Plus, Power, Radio, TriangleAlert, Warehouse, Wifi, WifiOff, Zap,
  };
  /** "Site" dropdown, from GET api/master-data/sites?clientId=. */
  sites: string[] = [];
  /** Master-data sites, to turn the Site dropdown's name into the site id the dashboard APIs filter by. */
  private masterSites: SiteOption[] = [];
  /** "Asset Type" dropdown, from GET api/master-data/asset-types. */
  types: string[] = [];
  readonly statuses = STATUSES;
  readonly statusColors = STATUS_COLORS;
  readonly healthColors = HEALTH_COLORS;
  readonly series = SERIES;
  tw = TW;
  readonly th = TH;
  readonly tp = TP;
  readonly Math = Math;

  // Filters
  period: Period = '30';
  site = '';
  type = '';
  status: DeviceStatus | '' = '';
  periodOpen = false;

  // Cards
  showAllSites = false;
  hiddenSeries = new Set<EventSeries>();
  timelinePeriod: Period = '30';
  hoverDay: number | null = null;
  hoverStatus: DeviceStatus | null = null;
  hoverHealth: HealthBand | null = null;
  alertSort: AlertSort = 'time';
  alertDesc = true;
  showAllAlerts = false;
  alertPage = 0;
  readonly alertPageSize = 10;
  menuOpen = '';
  selectedId = '';
  quickTab: QuickTab = 'overview';
  toast = '';
  /** Drill-down popup for the clicked widget element. */
  drill: Drill | null = null;

  @ViewChild('mapEl') private mapEl?: ElementRef<HTMLDivElement>;
  @ViewChild('chartEl') private chartEl?: ElementRef<HTMLDivElement>;
  private resizeObserver?: ResizeObserver;
  private map?: L.Map;
  private markerLayer?: L.LayerGroup;
  private selectedRing?: L.CircleMarker;
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private now = new Date();
  private readonly widgetsApi = inject(ModuleWidgetsApi);
  private readonly dashboardApi = inject(Dashboard);
  private readonly destroyRef = inject(DestroyRef);
  /** Asset Quick View: registered assets from GET api/asset-registrations, one at a time. */
  readonly qv = new AssetQuickView(this.dashboardApi, this.destroyRef);
  readonly statusKey = statusKey;
  readonly qvDate = qvDate;
  readonly qvDateTime = qvDateTime;

  /** KPI cards from GET api/asset-dashboard/kpis; null until the first response. */
  kpiData: AssetDashboardKpis | null = null;
  kpiLoading = false;
  kpiError = '';

  /** Connected Assets / Online Now / Data Freshness: GET {tracking}api/real-time-location/tags?minutes=1440, polled; null until the first response. */
  tagData: RealTimeTags | null = null;
  /** Assets with a linked tag heard in the window (one row per asset, its newest tag), newest first. */
  private tagConnected: ConnectedAssetDetail[] = [];

  /** Donut from GET api/asset-dashboard/device-connectivity; null until the first response. */
  connectivityData: DeviceConnectivity | null = null;
  connectivityLoading = false;

  /** IoT Events Timeline: daily location updates derived from GET api/asset-dashboard/location-updates, plus telemetry,
   * geofence events and alerts per day from GET api/asset-dashboard/events-timeline; null on error. */
  eventDays: EventDay[] | null = null;
  /** Series the timeline has data for. */
  eventSeries: EventSeries[] = [];
  /** Each day's location updates per tag, for the day popup. */
  private dayTags = new Map<string, { tagId: string; count: number }[]>();
  private timelineSub?: Subscription;
  timelineLoading = false;

  /** Sites ranked by the API (GET api/asset-dashboard/locations). */
  topSites: TopSite[] = [];
  sitesLoading = false;

  /** Registered assets with their network status and position: GET api/asset-dashboard/sites (filtered server-side). */
  fleetData: Source<AllSitesAssets> = null;
  /** Filtered assets; rebuilt whenever fleetData arrives. */
  devices: MapAsset[] = [];
  /** "Firmware Updates": GET api/asset-dashboard/firmware. */
  firmwareData: Source<FirmwareStatus> = null;
  /** "Sensors Requiring Attention": GET api/asset-dashboard/sensors-attention. */
  sensorsData: Source<SensorsAttention> = null;
  /** "Asset Health Score": GET api/asset-dashboard/health-scores. */
  healthData: Source<HealthScores> = null;
  /** "Utilization Rate (by Asset Type)": GET api/asset-dashboard/utilization?days=. */
  utilData: Source<UtilizationByType> = null;
  /** "Recent Alerts & Exceptions", the Alerts KPI's critical count and the map's alert markers: GET api/asset-dashboard/alerts?days=. */
  alertsData: Source<DashboardAlert[]> = null;
  private filterSubs: Subscription[] = [];

  constructor() {
    this.pollTags();
    this.loadKpis();
    this.loadConnectivity();
    this.loadSites();
    this.loadAssetTypes();
    this.loadSiteOptions();
    this.loadFiltered();
    this.qv.load();
  }

  /** Site dropdown: master-data sites plus the sites the dashboard API ranks (sites assets are mapped to). */
  get siteOptions(): string[] {
    const names = [...this.sites, ...this.topSites.map((s) => s.siteName)].filter(Boolean);
    return [...new Set(names)].sort((a, b) => a.localeCompare(b));
  }

  /** The site / type / status filters as the dashboard APIs take them (site by id). */
  private get filters(): DashboardFilters {
    const siteId = this.site
      ? this.masterSites.find((s) => s.siteName?.trim().toLowerCase() === this.site.trim().toLowerCase())?.id ?? this.topSites.find((s) => s.siteName === this.site)?.siteId ?? this.site
      : undefined;
    return { siteId, assetType: this.type || undefined, status: this.status || undefined };
  }

  /** Fetches every widget that follows the Site / Asset Type / Status filters (and the period, where it has one). */
  loadFiltered(): void {
    this.filterSubs.forEach((s) => s.unsubscribe());
    const f = this.filters;
    const days = Number(this.period);
    const take = <T>(req: Observable<T>, set: (v: Source<T>) => void, after?: () => void) => {
      set(null);
      return req.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (v) => {
          set(v);
          after?.();
        },
        error: () => {
          set('error');
          after?.();
        },
      });
    };
    this.filterSubs = [
      take(this.dashboardApi.getAllSiteAssets(f), (v) => (this.fleetData = v), () => this.onFleet()),
      take(this.dashboardApi.getFirmware(f), (v) => (this.firmwareData = v)),
      take(this.dashboardApi.getSensorsAttention(f), (v) => (this.sensorsData = v)),
      take(this.dashboardApi.getHealthScores(f), (v) => (this.healthData = v)),
      take(this.dashboardApi.getUtilization(days, f), (v) => (this.utilData = v)),
      take(this.dashboardApi.getAlerts(days, ALERT_LIMIT, f), (v) => (this.alertsData = v), () => this.drawMarkers()),
    ];
    this.loadTimeline();
  }

  /** Flattens GET api/asset-dashboard/sites into map assets and redraws the map. */
  private onFleet(): void {
    const res = this.ok(this.fleetData);
    const statusOf = (s: string) => STATUSES.find((x) => x.toLowerCase() === (s ?? '').toLowerCase()) ?? 'Unknown';
    this.devices = (res?.sites ?? []).flatMap((site) =>
      site.statuses.flatMap((g) =>
        g.assets.map((a: SiteAsset): MapAsset => ({
          id: a.assetRegistrationId, tag: a.assetTag || a.assetRegistrationId, name: a.assetName || '-', type: a.assetType || '-', site: site.siteName, siteId: site.siteId,
          status: statusOf(a.networkStatus), assetStatus: a.assetStatus, lat: a.latitude, lng: a.longitude,
          location: [a.buildingName, a.floorName, a.zoneName].filter(Boolean).join(' / ') || a.locationPath || null, gateway: a.gatewayId, lastSeen: a.lastSeenAt,
        })),
      ),
    );
    this.drawMarkers();
    this.fitToFilter();
  }

  /** The value of a loaded source, or null while loading / on error. */
  ok<T>(s: Source<T>): T | null {
    return s === 'error' ? null : s;
  }

  /** Empty-state text of a widget: loading, failed, or loaded with nothing to show. */
  emptyText(s: Source<unknown>): string {
    return s === null ? 'Loading…' : s === 'error' ? 'Could not load' : 'No data available';
  }

  /** Fills the Site dropdown with the client's active, non-deleted sites, sorted by name. */
  loadSiteOptions(): void {
    this.dashboardApi
      .getSites()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.masterSites = (res ?? []).filter((s) => !s.isDeleted && s.status !== 'Inactive');
          this.sites = [...new Set(this.masterSites.map((s) => s.siteName).filter(Boolean))].sort((a, b) => a.localeCompare(b));
        },
        error: () => {
          this.sites = [];
          this.showToast('Could not load site list');
        },
      });
  }

  /** Fills the Asset Type dropdown with the active, non-deleted types, sorted by name. */
  loadAssetTypes(): void {
    this.dashboardApi
      .getAssetTypes()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const names = (res ?? []).filter((t) => !t.isDeleted && t.status !== 'Inactive').map((t) => t.assetTypeName).filter(Boolean);
          this.types = [...new Set(names)].sort((a, b) => a.localeCompare(b));
        },
        error: () => {
          this.types = [];
          this.showToast('Could not load asset types');
        },
      });
  }

  /** Fetches every site for "Top Active Sites"; the widget shows the first 3 until View All. */
  loadSites(): void {
    this.sitesLoading = true;
    this.dashboardApi
      .getLocations(0)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.topSites = res.topSites ?? [];
          this.sitesLoading = false;
        },
        error: () => {
          this.topSites = [];
          this.sitesLoading = false;
          this.showToast('Could not load sites');
        },
      });
  }

  /** Fetches the Device Connectivity donut for the selected period. */
  loadConnectivity(): void {
    this.connectivityLoading = true;
    this.dashboardApi
      .getDeviceConnectivity(Number(this.period))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.connectivityData = res;
          this.connectivityLoading = false;
        },
        error: () => {
          this.connectivityData = null;
          this.connectivityLoading = false;
          this.showToast('Could not load device connectivity');
        },
      });
  }

  /** Fetches the IoT Events Timeline for its period and the dashboard's Site / Type / Status filters. */
  loadTimeline(): void {
    const n = Number(this.timelinePeriod);
    const filters = this.filters;
    // location updates: the API has no daily split, so fetch the "last k days" totals for k = 1..n and take the differences
    const windows$ = from(Array.from({ length: n }, (_, i) => i + 1)).pipe(
      mergeMap((k) => this.dashboardApi.getLocationUpdates(k, filters).pipe(map((res) => ({ k, res }))), 6),
      toArray(),
    );
    // telemetry, geofence events and alerts per day
    const events$ = this.dashboardApi.getEventsTimeline(n, filters).pipe(catchError(() => of(null as EventsTimeline | null)));
    this.timelineSub?.unsubscribe();
    this.timelineLoading = true;
    this.timelineSub = forkJoin({ list: windows$, events: events$ })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ list, events }) => {
          const windows = list.sort((a, b) => a.k - b.k).map(({ res }) => ({ from: res?.from ?? '', totalCount: res?.totalCount ?? 0, tags: res?.tags ?? [] }));
          const daily = dailyLocationUpdates(windows);
          const byDate = new Map((events?.points ?? []).map((p) => [p.date, p]));
          this.eventDays = daily.days.map((d) => {
            const e = byDate.get(d.date);
            return { ...d, telemetry: e?.telemetryData ?? 0, geofence: e?.geofenceEvents ?? 0, alerts: e?.alerts ?? 0 };
          });
          this.dayTags = daily.tagsByDay;
          const t = events?.totals;
          this.eventSeries = ['location', ...(t?.telemetryData ? ['telemetry' as const] : []), ...(t?.geofenceEvents ? ['geofence' as const] : []), ...(t?.alerts ? ['alerts' as const] : [])];
          this.hoverDay = null;
          this.timelineLoading = false;
        },
        error: () => {
          this.eventDays = null;
          this.dayTags.clear();
          this.eventSeries = [];
          this.timelineLoading = false;
          this.showToast('Could not load the events timeline');
        },
      });
  }

  /** Real-Time Location tags every few seconds, so Online Now and "x s ago" follow the live data. */
  private pollTags(): void {
    timer(0, TAG_POLL_MS)
      .pipe(
        switchMap(() => this.widgetsApi.getRealTimeTags(TAG_WINDOW_MIN).pipe(catchError(() => of(null as RealTimeTags | null)))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((res) => {
        if (!res) return; // keep the last good numbers; the cards fall back to the KPI API until the first response
        this.now = new Date();
        this.tagData = res;
        const seen = new Set<string>();
        this.tagConnected = [];
        for (const t of res.tags) {
          if (!t.asset || seen.has(t.asset.assetRegistrationId)) continue;
          seen.add(t.asset.assetRegistrationId);
          this.tagConnected.push({
            assetRegistrationId: t.asset.assetRegistrationId, assetTag: t.asset.assetTag ?? '', assetName: t.asset.assetName ?? '',
            assetType: t.asset.assetType ?? '', siteName: t.siteName ?? '', assetStatus: t.asset.status ?? '',
            networkStatus: (t.connection ?? '').toLowerCase(), gatewayId: t.gatewayName ?? t.gatewayId, firstSeenAt: t.timestamp, lastSeenAt: t.timestamp, readingCount: 0,
          });
        }
        this.applyReadingCounts();
      });
  }

  /** True when every connected asset's reading count is known (the KPI API serves it); otherwise the Readings column is hidden. */
  private readingsKnown = false;

  /** Readings per asset come from GET api/asset-dashboard/kpis (connectedAssetDetails); the tags API has no count. */
  private applyReadingCounts(): void {
    const counts = new Map((this.kpiData?.connectedAssetDetails ?? []).map((d) => [d.assetRegistrationId, d.readingCount]));
    this.readingsKnown = this.tagConnected.length > 0 && this.tagConnected.every((a) => counts.has(a.assetRegistrationId));
    this.tagConnected = this.tagConnected.map((a) => ({ ...a, readingCount: counts.get(a.assetRegistrationId) ?? 0 }));
    this.qv.setConnected(this.tagConnected);
  }

  /** Age of the newest reading of any tag in the window, in seconds; null when there is none. */
  get freshnessSec(): number | null {
    const newest = this.tagData?.tags.reduce((max, t) => Math.max(max, new Date(t.timestamp).getTime()), -Infinity);
    return newest === undefined || !Number.isFinite(newest) ? null : Math.max(0, Math.round((this.now.getTime() - newest) / 1000));
  }

  /** "6 s ago", "3 min ago", "2 h ago", "-" without readings. */
  get freshnessLabel(): string {
    const s = this.freshnessSec;
    return s === null ? '-' : s < 60 ? `${s} s ago` : s < 3600 ? `${Math.floor(s / 60)} min ago` : s < 86400 ? `${Math.floor(s / 3600)} h ago` : `${Math.floor(s / 86400)} d ago`;
  }

  /** Older than the Online threshold: the newest reading would no longer count as Online. */
  get freshnessStale(): boolean {
    const s = this.freshnessSec;
    return s === null || s > ONLINE_THRESHOLD_SEC;
  }

  /** Fetches the KPI cards for the selected period. */
  loadKpis(): void {
    this.kpiLoading = true;
    this.kpiError = '';
    this.dashboardApi
      .getKpis(Number(this.period))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.kpiData = res;
          this.kpiLoading = false;
          if (this.tagData) this.applyReadingCounts();
          else this.qv.setConnected(res?.connectedAssetDetails ?? []);
        },
        error: () => {
          this.kpiData = null;
          this.kpiLoading = false;
          this.kpiError = 'Could not load KPIs';
          this.showToast(this.kpiError);
        },
      });
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.observeSizes();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    this.resizeObserver?.disconnect();
    this.map?.remove();
  }

  /** The grid columns change width on window resize, breakpoints and sidebar collapse: keep the map tiles and the timeline in step. */
  private observeSizes(): void {
    this.resizeObserver = new ResizeObserver(() => {
      this.map?.invalidateSize();
      const width = this.chartEl?.nativeElement.clientWidth;
      if (!width) return;
      const tw = Math.max(TW_MIN, Math.round(width));
      // ResizeObserver callbacks run outside Angular's zone
      if (tw !== this.tw) this.zone.run(() => (this.tw = tw));
    });
    if (this.mapEl) this.resizeObserver.observe(this.mapEl.nativeElement);
    if (this.chartEl) this.resizeObserver.observe(this.chartEl.nativeElement);
  }

  apply(): void {
    this.alertPage = 0;
    this.loadFiltered();
  }

  setPeriod(p: Period): void {
    this.period = p;
    this.timelinePeriod = p;
    this.periodOpen = false;
    this.alertPage = 0;
    this.loadKpis();
    this.loadConnectivity();
    this.loadFiltered();
  }

  get periodLabel(): string {
    return `Last ${this.period} Days`;
  }

  get filtered(): boolean {
    return !!(this.site || this.type || this.status);
  }

  private minutesSince(v: string | null): number {
    return v ? (this.now.getTime() - new Date(v).getTime()) / 60_000 : Infinity;
  }

  /** Local yyyy-MM-dd of today, for file names. */
  private todayIso(): string {
    const d = this.now;
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }

  // ===== KPIs =====

  get kpis() {
    const api = this.kpiData;
    const round = (v: number | null | undefined) => (v === null || v === undefined ? null : Math.round(v * 10) / 10);
    // Connected / Online come from the Real-Time Location tags (same 24 h window and Online/Idle/Offline rule as that page)
    const tags = this.tagData ? this.tagConnected : null;
    const tagOnline = tags?.filter((a) => a.networkStatus === 'online').length ?? 0;
    return {
      connected: tags ? tags.length : api?.connectedAssets.value ?? 0,
      connectedChange: tags ? null : round(api?.connectedAssets.changePercent),
      online: tags ? tagOnline : api?.onlineNow.value ?? 0,
      onlinePct: tags ? (tags.length ? round((tagOnline / tags.length) * 100) ?? 0 : 0) : round(api?.onlineNow.percent) ?? 0,
      alerts: api?.alerts.value ?? 0,
      // the KPI API has no severity split: the critical count comes from GET api/asset-dashboard/alerts
      critical: this.periodAlerts.filter((a) => a.severity?.toLowerCase() === 'critical').length,
      coverage: round(api?.coverage.percent) ?? 0,
      coverageChange: round(api?.coverage.changePercent),
      freshness: round(api?.dataFreshness.percent) ?? 0,
      freshnessChange: round(api?.dataFreshness.changePercent),
    };
  }

  /** Connected assets returned with the KPIs. */
  get connectedAssets(): ConnectedAssetDetail[] {
    return this.tagData ? this.tagConnected : this.kpiData?.connectedAssetDetails ?? [];
  }

  private connectedColumns(...extra: DrillColumn[]): DrillColumn[] {
    return [
      { key: 'tag', label: 'Asset Tag' }, { key: 'name', label: 'Asset Name' }, { key: 'type', label: 'Type' }, { key: 'site', label: 'Site' },
      { key: 'network', label: 'Network', pill: { online: STATUS_COLORS.Online, idle: STATUS_COLORS.Idle, offline: STATUS_COLORS.Offline } },
      { key: 'gateway', label: 'Gateway' }, { key: 'lastSeen', label: 'Last Seen' },
      ...(this.tagData && !this.readingsKnown ? [] : [{ key: 'readings', label: 'Readings', num: true }]), ...extra,
    ];
  }

  private connectedRows(list: ConnectedAssetDetail[], extra: (a: ConnectedAssetDetail) => Record<string, string | number> = () => ({})): DrillRow[] {
    return list.map((a) => ({
      cells: {
        tag: a.assetTag, name: a.assetName, type: a.assetType, site: a.siteName, network: a.networkStatus,
        gateway: a.gatewayId, lastSeen: a.lastSeenAt ? this.fmtDateTime(a.lastSeenAt) : '-', readings: a.readingCount, ...extra(a),
      },
      open: () => connectedAssetDrill(a, (id) => this.showQuick(id)),
    }));
  }

  // ===== Sites =====

  get siteStats() {
    const rows = this.topSites.map((t) => ({
      site: { id: t.siteId, name: t.siteName, color: '#2563eb' },
      total: t.totalAssets, online: t.online, idle: t.idle, offline: t.offline + t.unknown,
      onlinePct: Math.round(t.onlinePercent * 10) / 10,
    }));
    return this.showAllSites ? rows : rows.slice(0, 3);
  }

  filterSite(name: string): void {
    this.site = this.site === name ? '' : name;
    this.apply();
  }

  // ===== Tracking health =====

  get connectivity() {
    const api = this.connectivityData;
    const bands = api?.bands ?? [];
    const total = api?.total ?? 0;
    const C = 2 * Math.PI * 48;
    let acc = 0;
    // keep the fixed status order; any extra band from the API goes last
    const names = [...STATUSES.filter((x) => x !== 'Not tracked'), ...bands.map((b) => b.name).filter((n) => !STATUSES.includes(n as DeviceStatus))];  // the donut counts tags; assets without a tag are not in it
    const rows = names.map((s) => {
      const band = bands.find((b) => b.name.toLowerCase() === s.toLowerCase());
      const count = band?.count ?? 0;
      const percent = band?.percent ?? 0;
      const len = total ? (count / total) * C : 0;
      const color = STATUS_COLORS[s as DeviceStatus] ?? '#94a3b8';
      const row = { status: s as DeviceStatus, count, percent, color, dash: `${Math.max(0, len - (count ? 2 : 0))} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, centrePercent: Math.round((api?.centrePercent ?? 0) * 10) / 10, rows };
  }

  /** Firmware of devices on registered assets whose model has a known latest release; null while loading, on error or with no such device. */
  get firmware() {
    const api = this.ok(this.firmwareData);
    if (!api?.totalDevices) return null;
    return { total: api.totalDevices, latest: api.onLatest, pct: Math.round(api.percent), models: api.models };
  }

  /** Sensors with an abnormal reading; null while loading or on error. */
  get attention(): number | null {
    return this.ok(this.sensorsData)?.count ?? null;
  }

  // ===== Asset health & utilization =====

  get health() {
    const api = this.ok(this.healthData);
    const total = api?.total ?? 0;
    const C = 2 * Math.PI * 48;
    let acc = 0;
    const rows = HEALTH_BANDS.map((b) => {
      const count = api?.bands.find((x) => x.name.toLowerCase() === b.toLowerCase())?.count ?? 0;
      const len = total ? (count / total) * C : 0;
      const row = { band: b, count, pct: total ? Math.round((count / total) * 100) : 0, color: HEALTH_COLORS[b], dash: `${Math.max(0, len - (count ? 2 : 0))} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows, healthyPct: rows[0].pct };
  }

  /** The donut needs at least one asset with a known (assessed) score; all-Unknown would draw a meaningless ring. */
  get hasHealthData(): boolean {
    const h = this.health;
    return h.total > 0 && h.rows.some((r) => r.band !== 'Unknown' && r.count > 0);
  }

  /** Running share (blue) and idle share (amber) per asset type, from GET api/asset-dashboard/utilization. */
  get utilizationRows() {
    return (this.ok(this.utilData)?.items ?? []).map((i) => ({
      type: i.assetType, running: Math.round(i.runningPercent), idle: Math.round(i.idlePercent), count: i.assetCount, runningHours: i.runningHours, idleHours: i.idleHours,
    }));
  }

  // ===== Events timeline =====

  /** Legend entries: only the series the API returned. */
  get timelineSeries() {
    return SERIES.filter((s) => this.eventSeries.includes(s.key));
  }

  /** Chart geometry; null while there is nothing to draw (no response, no dated rows, or no events in the window). */
  get timeline() {
    const vals = this.eventDays ?? [];
    const shown = this.timelineSeries;
    if (!vals.length || !shown.length || vals.every((v) => shown.every((s) => !v[s.key]))) return null;
    const visible = shown.filter((s) => !this.hiddenSeries.has(s.key));
    const max = Math.max(10, ...vals.flatMap((v) => visible.map((s) => v[s.key])));
    const step = max > 150 ? 50 : max > 60 ? 20 : 10;
    const top = Math.ceil(max / step) * step;
    const plotW = this.tw - TP.l - TP.r;
    const plotH = TH - TP.t - TP.b;
    const x = (i: number) => TP.l + (vals.length > 1 ? (i / (vals.length - 1)) * plotW : plotW / 2);
    const y = (v: number) => TH - TP.b - (v / top) * plotH;
    const lines = visible.map((s) => ({ ...s, d: vals.map((v, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(v[s.key]).toFixed(1)}`).join(''), pts: vals.map((v, i) => ({ x: x(i), y: y(v[s.key]) })) }));
    const ticks = Array.from({ length: top / step + 1 }, (_, i) => ({ y: y(i * step), label: String(i * step) }));
    const every = Math.max(1, Math.round(vals.length / 7));
    const labels = vals.map((v, i) => ({ x: x(i), label: this.shortDate(v.date), show: i % every === 0 })).filter((l) => l.show);
    const slot = plotW / Math.max(1, vals.length - 1);
    return { vals, lines, ticks, labels, x, slot };
  }

  toggleSeries(key: EventSeries): void {
    this.hiddenSeries.has(key) ? this.hiddenSeries.delete(key) : this.hiddenSeries.add(key);
    // keep at least one line
    if (this.timelineSeries.every((s) => this.hiddenSeries.has(s.key))) this.hiddenSeries.delete(key);
  }

  // ===== Alerts =====

  /** Alerts of the period on the filtered assets (the API filters by period, site, type and status). */
  get periodAlerts(): AlertRow[] {
    const tags = new Map(this.devices.map((d) => [d.id.toLowerCase(), d.tag]));
    return (this.ok(this.alertsData) ?? []).map((a) => ({ ...a, tag: tags.get((a.assetRegistrationId ?? '').toLowerCase()) ?? a.assetRegistrationId ?? a.deviceId ?? '-' }));
  }

  get alertRows(): AlertRow[] {
    const dir = this.alertDesc ? -1 : 1;
    return [...this.periodAlerts].sort((a, b) => dir * (this.alertSort === 'time' ? a.raisedAt.localeCompare(b.raisedAt) : a.tag.localeCompare(b.tag)));
  }

  get alertPageRows(): AlertRow[] {
    return this.showAllAlerts ? this.alertRows.slice(this.alertPage * this.alertPageSize, (this.alertPage + 1) * this.alertPageSize) : this.alertRows.slice(0, 5);
  }

  get alertPages(): number {
    return Math.max(1, Math.ceil(this.alertRows.length / this.alertPageSize));
  }

  get alertEmpty(): string {
    return this.alertsData === null ? 'Loading…' : this.alertsData === 'error' ? 'Could not load alerts.' : 'No alerts in this period.';
  }

  sortAlerts(by: AlertSort): void {
    if (this.alertSort === by) this.alertDesc = !this.alertDesc;
    else {
      this.alertSort = by;
      this.alertDesc = by === 'time';
    }
    this.alertPage = 0;
  }

  /** PATCH api/iot-alerts/{id}/status (Tracking service), then reloads the alerts. */
  setAlertStatus(a: AlertRow, status: string): void {
    this.menuOpen = '';
    this.dashboardApi
      .setAlertStatus(a.alertId, status)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.showToast(`${a.eventType ?? 'Alert'} on ${a.tag} marked ${status.toLowerCase()}.`);
          this.dashboardApi
            .getAlerts(Number(this.period), ALERT_LIMIT, this.filters)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({ next: (v) => ((this.alertsData = v), this.drawMarkers()), error: () => (this.alertsData = 'error') });
        },
        error: () => this.showToast('Could not update the alert status.'),
      });
  }

  // ===== Quick view =====

  get selected(): MapAsset | null {
    return this.devices.find((d) => d.id === this.selectedId) ?? null;
  }

  /** Highlights the asset on the map and shows it in the Quick View. */
  select(id: string | null, pan = false): void {
    if (!id) return;
    this.menuOpen = '';
    this.selectedId = id;
    this.markSelected(pan);
    this.showQuick(id);
  }

  /** Shows a registered asset in the Quick View. */
  showQuick(assetRegistrationId: string): void {
    this.quickTab = 'overview';
    this.qv.show(assetRegistrationId);
  }

  /** Pill colour of a registration status. */
  pillOf(status: string): string {
    const k = statusKey(status);
    return k === 'active' ? 'online' : /maintenance|repair|idle|transit/.test(k) ? 'idle' : /disposed|retired|inactive/.test(k) ? 'offline' : 'unknown';
  }

  /** Opens the asset's detail page in the Assets app. */
  openDetails(a: QuickViewAsset): void {
    this.router.navigate(['/assets/overview'], { queryParams: { id: a.registrationId, tag: a.tag } });
  }

  alertsOf(id: string): AlertRow[] {
    return this.periodAlerts.filter((a) => (a.assetRegistrationId ?? '').toLowerCase() === id.toLowerCase());
  }

  locate(d: MapAsset): void {
    this.select(d.id, true);
    this.mapEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  // ===== Map =====

  private initMap(): void {
    const el = this.mapEl?.nativeElement;
    if (!el) return;
    this.map = L.map(el, { zoomControl: false, attributionControl: true, preferCanvas: true, scrollWheelZoom: false });
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 19,
      attribution: 'Imagery &copy; Esri',
    }).addTo(this.map);
    this.markerLayer = L.layerGroup().addTo(this.map);
    this.drawMarkers();
    this.fitToFilter();
    // the card can resize after first paint (fonts, sidebar)
    setTimeout(() => this.map?.invalidateSize());
  }

  /** Assets with a position (registration latitude / longitude). */
  get pinned(): MapAsset[] {
    return this.devices.filter((d) => d.lat !== null && d.lng !== null && !(d.lat === 0 && d.lng === 0));
  }

  /** Assets with an unresolved critical alert in the period. */
  private get alerting(): Set<string> {
    return new Set(
      this.periodAlerts.filter((a) => a.status !== 'Resolved' && a.severity?.toLowerCase() === 'critical').map((a) => (a.assetRegistrationId ?? '').toLowerCase()),
    );
  }

  private drawMarkers(): void {
    if (!this.map || !this.markerLayer) return;
    this.markerLayer.clearLayers();
    const esc = (v: string) => v.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
    const alerting = this.alerting;
    for (const d of this.pinned) {
      const alert = alerting.has(d.id.toLowerCase());
      L.circleMarker([d.lat!, d.lng!], {
        radius: alert ? 7 : 5, weight: alert ? 2 : 1.5, color: '#fff', fillColor: alert ? '#f59e0b' : STATUS_COLORS[d.status], fillOpacity: 1,
      })
        .bindTooltip(`<strong>${esc(d.tag)}</strong><br>${esc(d.name)}<br>${d.status}${alert ? ' · Alert' : ''}`, { direction: 'top' })
        .on('click', () => this.zone.run(() => this.openDevice(d.id)))
        .addTo(this.markerLayer);
    }
    this.markSelected(false);
  }

  private markSelected(pan: boolean): void {
    const d = this.selected;
    this.selectedRing?.remove();
    if (!this.map || !d || d.lat === null || d.lng === null) return;
    this.selectedRing = L.circleMarker([d.lat, d.lng], { radius: 12, weight: 3, color: '#4f2bd8', fill: false, interactive: false }).addTo(this.map);
    if (pan) this.map.setView([d.lat, d.lng], Math.max(this.map.getZoom(), 18));
  }

  /** Fits the map to the pinned assets; a world view when none has a position. */
  private fitToFilter(): void {
    if (!this.map) return;
    const pts = this.pinned.map((d) => [d.lat!, d.lng!] as [number, number]);
    if (!pts.length) {
      this.map.setView([20, 0], 2);
      return;
    }
    this.map.fitBounds(L.latLngBounds(pts), { padding: [24, 24], maxZoom: 18 });
  }

  zoom(delta: number): void {
    delta > 0 ? this.map?.zoomIn() : this.map?.zoomOut();
  }

  resetView(): void {
    this.site = '';
    this.apply();
  }

  get legend() {
    const alerts = this.alerting.size;
    return [...(['Online', 'Idle', 'Offline', 'Not tracked'] as DeviceStatus[]).map((s) => ({ label: s, count: this.devices.filter((d) => d.status === s).length, color: STATUS_COLORS[s], alert: false })), { label: 'Alert', count: alerts, color: '#f59e0b', alert: true }];
  }

  /** Map note when there is nothing to pin. */
  get mapNote(): string {
    if (this.fleetData === null) return 'Loading assets…';
    if (this.fleetData === 'error') return 'Could not load asset locations.';
    if (!this.devices.length) return 'No data available';
    return this.pinned.length ? '' : `${this.devices.length} asset(s) have no map coordinates.`;
  }

  // ===== Drill-down popup =====

  private get scope(): string {
    return `${this.periodLabel} · ${[this.site, this.type, this.status].filter(Boolean).join(' · ') || 'All sites, types and statuses'}`;
  }

  private deviceColumns(...extra: DrillColumn[]): DrillColumn[] {
    return [
      { key: 'id', label: 'Asset Tag' }, { key: 'name', label: 'Asset Name' }, { key: 'type', label: 'Type' }, { key: 'site', label: 'Site' },
      { key: 'status', label: 'Network', pill: STATUS_COLORS }, ...extra, { key: 'updated', label: 'Last Seen' },
    ];
  }

  private deviceRows(list: MapAsset[], extra: (d: MapAsset) => Record<string, string | number> = () => ({})): DrillRow[] {
    return list.map((d) => ({
      cells: { id: d.tag, name: d.name, type: d.type, site: d.site, status: d.status, updated: d.lastSeen ? this.fmtDateTime(d.lastSeen) : '-', ...extra(d) },
      open: () => this.deviceDrill(d),
    }));
  }

  private alertColumns(withAsset: boolean): DrillColumn[] {
    return [
      { key: 'time', label: 'Time' },
      ...(withAsset ? [{ key: 'id', label: 'Asset' }, { key: 'name', label: 'Asset Name' }, { key: 'site', label: 'Site' }] : []),
      { key: 'event', label: 'Event Type' }, { key: 'severity', label: 'Severity', pill: SEVERITY_COLORS }, { key: 'status', label: 'Status' },
    ];
  }

  private alertRowsOf(list: AlertRow[]): DrillRow[] {
    return [...list].sort((a, b) => b.raisedAt.localeCompare(a.raisedAt)).map((a) => ({
      cells: { time: this.fmtDateTime(a.raisedAt), id: a.tag, name: a.assetName ?? '-', site: a.siteName ?? '-', event: a.eventType ?? '-', severity: a.severity ?? '-', status: a.status ?? '-' },
      open: () => this.alertDrill(a),
    }));
  }

  private alertStats(list: AlertRow[]) {
    return [
      { label: 'Alerts', value: list.length },
      ...(['Critical', 'Warning', 'Info'] as const).map((s) => ({ label: s, value: list.filter((a) => a.severity?.toLowerCase() === s.toLowerCase()).length, color: SEVERITY_COLORS[s] })),
      { label: 'Unresolved', value: list.filter((a) => a.status !== 'Resolved').length },
    ];
  }

  private deviceDrill(d: MapAsset): Drill {
    const alerts = this.alertsOf(d.id);
    this.showQuick(d.id);
    return {
      title: `${d.tag} · ${d.name}`,
      subtitle: `${d.type} · ${d.site}`,
      stats: [
        { label: 'Network', value: d.status, color: STATUS_COLORS[d.status] },
        { label: 'Asset Status', value: d.assetStatus ?? '-' },
        { label: `Alerts (${this.period} days)`, value: alerts.length },
      ],
      fields: [
        { label: 'Location', value: d.location ?? '-' }, { label: 'Gateway', value: d.gateway ?? '-' },
        { label: 'Last Seen', value: d.lastSeen ? this.fmtDateTime(d.lastSeen) : '-' },
        { label: 'Position', value: d.lat !== null && d.lng !== null ? `${d.lat.toFixed(5)}, ${d.lng.toFixed(5)}` : '-' },
      ],
      tableTitle: 'Alert history',
      columns: this.alertColumns(false),
      rows: this.alertRowsOf(alerts),
      empty: 'No alerts in this period.',
      actions: d.lat !== null ? [{ label: 'Show on map', run: () => this.locate(d) }] : [],
    };
  }

  private alertDrill(a: AlertRow): Drill {
    const sev = Object.keys(SEVERITY_COLORS).find((s) => s.toLowerCase() === (a.severity ?? '').toLowerCase());
    return {
      title: a.eventType ?? 'Alert',
      subtitle: [a.tag, a.assetName, a.siteName].filter(Boolean).join(' · '),
      stats: [
        { label: 'Severity', value: a.severity ?? '-', color: sev ? SEVERITY_COLORS[sev] : undefined },
        { label: 'Status', value: a.status ?? '-' },
        { label: 'Raised', value: this.ago(a.raisedAt) },
      ],
      fields: [
        { label: 'Time', value: this.fmtDateTime(a.raisedAt) }, { label: 'Device', value: a.deviceId ?? '-' },
        { label: 'Message', value: a.message ?? '-' },
      ],
      tableTitle: `Other alerts on ${a.tag}`,
      columns: this.alertColumns(false),
      rows: this.alertRowsOf(a.assetRegistrationId ? this.alertsOf(a.assetRegistrationId).filter((x) => x.alertId !== a.alertId) : []),
      empty: 'No other alerts on this asset.',
      actions: [
        ...(a.status !== 'Resolved' ? [{ label: 'Mark resolved', run: () => this.setAlertStatus(a, 'Resolved') }] : []),
        ...(a.assetRegistrationId ? [{ label: 'Show asset', run: () => this.select(a.assetRegistrationId, true) }] : []),
      ],
    };
  }

  openKpi(kind: 'connected' | 'online' | 'alerts' | 'coverage' | 'freshness'): void {
    const k = this.kpis;
    const connected = this.connectedAssets;
    const yesNo = (v: boolean) => (v ? 'Yes' : 'No');
    if (kind === 'connected') {
      this.drill = {
        title: 'Connected Assets', subtitle: this.tagData ? 'Assets with a linked tag heard in the last 24 hours (Real-Time Location)' : this.kpiData?.connectedAssets.note ?? this.scope,
        stats: [{ label: 'Connected', value: k.connected }, { label: 'Registered', value: this.kpiData?.totalAssets ?? 0 }],
        columns: this.connectedColumns(), rows: this.connectedRows(connected), empty: 'No connected assets.',
      };
    } else if (kind === 'online') {
      const online = connected.filter((a) => a.networkStatus?.toLowerCase() === 'online');
      this.drill = {
        title: 'Online Now', subtitle: this.tagData ? 'Status online and heard within 10 minutes (Real-Time Location)' : this.kpiData?.onlineNow.note ?? this.scope,
        stats: [{ label: 'Online', value: k.online, color: STATUS_COLORS.Online }, { label: 'Of connected assets', value: `${k.onlinePct}%` }],
        columns: this.connectedColumns(), rows: this.connectedRows(online), empty: 'No assets online.',
      };
    } else if (kind === 'alerts') {
      this.drill = { title: 'Alerts', subtitle: this.scope, stats: this.alertStats(this.periodAlerts), columns: this.alertColumns(true), rows: this.alertRowsOf(this.periodAlerts), empty: this.alertEmpty };
    } else if (kind === 'coverage') {
      const located = (a: ConnectedAssetDetail) => this.minutesSince(a.lastSeenAt) <= COVERAGE_MIN;
      const n = connected.filter(located).length;
      this.drill = {
        title: 'Coverage', subtitle: this.kpiData?.coverage.note ?? `Connected assets located within the last 24 hours · ${this.scope}`,
        stats: [{ label: 'Coverage', value: `${k.coverage}%` }, { label: 'Located (24 h)', value: n, color: '#16a34a' }, { label: 'Not located', value: connected.length - n, color: '#dc2626' }],
        columns: this.connectedColumns({ key: 'located', label: 'Located', pill: YES_NO }),
        rows: this.connectedRows([...connected].sort((a, b) => Number(located(a)) - Number(located(b))), (a) => ({ located: yesNo(located(a)) })),
        empty: 'No connected assets.',
      };
    } else {
      // with the Real-Time Location data a reading is fresh while the tag still counts as Online (10 min)
      const freshMin = this.tagData ? ONLINE_THRESHOLD_SEC / 60 : FRESH_MIN;
      const fresh = (a: ConnectedAssetDetail) => this.minutesSince(a.lastSeenAt) <= freshMin;
      const n = connected.filter(fresh).length;
      this.drill = {
        title: 'Data Freshness', subtitle: this.tagData ? `Newest reading ${this.freshnessLabel} · connected assets updated within ${freshMin} min` : this.kpiData?.dataFreshness.note ?? `Connected assets updated within the last hour · ${this.scope}`,
        stats: [{ label: 'Freshness', value: this.tagData ? this.freshnessLabel : `${k.freshness}%` }, { label: `Fresh (${freshMin} min)`, value: n, color: '#16a34a' }, { label: 'Stale', value: connected.length - n, color: '#dc2626' }],
        columns: this.connectedColumns({ key: 'fresh', label: this.tagData ? 'Within 10 min' : 'Within SLA', pill: YES_NO }),
        rows: this.connectedRows([...connected].sort((a, b) => Number(fresh(a)) - Number(fresh(b))), (a) => ({ fresh: yesNo(fresh(a)) })),
        empty: 'No connected assets.',
      };
    }
  }

  /** Site popup from GET api/asset-dashboard/sites/{siteId}. */
  openSite(name: string, siteId: string): void {
    const on = this.site === name;
    const action = { label: on ? 'Clear site filter' : `Filter dashboard to ${name}`, run: () => this.filterSite(name) };
    this.dashboardApi
      .getSite(siteId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const assets: SiteAsset[] = res.statuses.flatMap((s) => s.assets);
          const where = [res.siteType, res.city, res.country].filter(Boolean).join(' · ');
          this.drill = {
            title: res.siteName, subtitle: `Site${where ? ' · ' + where : ''}`,
            stats: [
              { label: 'Assets', value: res.totalAssets },
              ...STATUSES.map((st) => ({ label: st, value: res.statuses.find((x) => x.status === st)?.count ?? 0, color: STATUS_COLORS[st] })),
              { label: 'Online %', value: `${Math.round(res.onlinePercent * 10) / 10}%` },
            ],
            columns: [
              { key: 'tag', label: 'Asset Tag' }, { key: 'name', label: 'Asset Name' }, { key: 'type', label: 'Type' },
              { key: 'network', label: 'Network', pill: STATUS_COLORS }, { key: 'location', label: 'Location' },
              { key: 'gateway', label: 'Gateway' }, { key: 'lastSeen', label: 'Last Seen' },
            ],
            rows: assets.map((a) => ({
              cells: {
                tag: a.assetTag, name: a.assetName, type: a.assetType, network: a.networkStatus,
                location: [a.buildingName, a.floorName, a.zoneName].filter(Boolean).join(' / ') || '-',
                gateway: a.gatewayId ?? '-', lastSeen: a.lastSeenAt ? this.fmtDateTime(a.lastSeenAt) : '-',
              },
            })),
            empty: 'No assets at this site.',
            actions: [action],
          };
        },
        error: () => this.showToast(`Could not load ${name}`),
      });
  }

  openStatus(s: DeviceStatus): void {
    const list = this.devices.filter((d) => d.status === s);
    const on = this.status === s;
    this.drill = {
      title: `${s} Assets`, subtitle: this.scope,
      stats: [{ label: s, value: list.length, color: STATUS_COLORS[s] }, { label: 'Of registered assets', value: `${pct(list.length, this.devices.length)}%` }],
      columns: this.deviceColumns({ key: 'gateway', label: 'Gateway' }),
      rows: this.deviceRows(list, (d) => ({ gateway: d.gateway ?? '-' })),
      empty: this.fleetData === 'error' ? 'Could not load the assets.' : 'No assets.',
      actions: [{ label: on ? 'Clear status filter' : `Filter dashboard to ${s}`, run: () => { this.status = on ? '' : s; this.apply(); } }],
    };
  }

  /** Map legend "Alert": assets with an unresolved critical alert in the period. */
  openAlerting(): void {
    const list = this.periodAlerts.filter((a) => a.status !== 'Resolved' && a.severity?.toLowerCase() === 'critical');
    this.drill = { title: 'Unresolved Critical Alerts', subtitle: this.scope, stats: this.alertStats(list), columns: this.alertColumns(true), rows: this.alertRowsOf(list), empty: 'No unresolved critical alerts.' };
  }

  openFirmware(): void {
    const f = this.firmware;
    if (!f) return;
    this.drill = {
      title: 'Firmware Updates', subtitle: `Devices on registered assets, by model · ${this.scope}`,
      stats: [{ label: 'On latest', value: f.latest, color: '#16a34a' }, { label: 'Update available', value: f.total - f.latest, color: '#dc2626' }, { label: 'Up to date', value: `${f.pct}%` }],
      columns: [{ key: 'model', label: 'Device Model' }, { key: 'latest', label: 'Latest Version' }, { key: 'devices', label: 'Devices', num: true }, { key: 'on', label: 'On Latest', num: true }],
      rows: f.models.map((m) => ({ cells: { model: m.deviceModel, latest: m.latestVersion, devices: m.devices, on: m.onLatest } })),
      empty: 'No devices.',
    };
  }

  openAttention(): void {
    const api = this.ok(this.sensorsData);
    if (!api) return;
    this.drill = {
      title: 'Sensors Requiring Attention', subtitle: `Sensors with status Attention (abnormal reading) · ${this.scope}`,
      stats: [{ label: 'Sensors', value: api.count, color: '#ea580c' }],
      columns: [
        { key: 'sensor', label: 'Sensor' }, { key: 'type', label: 'Type' }, { key: 'param', label: 'Parameter' }, { key: 'reading', label: 'Reading' },
        { key: 'asset', label: 'Asset' }, { key: 'device', label: 'Device' },
      ],
      rows: api.sensors.map((s) => ({
        cells: {
          sensor: s.name ?? s.sensorId, type: s.sensorType ?? '-', param: s.measuredParameter ?? '-',
          reading: s.liveReading === null ? '-' : `${s.liveReading}${s.unit ? ' ' + s.unit : ''}`, asset: s.assetName ?? s.assetRegistrationId ?? '-', device: s.deviceId ?? '-',
        },
      })),
      empty: 'No sensors with abnormal readings.',
    };
  }

  /** Health band popup: the API returns counts per band only (no asset list). */
  openHealth(band: HealthBand): void {
    const row = this.health.rows.find((r) => r.band === band);
    if (!row) return;
    this.drill = {
      title: `${band} Assets`, subtitle: `Latest condition assessment per asset · ${[this.site, this.type, this.status].filter(Boolean).join(' · ') || 'All sites, types and statuses'}`,
      stats: [
        { label: band, value: row.count, color: HEALTH_COLORS[band] }, { label: 'Of assets', value: `${row.pct}%` },
        { label: band === 'Unknown' ? 'Rule' : 'Score', value: { Healthy: '70-100', Warning: '40-69', Critical: 'below 40', Unknown: 'never assessed' }[band] },
      ],
      actions: [{ label: 'Open Asset Health Review', run: () => this.go('/assets/health-score') }],
    };
  }

  openType(t: string): void {
    const u = this.utilizationRows.find((x) => x.type === t);
    const on = this.type === t;
    this.drill = {
      title: t, subtitle: `Utilization by asset type · ${this.scope}`,
      stats: [
        { label: 'Assets', value: u?.count ?? 0 }, { label: 'Running', value: `${u?.running ?? 0}%`, color: '#2f6fe4' }, { label: 'Idle', value: `${u?.idle ?? 0}%`, color: '#d97706' },
        { label: 'Running hours', value: u?.runningHours ?? 0 }, { label: 'Idle hours', value: u?.idleHours ?? 0 },
      ],
      actions: [{ label: on ? 'Clear type filter' : `Filter dashboard to ${t}`, run: () => { this.type = on ? '' : t; this.apply(); } }],
    };
  }

  openDay(i: number): void {
    const v = this.timeline?.vals[i];
    if (!v) return;
    const tags = this.dayTags.get(v.date) ?? [];
    this.drill = {
      title: `IoT Events · ${this.fmtDate(v.date)}`, subtitle: this.scope,
      stats: [...this.timelineSeries.map((s) => ({ label: s.label, value: v[s.key], color: s.color })), { label: 'Tags Moved', value: tags.length }],
      tableTitle: 'Location updates per tag',
      columns: [{ key: 'tag', label: 'Tag ID' }, { key: 'count', label: 'Location Updates', num: true }],
      rows: tags.map((t) => ({ cells: { tag: t.tagId, count: t.count } })),
      empty: 'No location updates this day.',
    };
  }

  openDevice(id: string | null): void {
    const d = this.devices.find((x) => x.id === id);
    if (!d) {
      if (id) this.showQuick(id);
      return;
    }
    this.selectedId = d.id;
    this.markSelected(false);
    this.drill = this.deviceDrill(d);
  }

  openAlert(a: AlertRow): void {
    if (a.assetRegistrationId) {
      this.selectedId = a.assetRegistrationId;
      this.markSelected(false);
      this.showQuick(a.assetRegistrationId);
    }
    this.drill = this.alertDrill(a);
  }

  // ===== Navigation, export =====

  go(path: string): void {
    this.router.navigate([path]);
  }

  exportReport(): void {
    const k = this.kpis;
    const f = this.firmware;
    const book = XLSX.utils.book_new();
    const filters = `Period: ${this.periodLabel}; Site: ${this.site || 'All'}; Type: ${this.type || 'All'}; Status: ${this.status || 'All'}`;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet([
      { Metric: 'Filters', Value: filters },
      { Metric: 'Connected Assets', Value: k.connected }, { Metric: 'Online Now', Value: k.online }, { Metric: 'Online %', Value: k.onlinePct },
      { Metric: 'Alerts', Value: k.alerts }, { Metric: 'Critical Alerts', Value: k.critical }, { Metric: 'Coverage %', Value: k.coverage }, { Metric: 'Data Freshness %', Value: k.freshness },
      { Metric: 'Devices on latest firmware', Value: f ? `${f.latest} / ${f.total}` : '' }, { Metric: 'Sensors requiring attention', Value: this.attention ?? '' },
    ]), 'Summary');
    const showAll = this.showAllSites;
    this.showAllSites = true;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.siteStats.map((s) => ({ Site: s.site.name, Assets: s.total, Online: s.online, Idle: s.idle, 'Offline / Unknown': s.offline, 'Online %': s.onlinePct }))), 'Sites');
    this.showAllSites = showAll;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.alertRows.map((a) => ({
      Time: this.fmtDateTime(a.raisedAt), Asset: a.tag, 'Asset Name': a.assetName ?? '', Site: a.siteName ?? '', 'Event Type': a.eventType ?? '', Severity: a.severity ?? '', Status: a.status ?? '',
    }))), 'Alerts');
    XLSX.writeFile(book, `IoT_Tracking_Overview_${this.todayIso()}.xlsx`);
  }

  // ===== Formatting =====

  shortDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}`;
  }

  /** Local date and time of an ISO timestamp. */
  fmtDateTime(v: string): string {
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return v;
    const p = (n: number) => String(n).padStart(2, '0');
    return `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  fmtDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}, ${iso.slice(0, 4)}`;
  }

  ago(v: string): string {
    const m = Math.round(this.minutesSince(v));
    return m < 60 ? `${m} min ago` : m < 1440 ? `${Math.round(m / 60)} h ago` : `${Math.round(m / 1440)} d ago`;
  }

  sevClass(s: string | null): string {
    return 'sev--' + (s ?? 'info').toLowerCase();
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
    this.periodOpen = false;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.periodOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
