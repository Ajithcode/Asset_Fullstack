import { AfterViewInit, Component, DestroyRef, ElementRef, HostListener, NgZone, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subscription, catchError, forkJoin, of } from 'rxjs';
import { ExportMenu, ExportRequest, pushToast } from 'shared-ui';
import {
  LucideAngularModule, Activity, Battery, BatteryLow, Bell, Box, Building2, Calendar, Clock, Cpu, Download, Filter, Heart,
  History, Layers, Link, MapPin, RadioTower, Search, Signal, TriangleAlert, Wifi, WifiOff, X,
} from 'lucide-angular';
import { apiErrorMessage } from '../shared/tracking-api';
import { FloorPlanMap } from '../geofence-alerts/floor-plan-map';
import { FloorMapApiService, FloorOption, FloorPlanRef, GatewayFeed, PlanPlot, PlanZone } from '../geofence-alerts/floor-map-api.service';
import { AssetTrend, EventPage, MetricSeries, RangeKey, TelemetryEvent, TelemetryOverview, TelemetryOverviewApi, TrackedAsset } from './telemetry-overview-api';

type Tab = 'live' | 'location' | 'device' | 'alerts' | 'history';
type Agg = 'avg' | 'min' | 'max';
type LiveState = 'connecting' | 'live' | 'stale' | 'error';

interface Band { key: string; label: string; unit: string; color: string; top: number; h: number; path: string; ticks: { y: number; label: string }[]; threshold: { y: number; label: string } | null }

/** Live data refresh (the readings arrive every few seconds). */
const POLL_MS = 15000;
const RANGES: { key: RangeKey; label: string }[] = [{ key: '1h', label: 'Last 1 hour' }, { key: '24h', label: 'Last 24 hours' }, { key: '7d', label: 'Last 7 days' }];
const METRIC_COLORS: Record<string, string> = { rssi: '#0d9488', battery: '#16a34a', temperature: '#7c3aed', vibration: '#0891b2' };
const CHART = { l: 56, r: 18, band: 118, gap: 26, axis: 24 };

/**
 * Tracking & Telemetry: operational monitoring of connected assets — where each asset is (estimated from the
 * gateway hearing its tag), what its devices report, and whether everything is normal. Data comes from
 * GET /api/tracking-telemetry (one row per asset; devices counted separately). Device configuration, full alert
 * management and location history stay in their own modules (IoT Device Management, Geofence & Alerts,
 * Real-Time Location).
 */
