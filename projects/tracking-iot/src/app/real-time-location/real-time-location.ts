import { Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subscription, catchError, forkJoin, of } from 'rxjs';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule, Bell, Box, Building2, Clock, Crosshair, Download, Eye, History, MapPin, Pause, Play, Route, Search, Settings, ShieldAlert,
  TriangleAlert, Wifi, X, HelpCircle,
} from 'lucide-angular';
import { apiErrorMessage } from '../shared/tracking-api';
import { FloorPlanMap } from '../geofence-alerts/floor-plan-map';
import { FloorMapApiService, FloorOption, FloorPlanRef, GatewayFeed, PlanPlot, PlanZone } from '../geofence-alerts/floor-map-api.service';
import { RtlApi, RtlAsset, RtlEventPage, RtlOverview, RtlTrail, TrailRange, TrailSegment } from './rtl-api';

type MoveFilter = '' | 'Moving' | 'Stationary' | 'Unknown' | 'alerts';
type LiveState = 'connecting' | 'live' | 'error';

/** Locations refresh (readings arrive every few seconds). */
const POLL_MS = 10000;

/**
 * Real-Time Location: where tracked assets are now (on the floor plan), whether they are moving, how fresh and how
 * reliable the location is, their recent zone trail and location events. Same location engine as Tracking &
 * Telemetry (GET /api/real-time-location). Geofence rules live in Geofence & Alerts; floor plans in
 * Administration › Maps & Floor Plans; custody changes in Movement & Custody (a location change is not a transfer).
 */
@Component({
  selector: 'app-real-time-location',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FloorPlanMap],
  templateUrl: './real-time-location.html',
  styleUrls: [
    '../tracking-telemetry/tracking-telemetry.css', '../tracking-telemetry/tracking-telemetry-panels.css', '../tracking-telemetry/tracking-telemetry-v2.css',
    './rtl-v2.css', '../shared/iot-purple-theme.css',
  ],
})
export class RealTimeLocation implements OnInit, OnDestroy {
  readonly icons = { Bell, Box, Building2, Clock, Crosshair, Download, Eye, History, MapPin, Pause, Play, Route, Search, Settings, ShieldAlert, TriangleAlert, Wifi, X, HelpCircle };
  readonly trailRanges: { key: TrailRange; label: string }[] = [{ key: '30m', label: 'Last 30 minutes' }, { key: '1h', label: 'Last 1 hour' }, { key: '24h', label: 'Last 24 hours' }, { key: '7d', label: 'Last 7 days' }];

  private readonly api = inject(RtlApi);
  private readonly floorApi = inject(FloorMapApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  // ---- scope / filters
  site = '';
  sites: { id: string; name: string }[] = [];
  query = '';
  typeFilter = '';
  moveFilter: MoveFilter = '';
  zoneFilter = '';

  // ---- data
  data: RtlOverview | null = null;
  loading = true;
  error = '';
  liveState: LiveState = 'connecting';
  lastUpdated: Date | null = null;
  now = new Date();

  // ---- selection / trail
  selectedId = '';
  trail: RtlTrail | null = null;
  trailRange: TrailRange = '30m';
  trailLoading = false;
  /** Trail segment picked on the timeline: its gateway is highlighted on the map instead of the current one. */
  pickedSegment: TrailSegment | null = null;

  // ---- floor plan map
  floors: FloorOption[] = [];
  private plans = new Map<string, FloorPlanRef>();
  mapFloorId = '';
  planZones: PlanZone[] = [];
  planPlots: PlanPlot[] = [];
  feed: Record<string, GatewayFeed> = {};
  layers = { gateways: true, assets: true, labels: true };

  // ---- locate dialog
  locateOpen = false;
  locateQuery = '';

  // ---- events (paged by the backend)
  evRange = '24h';
  evPage = 1;
  evPageSize = 10;
  readonly evPageSizes = [10, 25, 50];
  evType = '';
  evOnlySelected = false;
  ev: RtlEventPage | null = null;
  evLoading = false;

  toast = '';
  private timers: ReturnType<typeof setInterval>[] = [];
  private loadSub?: Subscription;
  private trailSub?: Subscription;
  private planSub?: Subscription;
  private evSub?: Subscription;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.loadFloors();
    this.refresh();
    this.timers.push(setInterval(() => this.refresh(), POLL_MS));
    this.timers.push(setInterval(() => (this.now = new Date()), 1000));
    this.floorApi.gatewaysLive().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((m) => {
      if (!m.data) return;
      const next: Record<string, GatewayFeed> = {};
      for (const g of m.data) next[g.gatewayId.toLowerCase()] = g;
      this.feed = next;
    });
  }