@Component({
  selector: 'app-tracking-telemetry',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FloorPlanMap, ExportMenu],
  templateUrl: './tracking-telemetry.html',
  styleUrls: ['./tracking-telemetry.css', './tracking-telemetry-panels.css', './tracking-telemetry-v2.css', '../shared/iot-purple-theme.css'],
})
export class TrackingTelemetry implements OnInit, AfterViewInit, OnDestroy {
  /** Map legend: a strip under the map; in full screen a collapsed "Legend" button so it does not cover the plan. */
  fullscreen = false;
  legendOpen = false;
  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.fullscreen = !!document.fullscreenElement;
    if (!this.fullscreen) this.legendOpen = false;
  }

  readonly icons = { Activity, Battery, BatteryLow, Bell, Box, Building2, Calendar, Clock, Cpu, Download, Filter, Heart, History, Layers, Link, MapPin, RadioTower, Search, Signal, TriangleAlert, Wifi, WifiOff, X };
  readonly ranges = RANGES;

  private readonly api = inject(TelemetryOverviewApi);
  private readonly floorApi = inject(FloorMapApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly zone = inject(NgZone);
  @ViewChild('chartBox') private chartBox?: ElementRef<HTMLDivElement>;

  // ---- filters
  range: RangeKey = '24h';
  site = '';
  typeFilter = '';
  techFilter = '';
  statusFilter = '';
  query = '';

  // ---- data
  data: TelemetryOverview | null = null;
  loading = true;
  error = '';
  liveState: LiveState = 'connecting';
  lastUpdated: Date | null = null;
  now = new Date();
  sites: { id: string; name: string }[] = [];

  // ---- selection
  selectedId = '';
  tab: Tab = 'live';
  trend: AssetTrend | null = null;
  trendLoading = false;
  show: Record<string, boolean> = { rssi: true, battery: true, temperature: true, vibration: true };
  agg: Agg = 'avg';
  hoverIdx: number | null = null;
  chartW = 1000;

  // ---- floor plan map
  floors: FloorOption[] = [];
  private plans = new Map<string, FloorPlanRef>();
  mapFloorId = '';
  planZones: PlanZone[] = [];
  planPlots: PlanPlot[] = [];
  feed: Record<string, GatewayFeed> = {};
  showAssetsLayer = true;
  showDevicesLayer = true;
  showLabels = true;

  // ---- events table (paged by the backend)
  evPage = 1;
  evPageSize = 10;
  readonly evPageSizes = [10, 25, 50];
  evCategory = '';
  readonly evCategories = ['Location', 'Telemetry', 'Connectivity', 'Device'];
  evData: EventPage | null = null;
  evLoading = false;
  private evSub?: Subscription;

  toast = '';
  private timers: ReturnType<typeof setInterval>[] = [];
  private loadSub?: Subscription;
  private trendSub?: Subscription;
  private planSub?: Subscription;
  private resize?: ResizeObserver;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.refresh();
    this.loadFloors();
    this.timers.push(setInterval(() => this.refresh(), POLL_MS));
    this.timers.push(setInterval(() => (this.now = new Date()), 1000));
    this.floorApi.gatewaysLive().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((m) => {
      if (!m.data) return;
      const next: Record<string, GatewayFeed> = {};
      for (const g of m.data) next[g.gatewayId.toLowerCase()] = g;
      this.feed = next;
    });
  }

  ngAfterViewInit(): void {
    const el = this.chartBox?.nativeElement;
    if (!el) return;
    this.resize = new ResizeObserver(() => {
      const w = Math.round(el.clientWidth);
      if (w > 0 && Math.abs(w - this.chartW) > 2) this.zone.run(() => (this.chartW = w));
    });
    this.resize.observe(el);
  }

  ngOnDestroy(): void {
    this.timers.forEach((t) => clearInterval(t));
    clearTimeout(this.toastTimer);
    this.resize?.disconnect();
    this.loadSub?.unsubscribe();
    this.trendSub?.unsubscribe();
    this.planSub?.unsubscribe();
    this.evSub?.unsubscribe();
  }

  // ================================================================= loading

  refresh(): void {
    if (this.liveState !== 'connecting') this.liveState = this.liveState === 'error' ? 'error' : 'live';
    this.loadSub?.unsubscribe();
    this.loadSub = this.api.overview(this.range, this.site).subscribe({
      next: (d) => {
        this.data = d;
        this.loading = false;
        this.error = '';
        this.liveState = 'live';
        this.lastUpdated = new Date();
        this.collectSites(d);
        this.loadEvents();
        if (!this.selectedId || !d.assets.some((a) => a.assetId === this.selectedId)) this.select(this.filtered[0]?.assetId ?? d.assets[0]?.assetId ?? '', false);
        else this.loadTrend(false);
      },
      error: (err) => {
        this.loading = false;
        const msg = apiErrorMessage(err, 'Could not load tracking & telemetry data.');
        if (msg !== this.error) this.showToast(msg);
        this.error = msg;
        this.liveState = 'error';
      },
    });
  }

  setRange(r: RangeKey): void {
    this.range = r;
    this.evPage = 1;
    this.hoverIdx = null;
    this.refresh();
    this.loadTrend(true);
  }

  setSite(): void {
    this.evPage = 1;
    this.refresh();
  }

  /** An asset filter changed: the events table goes back to its first page. */
  onFilters(): void {
    this.evPage = 1;
    this.loadEvents();
  }

  private collectSites(d: TelemetryOverview): void {
    const seen = new Map(this.sites.map((s) => [s.id, s.name]));
    for (const a of d.assets) {
      const id = a.location?.siteId ?? a.siteId;
      const name = a.location?.siteName ?? a.siteName;
      if (id && name) seen.set(id, name);
    }
    this.sites = [...seen].map(([id, name]) => ({ id, name })).sort((x, y) => x.name.localeCompare(y.name));
  }

  /** Live indicator: live while refreshes succeed; stale when the last good update is over a minute old. */
  get liveLabel(): { cls: string; text: string; detail: string } {
    if (this.liveState === 'connecting') return { cls: 'is-wait', text: 'CONNECTING', detail: '' };
    const ago = this.lastUpdated ? Math.round((this.now.getTime() - this.lastUpdated.getTime()) / 1000) : null;
    if (this.liveState === 'error') return { cls: 'is-off', text: 'OFFLINE', detail: this.lastUpdated ? `last update ${this.time(this.lastUpdated)}` : 'no data yet' };
    if (ago !== null && ago > 60) return { cls: 'is-warn', text: 'STALE', detail: `updated ${this.agoText(this.lastUpdated)}` };
    return { cls: '', text: 'LIVE', detail: ago === null ? '' : `updated ${ago}s ago` };
  }

  // ================================================================= filters / list

  get assets(): TrackedAsset[] {
    return this.data?.assets ?? [];
  }

  get assetTypes(): string[] {
    return [...new Set(this.assets.map((a) => a.assetType).filter((x): x is string => !!x))].sort();
  }

  get technologies(): string[] {
    return [...new Set(this.assets.flatMap((a) => a.tags.map((t) => t.technology)).filter((x): x is string => !!x))].sort();
  }

  get filtered(): TrackedAsset[] {
    const q = this.query.trim().toLowerCase();
    return this.assets.filter(
      (a) =>
        (!this.typeFilter || a.assetType === this.typeFilter) &&
        (!this.techFilter || a.tags.some((t) => t.technology === this.techFilter)) &&
        (!this.statusFilter || a.connectivity === this.statusFilter) &&
        (!q || `${a.name} ${a.assetId} ${a.assetTag ?? ''} ${a.tags.map((t) => t.tagId).join(' ')} ${a.location?.zoneName ?? ''}`.toLowerCase().includes(q))
    );
  }

  get selected(): TrackedAsset | null {
    return this.assets.find((a) => a.assetId === this.selectedId) ?? null;
  }

  select(id: string, resetTab = true): void {
    if (!id) {
      this.selectedId = '';
      this.trend = null;
      return;
    }
    const changed = id !== this.selectedId;
    this.selectedId = id;
    if (resetTab && changed) this.tab = 'live';
    // show the asset's floor on the map
    const plan = this.selected?.location?.floorPlanId;
    if (plan) {
      const floorId = [...this.plans.values()].find((p) => p.id === plan)?.locationId;
      if (floorId && floorId !== this.mapFloorId) this.setFloor(floorId);
    }
    this.loadTrend(changed);
  }

  clearFilters(): void {
    this.typeFilter = this.techFilter = this.statusFilter = this.query = '';
    this.onFilters();
  }

  // ================================================================= KPIs

  get kpis(): { key: string; label: string; value: string; note: string; sub: string }[] {
    const k = this.data?.kpis;
    if (!k) return ['tracked', 'online', 'alerts', 'points'].map((key) => ({ key, label: '', value: '—', note: '', sub: '' })).map((x, i) => ({ ...x, label: ['Tracked Assets', 'Devices Online', 'Active Alerts', 'Telemetry Points'][i] }));
    const change = k.telemetry.changePercent;
    const rangeLabel = this.data!.range.label.toLowerCase();
    return [
      { key: 'tracked', label: 'Tracked Assets', value: k.trackedAssets.toLocaleString(), note: 'unique assets with a linked tag', sub: '' },
      {
        key: 'online',
        label: 'Devices Online',
        value: `${k.devices.online.toLocaleString()} / ${k.devices.registered.toLocaleString()}`,
        note: k.devices.registered ? `${Math.round((k.devices.online / k.devices.registered) * 100)}% of devices` : 'no devices',
        sub: `Gateways ${k.devices.gatewaysOnline}/${k.devices.gateways} · Tags ${k.devices.tagsOnline}/${k.devices.tags}`,
      },
      {
        key: 'alerts',
        label: 'Active Alerts',
        value: k.alerts.active.toLocaleString(),
        note: 'unacknowledged',
        sub: `${k.alerts.critical} critical · ${k.alerts.warning} warning · ${k.alerts.info} info`,
      },
      {
        key: 'points',
        label: 'Telemetry Points',
        value: this.compact(k.telemetry.points),
        note: change === null ? rangeLabel : `${change >= 0 ? '↑' : '↓'} ${Math.abs(change)}% vs previous ${rangeLabel.replace('last ', '')}`,
        sub: `${this.compact(k.telemetry.messages)} messages · ${rangeLabel}`,
      },
    ];
  }

  // ================================================================= floor plan map

  private loadFloors(): void {
    forkJoin({
      locations: this.floorApi.locations().pipe(catchError(() => of({ floors: [], zones: [] }))),
      plans: this.floorApi.floorPlans().pipe(catchError(() => of(new Map<string, FloorPlanRef>()))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ locations, plans }) => {
        this.plans = plans;
        this.floors = locations.floors.filter((f) => plans.has(f.id));
        const fromAsset = this.selected?.location?.floorPlanId ? [...plans.values()].find((p) => p.id === this.selected!.location!.floorPlanId)?.locationId : undefined;
        this.setFloor(fromAsset ?? this.floors[0]?.id ?? '');
      });
  }

  setFloor(id: string): void {
    this.mapFloorId = id;
    this.planZones = [];
    this.planPlots = [];
    this.planSub?.unsubscribe();
    const plan = this.plans.get(id);
    if (!plan) return;
    this.planSub = forkJoin({ zones: this.floorApi.zones(plan.id), plots: this.floorApi.plots(plan.id) }).subscribe({
      next: ({ zones, plots }) => {
        if (id !== this.mapFloorId) return;
        this.planZones = zones;
        this.planPlots = plots;
      },
      error: () => undefined,
    });
  }

  get mapImage(): string | null {
    return this.plans.get(this.mapFloorId)?.fileUrl ?? this.floors.find((f) => f.id === this.mapFloorId)?.image ?? null;
  }

  get mapFallback(): string | null {
    return this.floors.find((f) => f.id === this.mapFloorId)?.image ?? null;
  }

  /** Gateway hearing the selected asset, emphasised on the map. */
  get highlight(): string[] {
    const g = this.selected?.location?.gatewayId;
    return g ? [g] : [];
  }

  get mapNote(): string {
    if (!this.floors.length) return 'No floor plans yet — upload one in Maps & Floor Plans.';
    const s = this.selected;
    if (s?.location && !s.location.floorPlanId) return `${s.name} is heard by gateway ${s.location.gatewayId}, which is not plotted on a floor plan.`;
    return '';
  }

  // ================================================================= trend

  loadTrend(reset: boolean): void {
    const id = this.selectedId;
    if (!id) return;
    if (reset) {
      this.trend = null;
      this.hoverIdx = null;
    }
    this.trendLoading = !this.trend;
    this.trendSub?.unsubscribe();
    this.trendSub = this.api.trend(id, this.range).subscribe({
      next: (t) => {
        if (id !== this.selectedId) return;
        this.trend = t;
        this.trendLoading = false;
      },
      error: () => {
        this.trendLoading = false;
        if (reset) this.trend = { assetId: id, range: this.data?.range ?? { key: this.range, label: '', from: '', to: '' }, metrics: [] };
      },
    });
  }

  get metrics(): MetricSeries[] {
    return this.trend?.metrics ?? [];
  }

  color(key: string): string {
    return METRIC_COLORS[key] ?? '#6366f1';
  }

  private value(p: { avg: number; min: number; max: number }): number {
    return this.agg === 'min' ? p.min : this.agg === 'max' ? p.max : p.avg;
  }

  /** One band per metric (each on its own axis: units differ). */
  get chart(): { bands: Band[]; height: number; xTicks: { x: number; label: string }[]; times: number[] } | null {
    const t = this.trend;
    if (!t) return null;
    const shown = t.metrics.filter((m) => this.show[m.key] !== false && m.points.length);
    if (!shown.length) return null;
    const from = Date.parse(t.range.from);
    const to = Date.parse(t.range.to);
    const w = Math.max(320, this.chartW);
    const x = (ms: number) => CHART.l + ((ms - from) / Math.max(1, to - from)) * (w - CHART.l - CHART.r);
    const bands: Band[] = shown.map((m, i) => {
      const top = 12 + i * (CHART.band + CHART.gap);
      const vals = m.points.map((p) => this.value(p));
      const extra = m.threshold !== null ? [m.threshold] : [];
      let lo = Math.min(...vals, ...extra);
      let hi = Math.max(...vals, ...extra);
      if (hi === lo) { hi += 1; lo -= 1; }
      const pad = (hi - lo) * 0.12;
      lo -= pad; hi += pad;
      const y = (v: number) => top + CHART.band - ((v - lo) / (hi - lo)) * CHART.band;
      const path = m.points.map((p, j) => `${j ? 'L' : 'M'}${x(Date.parse(p.at)).toFixed(1)},${y(this.value(p)).toFixed(1)}`).join('');
      const ticks = [0, 0.5, 1].map((f) => { const v = lo + (hi - lo) * f; return { y: y(v), label: `${Math.round(v)}` }; });
      return {
        key: m.key, label: `${m.label} (${m.unit})`, unit: m.unit, color: this.color(m.key), top, h: CHART.band, path, ticks,
        threshold: m.threshold !== null ? { y: y(m.threshold), label: m.thresholdLabel ?? `Threshold ${m.threshold} ${m.unit}` } : null,
      };
    });
    const height = 12 + shown.length * (CHART.band + CHART.gap) + CHART.axis - CHART.gap;
    const xTicks = [0, 0.25, 0.5, 0.75, 1].map((f) => { const ms = from + (to - from) * f; return { x: x(ms), label: this.range === '7d' ? this.day(ms) : this.time(new Date(ms)) }; });
    const times = [...new Set(shown.flatMap((m) => m.points.map((p) => Date.parse(p.at))))].sort((a, b) => a - b);
    return { bands, height, xTicks, times };
  }

  onChartMove(ev: MouseEvent): void {
    const c = this.chart;
    const el = this.chartBox?.nativeElement;
    if (!c || !el || !this.trend) return;
    const from = Date.parse(this.trend.range.from);
    const to = Date.parse(this.trend.range.to);
    const px = ev.clientX - el.getBoundingClientRect().left;
    const ms = from + ((px - CHART.l) / Math.max(1, this.chartW - CHART.l - CHART.r)) * (to - from);
    let best = 0;
    c.times.forEach((t, i) => { if (Math.abs(t - ms) < Math.abs(c.times[best] - ms)) best = i; });
    this.hoverIdx = c.times.length ? best : null;
  }

  get hover(): { x: number; time: string; rows: { label: string; value: string; color: string }[] } | null {
    const c = this.chart;
    if (this.hoverIdx === null || !c || !this.trend) return null;
    const at = c.times[this.hoverIdx];
    const from = Date.parse(this.trend.range.from);
    const to = Date.parse(this.trend.range.to);
    const x = CHART.l + ((at - from) / Math.max(1, to - from)) * (this.chartW - CHART.l - CHART.r);
    const rows = this.metrics.filter((m) => this.show[m.key] !== false).map((m) => {
      const p = m.points.find((q) => Date.parse(q.at) === at);
      return { label: m.label, value: p ? `${this.value(p)} ${m.unit} (min ${p.min} · max ${p.max} · ${p.count} readings)` : '—', color: this.color(m.key) };
    });
    return { x, time: this.range === '7d' ? `${this.day(at)} ${this.time(new Date(at))}` : this.time(new Date(at)), rows };
  }

  spark(m: MetricSeries): string {
    const pts = m.points.slice(-30);
    if (pts.length < 2) return '';
    const vals = pts.map((p) => p.avg);
    const lo = Math.min(...vals);
    const hi = Math.max(...vals);
    return pts.map((p, i) => `${i ? 'L' : 'M'}${((i / (pts.length - 1)) * 120).toFixed(1)},${(28 - ((p.avg - lo) / (hi - lo || 1)) * 26).toFixed(1)}`).join('');
  }

  // ================================================================= events

  /** The current page of the events table. */
  get events(): TelemetryEvent[] {
    return this.evData?.items ?? [];
  }

  /** GET /api/tracking-telemetry/events for the current page, limited to the filtered assets when a filter is set. */
  loadEvents(): void {
    const filtering = !!(this.typeFilter || this.techFilter || this.statusFilter || this.query.trim());
    const ids = filtering ? this.filtered.map((a) => a.assetId) : [];
    // filters that match no asset: ask for an id no event has, so the table is empty
    const assetIds = filtering && !ids.length ? ['-'] : ids;
    this.evLoading = !this.evData;
    this.evSub?.unsubscribe();
    this.evSub = this.api.events(this.range, this.site, this.evPage, this.evPageSize, assetIds, this.evCategory).subscribe({
      next: (p) => {
        this.evData = p;
        this.evPage = p.page;
        this.evLoading = false;
      },
      error: () => (this.evLoading = false),
    });
  }

  goToPage(p: number): void {
    const last = this.evData?.totalPages ?? 1;
    const next = Math.min(Math.max(1, p), last);
    if (next === this.evPage && this.evData) return;
    this.evPage = next;
    this.loadEvents();
  }

  setPageSize(size: number): void {
    this.evPageSize = Number(size);
    this.evPage = 1;
    this.loadEvents();
  }

  setCategory(c: string): void {
    this.evCategory = c;
    this.evPage = 1;
    this.loadEvents();
  }

  /** Page numbers around the current one (1 … 4 5 6 … 12). */
  get pageNumbers(): (number | '…')[] {
    const total = this.evData?.totalPages ?? 1;
    const cur = this.evPage;
    const out: (number | '…')[] = [];
    for (let i = 1; i <= total; i++) {
      if (i === 1 || i === total || Math.abs(i - cur) <= 1) out.push(i);
      else if (out[out.length - 1] !== '…') out.push('…');
    }
    return out;
  }

  get evRangeText(): string {
    const p = this.evData;
    if (!p || !p.total) return '0 events';
    const from = (p.page - 1) * p.pageSize + 1;
    return `${from}–${Math.min(p.total, p.page * p.pageSize)} of ${p.total.toLocaleString()} events`;
  }

  eventsOf(a: TrackedAsset): TelemetryEvent[] {
    return (this.data?.events ?? []).filter((e) => e.assetId === a.assetId);
  }

  alertsOf(a: TrackedAsset): TelemetryEvent[] {
    return this.eventsOf(a).filter((e) => e.status !== 'Recorded');
  }

  locationChangesOf(a: TrackedAsset): TelemetryEvent[] {
    return this.eventsOf(a).filter((e) => e.category === 'Location').slice(0, 8);
  }

  viewAsset(id: string | null, tab: Tab = 'live'): void {
    if (!id) return;
    this.select(id);
    this.tab = tab;
    document.querySelector('.sel-card')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  // ================================================================= export

  /** Exports the Assets table of the page (current site / time range / filters). */
  exportRequest = (): ExportRequest => ({
    title: 'Tracking & Telemetry', fileBase: 'tracking-telemetry',
    columns: [
      { label: 'Asset ID', key: 'assetId' }, { label: 'Asset Tag', value: (a: any) => a.assetTag ?? '' }, { label: 'Asset', key: 'name' }, { label: 'Type', value: (a: any) => a.assetType ?? '' },
      { label: 'Operational Status', value: (a: any) => a.operationalStatus ?? '' }, { label: 'Connectivity', key: 'connectivity' },
      { label: 'Zone', value: (a: any) => a.location?.zoneName ?? '' }, { label: 'Floor', value: (a: any) => a.location?.floorName ?? '' }, { label: 'Gateway', value: (a: any) => a.location?.gatewayId ?? '' },
      { label: 'Location Method', value: (a: any) => (a.location ? `${a.location.method} (${a.location.accuracy})` : '') },
      { label: 'Last Location Update', value: (a: any) => this.stamp(a.lastLocationAt) }, { label: 'Last Telemetry Update', value: (a: any) => this.stamp(a.lastTelemetryAt) },
      { label: 'Battery %', value: (a: any) => a.battery ?? '' }, { label: 'RSSI dBm', value: (a: any) => a.rssi ?? '' }, { label: 'Active Alerts', key: 'activeAlerts' },
      { label: 'Asset Health', value: (a: any) => a.assetHealth ?? '' }, { label: 'Device Health', key: 'deviceHealth' }, { label: 'Tags', value: (a: any) => a.tags.map((t: any) => t.tagId).join(', ') },
    ],
    filters: { Site: this.sites?.find((s: any) => s.id === this.site)?.name || this.site, Range: this.range },
    rows: () => this.filtered,
  });

  // ================================================================= formatting

  compact(n: number): string {
    return n >= 1e6 ? `${(n / 1e6).toFixed(1)}M` : n >= 1e4 ? `${(n / 1e3).toFixed(1)}K` : n.toLocaleString();
  }

  time(d: Date): string {
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  day(ms: number): string {
    return new Date(ms).toLocaleDateString([], { day: '2-digit', month: 'short' });
  }

  stamp(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return `${d.toLocaleDateString([], { day: '2-digit', month: 'short' })} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}`;
  }

  agoText(v: string | Date | null | undefined): string {
    if (!v) return 'never';
    const s = Math.max(0, Math.round((this.now.getTime() - new Date(v).getTime()) / 1000));
    return s < 60 ? `${s}s ago` : s < 3600 ? `${Math.floor(s / 60)} min ago` : s < 86400 ? `${Math.floor(s / 3600)} h ago` : `${Math.floor(s / 86400)} d ago`;
  }

  connClass(c: string | null | undefined): string {
    return c === 'Online' ? 'st--online' : c === 'Idle' ? 'st--warning' : 'st--offline';
  }

  sevClass(s: string): string {
    return s === 'Critical' ? 'sev--critical' : s === 'Warning' ? 'sev--warning' : 'sev--info';
  }

  metricStatusClass(s: string): string {
    return s === 'Normal' ? 'ms--ok' : s === 'Warning' ? 'ms--warn' : 'ms--stale';
  }

  healthClass(h: string | null | undefined): string {
    return /critical|poor|bad/i.test(h ?? '') ? 'hl--critical' : /attention|fair|warn/i.test(h ?? '') ? 'hl--attention' : 'hl--healthy';
  }

  showToast(msg: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(msg);
  }
}