  ngOnDestroy(): void {
    this.timers.forEach((t) => clearInterval(t));
    clearTimeout(this.toastTimer);
    [this.loadSub, this.trailSub, this.planSub, this.evSub].forEach((s) => s?.unsubscribe());
  }

  // ================================================================= loading

  /** Overview of the floor shown on the map ("assets in view"), then the events page and the selected asset's trail. */
  refresh(): void {
    const floorPlanId = this.plans.get(this.mapFloorId)?.id ?? '';
    this.loadSub?.unsubscribe();
    this.loadSub = this.api.overview(this.site, floorPlanId).subscribe({
      next: (d) => {
        this.data = d;
        this.loading = false;
        this.error = '';
        this.liveState = 'live';
        this.lastUpdated = new Date();
        this.collectSites(d);
        if (this.selectedId && !d.assets.some((a) => a.assetId === this.selectedId)) this.selectedId = '';
        this.loadEvents();
        if (this.selectedId) this.loadTrail(false);
      },
      error: (err) => {
        this.loading = false;
        const msg = apiErrorMessage(err, 'Could not load asset locations.');
        if (msg !== this.error) this.showToast(msg);
        this.error = msg;
        this.liveState = 'error';
      },
    });
  }

  private collectSites(d: RtlOverview): void {
    const seen = new Map(this.sites.map((s) => [s.id, s.name]));
    for (const a of d.assets) if (a.location?.siteId && a.location.siteName) seen.set(a.location.siteId, a.location.siteName);
    this.sites = [...seen].map(([id, name]) => ({ id, name })).sort((x, y) => x.name.localeCompare(y.name));
  }

  get liveLabel(): { cls: string; text: string; detail: string } {
    if (this.liveState === 'connecting') return { cls: 'is-wait', text: 'CONNECTING', detail: '' };
    if (this.liveState === 'error') return { cls: 'is-off', text: 'OFFLINE', detail: this.lastUpdated ? `last update ${this.lastUpdated.toLocaleTimeString()}` : 'no data yet' };
    const ago = this.lastUpdated ? Math.round((this.now.getTime() - this.lastUpdated.getTime()) / 1000) : 0;
    return ago > 60 ? { cls: 'is-warn', text: 'STALE', detail: `updated ${ago}s ago` } : { cls: '', text: 'LIVE', detail: `updated ${ago}s ago` };
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
        if (!this.mapFloorId && this.floors.length) this.setFloor(this.floors[0].id);
      });
  }

  /** Shows a floor; "assets in view" and the KPIs follow it ('' = all floors). */
  setFloor(id: string, reload = true): void {
    this.mapFloorId = id;
    this.planZones = [];
    this.planPlots = [];
    this.pickedSegment = null;
    this.planSub?.unsubscribe();
    const plan = this.plans.get(id);
    if (plan) {
      this.planSub = forkJoin({ zones: this.floorApi.zones(plan.id), plots: this.floorApi.plots(plan.id) }).subscribe({
        next: ({ zones, plots }) => {
          if (id !== this.mapFloorId) return;
          this.planZones = zones;
          this.planPlots = plots;
        },
        error: () => undefined,
      });
    }
    if (reload) this.refresh();
  }

  private floorOfPlan(planId: string | null | undefined): string {
    return planId ? [...this.plans.values()].find((p) => p.id === planId)?.locationId ?? '' : '';
  }

  get mapImage(): string | null {
    return this.plans.get(this.mapFloorId)?.fileUrl ?? this.floors.find((f) => f.id === this.mapFloorId)?.image ?? null;
  }

  get mapFallback(): string | null {
    return this.floors.find((f) => f.id === this.mapFloorId)?.image ?? null;
  }

  /** The gateway hearing the selected asset (or the picked trail segment's gateway). */
  get highlight(): string[] {
    const g = this.pickedSegment?.gatewayId ?? this.selected?.location?.gatewayId;
    return g ? [g] : [];
  }

  /** Zone of the selected asset (or the picked trail segment) shown alone on the map. */
  get mapZone(): string | undefined {
    if (this.pickedSegment) return this.planZones.find((z) => z.name === this.pickedSegment!.zoneName)?.id ?? undefined;
    return undefined;
  }

  get mapNote(): string {
    if (!this.floors.length) return 'No floor plans yet — upload one in Administration › Maps & Floor Plans.';
    const s = this.selected;
    if (s?.location && !s.location.floorPlanId) return `${s.name} is heard by gateway ${s.location.gatewayId}, which is not plotted on a floor plan.`;
    if (s?.location?.floorPlanId && this.floorOfPlan(s.location.floorPlanId) !== this.mapFloorId) return `${s.name} is on ${s.location.floorName ?? 'another floor'}.`;
    return '';
  }

  // ================================================================= list / filters / KPIs

  get assets(): RtlAsset[] {
    return this.data?.assets ?? [];
  }

  get assetTypes(): string[] {
    return [...new Set(this.assets.map((a) => a.assetType).filter((x): x is string => !!x))].sort();
  }

  get zones(): string[] {
    return [...new Set(this.assets.map((a) => a.location?.zoneName).filter((x): x is string => !!x))].sort();
  }

  get filtered(): RtlAsset[] {
    const q = this.query.trim().toLowerCase();
    return this.assets.filter(
      (a) =>
        (!this.typeFilter || a.assetType === this.typeFilter) &&
        (!this.zoneFilter || a.location?.zoneName === this.zoneFilter) &&
        (!this.moveFilter || (this.moveFilter === 'alerts' ? a.activeLocationAlerts > 0 : a.movement === this.moveFilter)) &&
        (!q || this.searchText(a).includes(q))
    );
  }

  private searchText(a: RtlAsset): string {
    return `${a.assetId} ${a.assetTag ?? ''} ${a.name} ${a.assetType ?? ''} ${a.tags.join(' ')} ${a.location?.gatewayId ?? ''} ${a.location?.zoneName ?? ''}`.toLowerCase();
  }

  setMoveFilter(f: MoveFilter): void {
    this.moveFilter = this.moveFilter === f ? '' : f;
  }

  clearFilters(): void {
    this.query = this.typeFilter = this.zoneFilter = '';
    this.moveFilter = '';
  }

  get kpis(): { key: MoveFilter | 'all'; label: string; value: number | string; note: string; icon: typeof Box; cls: string }[] {
    const k = this.data?.kpis;
    const v = (n: number | undefined) => (k ? n ?? 0 : '—');
    return [
      { key: 'all', label: 'Assets in View', value: v(k?.assetsInView), note: this.mapFloorId ? this.floors.find((f) => f.id === this.mapFloorId)?.label.split(' > ').pop() ?? '' : 'all floors', icon: Box, cls: 'violet' },
      { key: 'Moving', label: 'Moving', value: v(k?.moving), note: 'zone changed in 5 min', icon: Play, cls: 'green' },
      { key: 'Stationary', label: 'Stationary', value: v(k?.stationary), note: 'same zone 5 min+', icon: Pause, cls: 'blue' },
      { key: 'Unknown', label: 'Unknown / Stale', value: v(k?.unknown), note: 'no live location', icon: HelpCircle, cls: 'grey' },
      { key: 'alerts', label: 'Location Alerts', value: v(k?.locationAlerts), note: 'open', icon: ShieldAlert, cls: 'red' },
    ];
  }

  // ================================================================= selection

  get selected(): RtlAsset | null {
    return this.assets.find((a) => a.assetId === this.selectedId) ?? null;
  }

  select(id: string): void {
    const changed = id !== this.selectedId;
    this.selectedId = id;
    this.pickedSegment = null;
    const floor = this.floorOfPlan(this.selected?.location?.floorPlanId);
    if (floor && floor !== this.mapFloorId) this.setFloor(floor);
    if (changed) {
      this.trail = null;
      this.loadTrail(true);
      if (this.evOnlySelected) this.setEvPage(1);
    }
  }

  closeSelected(): void {
    this.selectedId = '';
    this.trail = null;
    this.pickedSegment = null;
    if (this.evOnlySelected) this.setEvPage(1);
  }

  // ---- locate asset
  openLocate(): void {
    this.locateOpen = true;
    this.locateQuery = '';
    setTimeout(() => (document.getElementById('rtl-locate-q') as HTMLInputElement | null)?.focus(), 50);
  }

  get locateRows(): RtlAsset[] {
    const q = this.locateQuery.trim().toLowerCase();
    return (q ? this.assets.filter((a) => this.searchText(a).includes(q)) : this.assets).slice(0, 50);
  }

  /** Locate: go to the asset's floor (all floors first, so it is in view), highlight it, open its details. */
  locate(a: RtlAsset): void {
    this.locateOpen = false;
    this.clearFilters();
    const floor = this.floorOfPlan(a.location?.floorPlanId);
    if (floor && floor !== this.mapFloorId) this.setFloor(floor);
    this.select(a.assetId);
    if (!a.location) this.showToast(`${a.name} has no location yet: none of its tags has been heard.`);
  }

  // ---- trail
  loadTrail(reset: boolean): void {
    const id = this.selectedId;
    if (!id) return;
    if (reset) this.trailLoading = true;
    this.trailSub?.unsubscribe();
    this.trailSub = this.api.trail(id, this.trailRange).subscribe({
      next: (t) => {
        if (id !== this.selectedId) return;
        this.trail = t;
        this.trailLoading = false;
      },
      error: () => (this.trailLoading = false),
    });
  }

  setTrailRange(r: TrailRange): void {
    this.trailRange = r;
    this.pickedSegment = null;
    this.loadTrail(true);
  }

  pickSegment(s: TrailSegment): void {
    this.pickedSegment = this.pickedSegment === s ? null : s;
  }

  /** Newest first for the timeline. */
  get trailSegments(): TrailSegment[] {
    return [...(this.trail?.segments ?? [])].reverse();
  }

  // ---- actions
  viewAsset(a: RtlAsset): void {
    void this.router.navigate(['/assets/overview'], { queryParams: { id: a.assetId, tag: a.assetTag ?? undefined } });
  }

  trackHistory(): void {
    this.setTrailRange('24h');
    document.querySelector('.trail-card')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** Opens Geofence & Alerts with a new rule for this asset's current zone. */
  createAlert(a: RtlAsset): void {
    const zone = a.location?.zoneId ? `zone:${a.location.zoneId}` : '';
    void this.router.navigate(['/tracking-iot/geofence-alerts'], { queryParams: { newRule: 1, zone: zone || undefined, asset: a.name } });
  }

  // ================================================================= events

  loadEvents(): void {
    const assetId = this.evOnlySelected ? this.selectedId : '';
    this.evLoading = !this.ev;
    this.evSub?.unsubscribe();
    this.evSub = this.api.events(this.evRange, this.site, this.evPage, this.evPageSize, assetId, this.evType).subscribe({
      next: (p) => {
        this.ev = p;
        this.evPage = p.page;
        this.evLoading = false;
      },
      error: () => (this.evLoading = false),
    });
  }

  setEvPage(p: number): void {
    const last = this.ev?.totalPages ?? 1;
    this.evPage = Math.min(Math.max(1, p), Math.max(1, last));
    this.loadEvents();
  }

  setEvFilter(): void {
    this.evPage = 1;
    this.loadEvents();
  }

  get pageNumbers(): (number | '…')[] {
    const total = this.ev?.totalPages ?? 1;
    const out: (number | '…')[] = [];
    for (let i = 1; i <= total; i++) {
      if (i === 1 || i === total || Math.abs(i - this.evPage) <= 1) out.push(i);
      else if (out[out.length - 1] !== '…') out.push('…');
    }
    return out;
  }

  get evRangeText(): string {
    const p = this.ev;
    if (!p || !p.total) return '0 events';
    return `${(p.page - 1) * p.pageSize + 1}–${Math.min(p.total, p.page * p.pageSize)} of ${p.total.toLocaleString()} events`;
  }

  exportData(): void {
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.filtered.map((a) => ({
      'Asset ID': a.assetId, Asset: a.name, Type: a.assetType ?? '', Zone: a.location?.zoneName ?? '', Floor: a.location?.floorName ?? '', Gateway: a.location?.gatewayId ?? '',
      Movement: a.movement, Freshness: a.freshness, 'Last valid location': this.stamp(a.lastValidLocationAt), Source: a.locationSource, Accuracy: a.accuracy,
      Confidence: a.confidence, Tracker: a.tags.join(', '), 'Registered location': a.expected?.zoneName ?? a.expected?.floorName ?? '', Comparison: a.comparisonStatus,
    }))), 'Assets');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet((this.ev?.items ?? []).map((e) => ({
      Time: this.stamp(e.at), 'Event ID': e.eventId, 'Asset ID': e.assetId ?? '', Asset: e.assetName ?? '', Event: e.eventType, Zone: e.zone ?? '', Details: e.details ?? '',
      Severity: e.severity, Source: e.source ?? '', Status: e.status,
    }))), 'Events');
    if (this.trail) {
      XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.trail.segments.map((s) => ({
        Asset: this.trail!.name, Zone: s.zoneName ?? '', Floor: s.floorName ?? '', Gateway: s.gatewayId, From: this.stamp(s.from), To: this.stamp(s.to), Minutes: s.minutes,
      }))), 'Trail');
    }
    XLSX.writeFile(book, `real-time-location-${new Date().toISOString().slice(0, 10)}.xlsx`);
  }

  // ================================================================= formatting

  stamp(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    return `${d.toLocaleDateString([], { day: '2-digit', month: 'short' })} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}`;
  }

  hm(iso: string): string {
    return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  agoText(v: string | null | undefined): string {
    if (!v) return 'never';
    const s = Math.max(0, Math.round((this.now.getTime() - new Date(v).getTime()) / 1000));
    return s < 60 ? `${s}s ago` : s < 3600 ? `${Math.floor(s / 60)} min ago` : s < 86400 ? `${Math.floor(s / 3600)} h ago` : `${Math.floor(s / 86400)} d ago`;
  }

  duration(min: number): string {
    return min < 1 ? '<1 min' : min < 60 ? `${Math.round(min)} min` : `${Math.floor(min / 60)} h ${Math.round(min % 60)} min`;
  }

  moveClass(m: string): string {
    return m === 'Moving' ? 'mv--moving' : m === 'Stationary' ? 'mv--still' : 'mv--unknown';
  }

  freshClass(f: string): string {
    return f === 'Live' ? 'st--online' : f === 'Stale' ? 'st--warning' : 'st--offline';
  }

  confClass(c: string): string {
    return c === 'High' ? 'cf--high' : c === 'Medium' ? 'cf--med' : c === 'Low' ? 'cf--low' : 'cf--unk';
  }

  sevClass(s: string): string {
    return s === 'Critical' ? 'sev--critical' : s === 'Warning' ? 'sev--warning' : 'sev--info';
  }

  showToast(msg: string): void {
    this.toast = msg;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}
