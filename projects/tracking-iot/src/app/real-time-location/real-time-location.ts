import { Component, DestroyRef, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Observable, Subscription, catchError, forkJoin, map, of } from 'rxjs';
import { ExportMenu, ExportRequest, pushToast } from 'shared-ui';
import {
  LucideAngularModule, ArrowLeft, Bell, Box, Building2, Clock, Crosshair, Download, Eye, History, Lightbulb, LocateFixed, MapPin, Pause, Play, Power,
  Radio, Route, Search, Send, Settings, ShieldAlert, Square, TriangleAlert, Volume2, Wifi, X, HelpCircle,
} from 'lucide-angular';
import { apiErrorMessage } from '../shared/tracking-api';
import { AssetFocus, FloorPlanMap, LivePoint, MapFocus, PlanAsset } from '../geofence-alerts/floor-plan-map';
import { FloorMapApiService, FloorOption, FloorPlanRef, GatewayFeed, PlanPlot, PlanZone } from '../geofence-alerts/floor-map-api.service';
import { LiveMessage, LiveSnapshot, LiveTag, LiveTagsMessage, LocateMessage, LocateReply, LocateSocket, RtlApi, RtlAsset, RtlEventPage, RtlOverview, RtlTrail, TrailRange, TrailSegment } from './rtl-api';

type MoveFilter = '' | 'Moving' | 'Stationary' | 'Unknown' | 'alerts';
type LiveState = 'connecting' | 'live' | 'error';

/** Locations refresh (readings arrive every few seconds). */
const POLL_MS = 10000;
/** While the live socket is open the overview is only re-read now and then (KPIs, events, freshness). */
const SOCKET_RECONCILE_MS = 60000;
/** An asset that changed zone is "Moving" for this long, then "Stationary" again. */
const MOVING_MS = 60000;

/**
 * Real-Time Location: where tracked assets are now (on the floor plan), whether they are moving, how fresh and how
 * reliable the location is, their recent zone trail and location events. Same location engine as Tracking &
 * Telemetry (GET /api/real-time-location). Geofence rules live in Geofence & Alerts; floor plans in
 * Administration › Maps & Floor Plans; custody changes in Movement & Custody (a location change is not a transfer).
 */
@Component({
  selector: 'app-real-time-location',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FloorPlanMap, ExportMenu],
  templateUrl: './real-time-location.html',
  styleUrls: [
    '../tracking-telemetry/tracking-telemetry.css', '../tracking-telemetry/tracking-telemetry-panels.css', '../tracking-telemetry/tracking-telemetry-v2.css',
    './rtl-v2.css', '../shared/iot-purple-theme.css',
  ],
})
export class RealTimeLocation implements OnInit, OnDestroy {
  /** Map legend: a strip under the map; in full screen a collapsed "Legend" button so it does not cover the plan. */
  fullscreen = false;
  legendOpen = false;
  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.fullscreen = !!document.fullscreenElement;
    if (!this.fullscreen) this.legendOpen = false;
  }

  readonly icons = { ArrowLeft, Bell, Box, Building2, Clock, Crosshair, Download, Eye, History, Lightbulb, LocateFixed, MapPin, Pause, Play, Power, Radio, Route, Search, Send, Settings, ShieldAlert, Square, TriangleAlert, Volume2, Wifi, X, HelpCircle };
  /** LED colours the tags support (MBT02 has a red LED only: colour 3). */
  readonly ledColours = [{ value: 3, label: 'Red' }];
  readonly patterns = [
    { label: 'Fast blink', onMs: 200, offMs: 200, cycles: 25 },
    { label: 'Slow blink', onMs: 1000, offMs: 1000, cycles: 15 },
    { label: 'Steady on 5 min', onMs: 60000, offMs: 1, cycles: 5 },
  ];
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
  /** Floor plans are loaded (the first overview waits for them). */
  private floorsReady = false;
  private plans = new Map<string, FloorPlanRef>();
  mapFloorId = '';
  planZones: PlanZone[] = [];
  planPlots: PlanPlot[] = [];
  feed: Record<string, GatewayFeed> = {};
  layers = { gateways: true, assets: true, labels: true };

  // ---- locate dialog
  locateOpen = false;
  locateQuery = '';
  /** Assets ticked in the Locate asset list (Locate selected). */
  locateSel = new Set<string>();
  /** The Locate list covers all floors (the page's overview only has the floor on the map). */
  private locateAll: RtlAsset[] | null = null;
  private locateSub?: Subscription;
  /** The assets highlighted on the map (ripple, then a steady ring) until cleared. */
  assetFocus: AssetFocus | null = null;

  // ---- live socket (/api/real-time-location/tags/live) + assets drawn in their zone
  socketState: 'connecting' | 'open' | 'closed' = 'connecting';
  mapAssets: PlanAsset[] = [];
  private mapAssetsKey = '';
  private tagsSub?: Subscription;
  private lastRefreshAt = 0;
  private refreshSoon?: ReturnType<typeof setTimeout>;
  private movingTimer?: ReturnType<typeof setTimeout>;
  private lastZone = new Map<string, string>();
  private movingUntil = new Map<string, number>();

  // ---- events (paged by the backend)
  evRange = '24h';
  evPage = 1;
  evPageSize = 10;
  readonly evPageSizes = [10, 25, 50];
  evType = '';
  evOnlySelected = false;
  ev: RtlEventPage | null = null;
  evLoading = false;

  // ---- LED / buzzer (Locate asset › bulb button): commands over /ws/locate
  ctl: SignalForm | null = null;
  /** The asset whose tag was told to blink / beep: an amber marker on the map and a Turn off panel. */
  signalling: { assetId: string; name: string; tagId: string; command: string; state: 'sent' | 'on'; until: number; gatewayId: string } | null = null;
  signalPoint: LivePoint | null = null;
  signalMsg = '';
  private signalFocused: unknown = null;
  private ctlSock?: LocateSocket;
  private ctlSub?: Subscription;
  private signalTimer?: ReturnType<typeof setTimeout>;
  private ctlWaitTimer?: ReturnType<typeof setTimeout>;

  // ---- live follow (Locate asset › location button): WebSocket stream of one asset's position
  followId = '';
  followName = '';
  followState: LiveMessage['state'] | '' = '';
  followError = '';
  live: LiveSnapshot | null = null;
  livePoint: LivePoint | null = null;
  private liveFocusKey = '';
  liveTrail: { x: number; y: number }[] = [];
  mapFocus: MapFocus | null = null;
  private liveSub?: Subscription;
  /** Where each gateway is plotted, across all floor plans (lower-case gateway id): places the live tag on the map. */
  private gatewaySpots = new Map<string, { floorPlanId: string; x: number; y: number; zoneName: string | null; floorName: string | null }>();

  toast = '';
  private timers: ReturnType<typeof setInterval>[] = [];
  private loadSub?: Subscription;
  private trailSub?: Subscription;
  private planSub?: Subscription;
  private evSub?: Subscription;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    // The first overview waits for the floor plans, so the page loads straight into the floor on the map
    // instead of showing all floors and then jumping to the first floor's numbers
    this.loadFloors();
    // Polling is the fallback: with the live socket open the overview is only re-read every minute
    this.timers.push(setInterval(() => this.floorsReady && (this.socketState !== 'open' || Date.now() - this.lastRefreshAt > SOCKET_RECONCILE_MS) && this.refresh(), POLL_MS));
    this.tagsSub = this.api.tagsLive().subscribe((e) => {
      if ('state' in e) {
        this.socketState = e.state;
        // dropped: read the overview now instead of waiting for the next poll
        if (e.state === 'closed' && this.floorsReady) this.refresh();
      } else {
        this.onTags(e.message);
      }
    });
    this.timers.push(setInterval(() => (this.now = new Date()), 1000));
    this.floorApi.gatewaysLive().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((m) => {
      if (!m.data) return;
      const next: Record<string, GatewayFeed> = {};
      for (const g of m.data) next[g.gatewayId.toLowerCase()] = g;
      this.feed = next;
    });
  }

  ngOnDestroy(): void {
    this.stopLive();
    this.closeCtlSocket();
    clearTimeout(this.signalTimer);
    clearTimeout(this.ctlWaitTimer);
    clearTimeout(this.refreshSoon);
    clearTimeout(this.movingTimer);
    this.tagsSub?.unsubscribe();
    this.locateSub?.unsubscribe();
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
        this.lastRefreshAt = Date.now();
        this.collectSites(d);
        this.noteZones();
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
    // `now` ticks once a second, so it can be just behind the moment the data arrived: never show "-1s ago"
    const ago = this.lastUpdated ? Math.max(0, Math.round((this.now.getTime() - this.lastUpdated.getTime()) / 1000)) : 0;
    const via = this.socketState === 'open' ? 'real-time' : 'polling';
    return ago > 60 && this.socketState !== 'open' ? { cls: 'is-warn', text: 'STALE', detail: `updated ${ago}s ago` } : { cls: '', text: 'LIVE', detail: `${via} · updated ${ago}s ago` };
  }

  // ================================================================= live socket + moving / stationary

  /** Zone changes seen in an overview: Moving for a minute (also right after a page load, from the backend's last zone change). */
  private noteZones(): void {
    const now = Date.now();
    for (const a of this.assets) {
      const zone = a.location?.zoneId;
      if (!zone) continue;
      const before = this.lastZone.get(a.assetId);
      if (before !== undefined && before !== zone) this.movingUntil.set(a.assetId, now + MOVING_MS);
      else if (before === undefined && a.lastZoneChangeAt) {
        const until = new Date(a.lastZoneChangeAt).getTime() + MOVING_MS;
        if (until > now) this.movingUntil.set(a.assetId, until);
      }
      this.lastZone.set(a.assetId, zone);
    }
    this.rebuildMapAssets();
  }

  /** The asset a tag belongs to: by its tag id, else by the asset the backend matched it to. */
  private assetOfTag(t: LiveTag): RtlAsset | undefined {
    const id = t.tagId.toLowerCase();
    return this.assets.find(
      (a) => a.tags.some((x) => x.toLowerCase() === id) || (!!t.asset && (a.assetId === t.asset.assetRegistrationId || (!!a.assetTag && a.assetTag === t.asset.assetTag)))
    );
  }

  /** One message of the live socket: a tag now heard in another zone moves its asset (animated) and marks it Moving. */
  private onTags(m: LiveTagsMessage): void {
    const tags = m.type === 'snapshot' ? m.tags : m.changed;
    let unknownHere = false;
    let zoneChanged = false;
    for (const t of tags) {
      const a = this.assetOfTag(t);
      if (!a) {
        // an asset not in the page's list yet (e.g. it just entered this floor): read the overview soon
        if (t.plotted && t.asset && (!this.mapFloorId || this.plans.get(this.mapFloorId)?.id === t.floorPlanId)) unknownHere = true;
        continue;
      }
      if (!t.plotted || !t.zoneId) continue;
      const before = a.location?.zoneId ?? null;
      a.location = {
        gatewayId: t.gatewayId, gatewayName: t.gatewayName, zoneId: t.zoneId, zoneName: t.zoneName, floorName: t.floorName, buildingName: t.buildingName,
        siteId: t.siteId, siteName: t.siteName, floorPlanId: t.floorPlanId, locationPath: t.locationPath,
        method: a.location?.method ?? 'Estimated', basis: a.location?.basis ?? 'BLE proximity', accuracy: a.location?.accuracy ?? 'Zone level', at: t.timestamp,
      };
      a.freshness = 'Live';
      if (before && before !== t.zoneId) {
        zoneChanged = true;
        a.movement = 'Moving';
        a.lastZoneChangeAt = new Date().toISOString();
        this.movingUntil.set(a.assetId, Date.now() + MOVING_MS);
      }
      this.lastZone.set(a.assetId, t.zoneId);
    }
    for (const s of m.type === 'update' ? m.seen : []) {
      const a = this.assets.find((x) => x.tags.some((y) => y.toLowerCase() === s.tagId.toLowerCase()));
      if (a) {
        a.freshness = 'Live';
        a.rssi = s.rssi ?? a.rssi;
        if (a.location) a.location = { ...a.location, at: s.timestamp };
      }
    }
    this.rebuildMapAssets();
    // KPIs, events and the asset list follow a zone change: read them (at most every few seconds)
    if ((zoneChanged || unknownHere) && this.floorsReady && !this.refreshSoon) {
      this.refreshSoon = setTimeout(() => {
        this.refreshSoon = undefined;
        this.refresh();
      }, 2500);
    }
  }

  /** The asset markers for the map: all assets with a zone, Moving for a minute after a zone change. Same array until something changed. */
  private rebuildMapAssets(): void {
    clearTimeout(this.movingTimer);
    const now = Date.now();
    let nextExpiry = Infinity;
    const list: PlanAsset[] = [];
    for (const a of this.assets) {
      const zoneId = a.location?.zoneId;
      if (!zoneId) continue;
      const until = this.movingUntil.get(a.assetId) ?? 0;
      const moving = until > now;
      if (moving) nextExpiry = Math.min(nextExpiry, until);
      const state = a.freshness === 'Offline' || a.freshness === 'Unknown' ? 'Unknown' : moving ? 'Moving' : 'Stationary';
      list.push({ id: a.assetId, name: a.name, code: a.assetTag || a.assetId, zoneId, zoneName: a.location?.zoneName ?? '', state, tag: a.tags[0] ?? a.tracker ?? '', lastSeenAt: a.lastValidLocationAt });
    }
    const key = list.map((x) => `${x.id}|${x.zoneId}|${x.state}|${x.lastSeenAt ?? ''}`).join(',');
    if (key !== this.mapAssetsKey) {
      this.mapAssetsKey = key;
      this.mapAssets = list;
    }
    if (nextExpiry !== Infinity) this.movingTimer = setTimeout(() => this.rebuildMapAssets(), nextExpiry - now + 100);
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
        this.floorsReady = true;
        if (!this.mapFloorId && this.floors.length) this.setFloor(this.floors[0].id);
        else this.refresh();
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
    const g = (this.followId ? this.live?.gatewayId : null) ?? this.pickedSegment?.gatewayId ?? this.selected?.location?.gatewayId ?? '';
    // Same array while the gateway is the same, so the map is not told to redraw on every change detection
    if (g !== this.highlightKey) {
      this.highlightKey = g;
      this.highlightList = g ? [g] : [];
    }
    return this.highlightList;
  }

  private highlightKey = '';
  private highlightList: string[] = [];

  // Keyed lists: a refresh updates the rows in place instead of rebuilding them
  readonly byKey = (_: number, k: { key: string }) => k.key;
  readonly byAsset = (_: number, a: RtlAsset) => a.assetId;
  readonly byEvent = (_: number, e: { eventId: string }) => e.eventId;
  readonly byFloor = (_: number, f: FloorOption) => f.id;
  readonly bySegment = (_: number, s: TrailSegment) => `${s.gatewayId}|${s.from}`;

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
    this.locateSel = new Set();
    // the list covers all floors, so "Locate selected" can switch to the floor the assets are on
    this.locateSub?.unsubscribe();
    this.locateSub = this.api.overview(this.site, '').subscribe({ next: (d) => (this.locateAll = d.assets), error: () => (this.locateAll = null) });
    setTimeout(() => (document.getElementById('rtl-locate-q') as HTMLInputElement | null)?.focus(), 50);
  }

  get locateRows(): RtlAsset[] {
    const q = this.locateQuery.trim().toLowerCase();
    const all = this.locateAll ?? this.assets;
    return (q ? all.filter((a) => this.searchText(a).includes(q)) : all).slice(0, 50);
  }

  toggleLocateSel(id: string): void {
    const next = new Set(this.locateSel);
    if (!next.delete(id)) next.add(id);
    this.locateSel = next;
  }

  /** The asset has a zone on a floor plan, so it can be shown on the map. */
  hasLocation(a: RtlAsset): boolean {
    return !!a.location?.zoneId && !!this.floorOfPlan(a.location.floorPlanId);
  }

  /** Why an asset has no marker (shown as the "No location" badge). */
  noLocationReason(a: RtlAsset): string {
    if (!a.tags.length && !a.tracker) return 'no tag linked';
    const l = a.location;
    if (!l) return a.freshness === 'Offline' ? 'tag offline, not heard by any gateway' : 'tag not heard by any gateway yet';
    if (!l.floorPlanId || !l.zoneId) return `gateway ${l.gatewayId} is not plotted in a zone on a floor plan`;
    return 'its floor plan is not available';
  }

  private get locatableRows(): RtlAsset[] {
    return this.locateRows.filter((a) => this.hasLocation(a));
  }

  get allLocateSelected(): boolean {
    const r = this.locatableRows;
    return r.length > 0 && r.every((a) => this.locateSel.has(a.assetId));
  }

  toggleAllLocate(): void {
    this.locateSel = this.allLocateSelected ? new Set() : new Set(this.locatableRows.map((a) => a.assetId));
  }

  /** Locate selected: switch to the first asset's floor plan, zoom to the zones and highlight every asset found there. */
  locateSelected(): void {
    const all = this.locateAll ?? this.assets;
    const picked = [...this.locateSel].map((id) => all.find((a) => a.assetId === id)).filter((a): a is RtlAsset => !!a);
    this.locateMany(picked);
  }

  private locateMany(picked: RtlAsset[]): void {
    this.locateOpen = false;
    this.clearFilters();
    const placed = picked.filter((a) => this.hasLocation(a));
    const missingMsg = picked.filter((a) => !this.hasLocation(a)).map((a) => `${a.name} – no current location (${this.noLocationReason(a)})`).join('; ');
    if (!placed.length) {
      this.showToast(missingMsg || 'Nothing to locate.');
      return;
    }
    const floor = this.floorOfPlan(placed[0].location!.floorPlanId);
    const here = placed.filter((a) => this.floorOfPlan(a.location!.floorPlanId) === floor);
    if (floor !== this.mapFloorId) this.setFloor(floor);
    if (here.length === 1) this.select(here[0].assetId);
    this.focusAssets(here.map((a) => a.assetId));
    const other = placed.length - here.length;
    if (other || missingMsg) this.showToast([missingMsg, other ? `${other} on other floors (showing ${this.floors.find((f) => f.id === floor)?.label ?? 'this floor'}).` : ''].filter(Boolean).join(' '));
  }

  clearHighlight(): void {
    this.assetFocus = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.assetFocus && !this.locateOpen) this.assetFocus = null;
  }

  /**
   * The one way the map goes to an asset: switch to its floor plan, then zoom and centre on the asset's own marker (its spot in
   * the zone grid, the same spot as the red locate marker) with the zone filling ~2/3 of the view, ripple + steady highlight.
   * Used by the Locate icon, Locate selected, Send to tag / Turn off, live follow and a click on an asset row.
   * The gateway position is only the fallback for an asset that is not drawn in a zone.
   */
  focusAsset(assetId: string): void {
    this.focusAssets([assetId]);
  }

  focusAssets(ids: string[]): void {
    const all = [...(this.locateAll ?? []), ...this.assets];
    const picked = ids.map((id) => all.find((a) => a.assetId === id)).filter((a): a is RtlAsset => !!a);
    if (!picked.length) return;
    const withZone = picked.filter((a) => !!a.location?.zoneId && !!a.location?.floorPlanId);
    const first = (withZone[0] ?? picked[0]).location;
    const floor = this.floorOfPlan(first?.floorPlanId);
    if (floor && floor !== this.mapFloorId) this.setFloor(floor);
    if (withZone.length) {
      const onFloor = withZone.filter((a) => this.floorOfPlan(a.location!.floorPlanId) === (floor ?? ''));
      this.mapFocus = null;
      // a new key restarts the zoom + highlight even for the same assets; the map retries until the markers are drawn
      this.assetFocus = { key: `${Date.now()}`, ids: (onFloor.length ? onFloor : withZone).map((a) => a.assetId) };
      return;
    }
    // not in a zone: the gateway that hears it
    const gw = first?.gatewayId?.toLowerCase();
    const plot = gw ? this.planPlots.find((p) => p.kind === 'device' && p.itemId?.toLowerCase() === gw) : undefined;
    if (plot) this.mapFocus = { key: `${picked[0].assetId}:gw:${Date.now()}`, x: plot.x, y: plot.y };
  }

  /** Map popup buttons: Locate / LED & Buzzer for the asset marker that was clicked. */
  mapAssetLocate(id: string): void {
    const a = this.assets.find((x) => x.assetId === id);
    if (a) this.locate(a);
  }

  mapAssetSignal(id: string): void {
    const a = this.assets.find((x) => x.assetId === id);
    if (!a) return;
    // the LED / buzzer form lives in the Locate asset dialog
    this.locateOpen = true;
    this.openSignal(a);
  }

  /** Locate: go to the asset's floor (all floors first, so it is in view), highlight it, open its details. */
  locate(a: RtlAsset): void {
    this.locateMany([a]);
  }

  // ---- LED / buzzer

  /** Bulb button: the LED / buzzer form for this asset's tag, over a Locating socket. */
  openSignal(a: RtlAsset): void {
    const tag = (a.tags[0] ?? a.tracker ?? '').trim();
    if (!tag) {
      this.showToast(`${a.name} has no tag linked (Tag Management).`);
      return;
    }
    this.ctl = {
      asset: a, tagId: tag, mode: 'led', gatewayId: '', rssi: null,
      led: { onMs: 300, offMs: 300, cycles: 10, color: 3, brightness: 100 },
      buzzer: { beepMs: 300, pauseMs: 300, cycles: 10 },
      errors: {}, status: 'Finding the gateway that hears this tag…', statusKind: 'wait', busy: false,
    };
    this.ensureCtlSocket();
    this.ctlSock!.send({ type: 'subscribe', tagid: tag });
  }

  /** Back to the asset list (the socket stays open while a tag is signalling, so Turn off still works). */
  backToLocateList(): void {
    this.ctl = null;
    if (!this.signalling) this.closeCtlSocket();
  }

  closeLocate(): void {
    this.locateOpen = false;
    this.backToLocateList();
  }

  setSignalMode(m: 'led' | 'buzzer' | 'both'): void {
    if (this.ctl) this.ctl.mode = m;
  }

  applyPattern(p: { onMs: number; offMs: number; cycles: number }): void {
    const c = this.ctl;
    if (!c) return;
    c.led.onMs = p.onMs;
    c.led.offMs = p.offMs;
    c.led.cycles = p.cycles;
    if (c.mode === 'buzzer') c.mode = 'led';
    c.errors = {};
  }

  /** How long the LED / buzzer will run with these settings. */
  get signalRunText(): string {
    const c = this.ctl;
    if (!c) return '';
    const parts: string[] = [];
    if (c.mode !== 'buzzer') parts.push(`LED runs ${this.runText(this.ledMs(c))}`);
    if (c.mode !== 'led') parts.push(`buzzer runs ${this.runText(this.buzzerMs(c))}`);
    return parts.join(' · ');
  }

  private ledMs(c: SignalForm): number {
    return (Number(c.led.cycles) || 0) * ((Number(c.led.onMs) || 0) + (Number(c.led.offMs) || 0));
  }

  private buzzerMs(c: SignalForm): number {
    return (Number(c.buzzer.cycles) || 0) * ((Number(c.buzzer.beepMs) || 0) + (Number(c.buzzer.pauseMs) || 0));
  }

  private runText(ms: number): string {
    const s = ms / 1000;
    return s >= 3600 ? `${(s / 3600).toFixed(1)} h` : s >= 60 ? `${(s / 60).toFixed(1)} min` : `${s.toFixed(1)} s`;
  }

  /** Same limits as the backend (LocateCommands.cs). */
  private validateSignal(c: SignalForm): boolean {
    const e: Record<string, string> = {};
    const check = (key: string, v: number, min: number, max: number) => {
      if (v === null || v === undefined || (v as unknown) === '' || !Number.isInteger(Number(v)) || v < min || v > max) e[key] = `Enter a whole number from ${min} to ${max}`;
    };
    if (c.mode !== 'buzzer') {
      check('onMs', c.led.onMs, 1, 65535);
      check('offMs', c.led.offMs, 1, 65535);
      check('ledCycles', c.led.cycles, 1, 65535);
      check('brightness', c.led.brightness, 1, 100);
    }
    if (c.mode !== 'led') {
      check('beepMs', c.buzzer.beepMs, 20, 65535);
      check('pauseMs', c.buzzer.pauseMs, 20, 65535);
      check('buzzerCycles', c.buzzer.cycles, 1, 65535);
    }
    c.errors = e;
    return !Object.keys(e).length;
  }

  /** Send to tag: {"type":"led"|"buzzer"|"both","tagid",led:{...},buzzer:{...}} on /ws/locate. */
  sendSignal(): void {
    const c = this.ctl;
    if (!c || c.busy) return;
    if (!this.validateSignal(c)) {
      c.status = 'Fix the highlighted fields first.';
      c.statusKind = 'bad';
      return;
    }
    const led = { color: Number(c.led.color), onMs: Number(c.led.onMs), offMs: Number(c.led.offMs), cycles: Number(c.led.cycles), brightness: Number(c.led.brightness) };
    const buzzer = { beepMs: Number(c.buzzer.beepMs), pauseMs: Number(c.buzzer.pauseMs), cycles: Number(c.buzzer.cycles), level: 0 };
    this.ensureCtlSocket();
    this.ctlSock!.send({ type: c.mode, tagid: c.tagId, ...(c.mode !== 'buzzer' ? { led } : {}), ...(c.mode !== 'led' ? { buzzer } : {}) });
    this.focusAsset(c.asset.assetId);
    c.busy = true;
    c.status = 'Sending to the gateway…';
    c.statusKind = 'wait';
    const runMs = c.mode === 'led' ? this.ledMs(c) : c.mode === 'buzzer' ? this.buzzerMs(c) : Math.max(this.ledMs(c), this.buzzerMs(c));
    this.startSignal(c.asset, c.tagId, c.mode, runMs);
  }

  /** Turn off: {"type":"off","tagid"} (LED colour 0 and buzzer cycles 0 in one gateway message). */
  turnOffSignal(): void {
    const tag = this.ctl?.tagId ?? this.signalling?.tagId;
    if (!tag) return;
    const offAsset = this.ctl?.asset.assetId ?? this.signalling?.assetId;
    if (offAsset) this.focusAsset(offAsset);
    this.ensureCtlSocket();
    this.ctlSock!.send({ type: 'off', tagid: tag });
    if (this.ctl) {
      this.ctl.busy = true;
      this.ctl.status = 'Turning the LED and buzzer off…';
      this.ctl.statusKind = 'wait';
    }
    this.signalMsg = 'Turning off…';
  }

  /** Puts the amber marker on the map at the gateway hearing the tag and zooms to it. */
  private startSignal(a: RtlAsset, tagId: string, command: string, runMs: number): void {
    clearTimeout(this.signalTimer);
    this.signalling = { assetId: a.assetId, name: a.name, tagId, command, state: 'sent', until: Date.now() + runMs, gatewayId: this.ctl?.gatewayId || a.location?.gatewayId || '' };
    this.signalMsg = 'Sent. Waiting for the tag…';
    this.placeSignal();
  }

  private placeSignal(): void {
    const s = this.signalling;
    if (!s) {
      this.signalPoint = null;
      return;
    }
    this.loadGatewaySpots().subscribe(() => {
      const spot = this.gatewaySpots.get(s.gatewayId.toLowerCase());
      if (!spot || this.signalling !== s) return;
      const floor = this.floorOfPlan(spot.floorPlanId);
      if (floor && floor !== this.mapFloorId) this.setFloor(floor);
      const what = s.command === 'buzzer' ? 'beeping' : s.command === 'both' ? 'blinking + beeping' : 'blinking';
      this.signalPoint = { x: spot.x, y: spot.y, label: s.state === 'on' ? `${s.name} · ${what}` : `${s.name} · sending…`, state: s.state === 'on' ? 'Live' : 'Waiting', assetId: s.assetId };
      if (!this.followId && this.signalFocused !== s) {
        this.signalFocused = s;
        this.focusAsset(s.assetId);
      }
    });
  }

  private endSignal(message = ''): void {
    clearTimeout(this.signalTimer);
    this.signalling = null;
    this.signalPoint = null;
    this.signalMsg = '';
    if (message) this.showToast(message);
    this.signalFocused = null;
    if (!this.ctl) this.closeCtlSocket();
  }

  private ensureCtlSocket(): void {
    if (this.ctlSock) return;
    const sock = this.api.locateSocket();
    this.ctlSock = sock;
    this.ctlSub = sock.messages.subscribe((m) => this.onCtlMessage(m));
  }

  private closeCtlSocket(): void {
    this.ctlSub?.unsubscribe();
    this.ctlSub = undefined;
    this.ctlSock?.close();
    this.ctlSock = undefined;
  }

  private onCtlMessage(m: LocateMessage): void {
    const c = this.ctl;
    switch (m.type) {
      case 'subscribed':
        if (c && m.tagid?.toLowerCase() === c.tagId.toLowerCase()) {
          c.gatewayId = m.gatewayid ?? '';
          c.rssi = m.rssi ?? null;
          if (!c.busy) {
            c.status = '';
            c.statusKind = '';
          }
        }
        if (this.signalling && m.gatewayid) this.signalling.gatewayId = m.gatewayid;
        break;
      case 'commandSent':
        // The backend answers within Locating:CommandTimeoutSeconds (60 s) with completed / failed / timeout;
        // if even that never arrives (connection dropped), stop waiting here so Send works again
        clearTimeout(this.ctlWaitTimer);
        this.ctlWaitTimer = setTimeout(() => this.onCtlMessage({ type: 'commandStatus', command: m.command, status: 'timeout', message: 'No answer from the gateway in time. Check that the tag is near the gateway and try again.' }), 70_000);
        if (m.command === 'off') break;
        if (c) {
          c.status = `Sent to gateway ${m.gatewayid}. Waiting for the tag`;
          c.statusKind = 'wait';
        }
        if (this.signalling) {
          this.signalling.gatewayId = m.gatewayid ?? this.signalling.gatewayId;
          this.placeSignal();
        }
        break;
      case 'commandStatus': {
        const done = m.status !== 'accepted';
        if (done) clearTimeout(this.ctlWaitTimer);
        if (c) {
          if (done) c.busy = false;
          const txt = m.message ?? m.status ?? '';
          // sent -> acknowledged -> failed, spelled out
          c.status = m.status === 'accepted' ? `Acknowledged by the gateway. ${txt}`.trim() : m.status === 'completed' ? `Acknowledged by the tag. ${txt}`.trim() : `Failed: ${txt}`;
          c.statusKind = m.status === 'completed' ? 'ok' : m.status === 'accepted' ? 'wait' : 'bad';
        }
        if (m.command === 'off') {
          if (m.status === 'completed') this.endSignal(`${this.signalling?.name ?? 'Tag'}: LED and buzzer turned off.`);
          else if (done) this.signalMsg = m.message ?? 'Turn off failed.';
          break;
        }
        const s = this.signalling;
        if (!s) break;
        if (m.status === 'accepted') this.signalMsg = 'Gateway accepted. Waiting for the tag…';
        else if (m.status === 'completed') {
          s.state = 'on';
          this.signalMsg = m.message ?? 'Tag is signalling.';
          this.placeSignal();
          // Clear the marker when the pattern has run out
          this.signalTimer = setTimeout(() => this.endSignal(), Math.max(1000, s.until - Date.now()));
        } else this.endSignal(`${s.name}: ${m.message ?? 'the tag did not respond.'}`);
        break;
      }
      case 'error':
        if (c) {
          c.busy = false;
          c.status = m.message ?? 'Something went wrong.';
          c.statusKind = 'bad';
        }
        if (this.signalling?.state === 'sent') this.endSignal(m.message ?? 'The command could not be sent.');
        break;
      case 'closed':
        this.ctlSub?.unsubscribe();
        this.ctlSub = undefined;
        this.ctlSock = undefined;
        if (c?.busy) {
          c.busy = false;
          c.status = 'The connection closed. Try again.';
          c.statusKind = 'bad';
        }
        break;
    }
  }

  // ---- live follow

  /**
   * Location button in Locate asset: go to the asset's floor, select it and zoom in on it, then follow it live over the
   * Locating socket (/ws/locate): every reply gives the gateway now hearing its tag, and the red marker moves to that
   * gateway on the floor plan (and to its floor). Stop unsubscribes and closes the socket.
   */
  startLive(a: RtlAsset): void {
    this.locateOpen = false;
    this.stopLive();
    this.clearFilters();
    const floor = this.floorOfPlan(a.location?.floorPlanId);
    if (floor && floor !== this.mapFloorId) this.setFloor(floor);
    this.select(a.assetId);
    this.followId = a.assetId;
    this.followName = a.name;
    this.followState = 'connecting';
    this.followError = '';
    // Zoom in straight away on the asset's own marker; live replies then move it
    this.liveFocusKey = '';
    this.focusAsset(a.assetId);
    const tag = (a.tags[0] ?? a.tracker ?? '').trim();
    if (!tag) {
      this.followState = 'error';
      this.followError = `${a.name} has no tag linked (Tag Management), so it cannot be followed live.`;
      return;
    }
    const assetId = a.assetId;
    this.liveSub = this.loadGatewaySpots().subscribe(() => {
      if (this.followId !== assetId) return;
      this.liveSub = this.api.locate(tag).subscribe((m) => this.onLive(m, a, tag));
    });
  }

  /** Gateways plotted on every floor plan (loaded once). */
  private loadGatewaySpots(): Observable<void> {
    if (this.gatewaySpots.size) return of(undefined);
    const plans = [...this.plans.entries()];
    if (!plans.length) return of(undefined);
    return forkJoin(
      plans.map(([floorId, plan]) =>
        forkJoin({
          plots: this.floorApi.plots(plan.id).pipe(catchError(() => of([] as PlanPlot[]))),
          zones: this.floorApi.zones(plan.id).pipe(catchError(() => of([] as PlanZone[]))),
        }).pipe(map(({ plots, zones }) => ({ floorId, planId: plan.id, plots, zones })))
      )
    ).pipe(
      map((all) => {
        for (const { floorId, planId, plots, zones } of all) {
          const floorName = this.floors.find((f) => f.id === floorId)?.label.split(' > ').pop() ?? null;
          for (const p of plots) {
            if (p.kind !== 'device' || !p.itemId) continue;
            const zoneName = zones.find((z) => z.id === p.zoneId)?.name ?? null;
            this.gatewaySpots.set(p.itemId.toLowerCase(), { floorPlanId: planId, x: p.x, y: p.y, zoneName, floorName });
            if (p.code) this.gatewaySpots.set(p.code.toLowerCase(), { floorPlanId: planId, x: p.x, y: p.y, zoneName, floorName });
          }
        }
      })
    );
  }

  private onLive(m: LiveMessage, a: RtlAsset, tag: string): void {
    if (this.followId !== a.assetId) return;
    this.followState = m.state;
    if (m.state === 'error') {
      this.followError = m.message ?? 'Live location failed.';
      return;
    }
    const r: LocateReply | undefined = m.data;
    if (!r) return;
    this.followError = '';
    const spot = this.gatewaySpots.get(r.gatewayid?.toLowerCase() ?? '');
    const fresh = !!r.lastSeen && Date.now() - new Date(r.lastSeen).getTime() <= 60_000;
    const d: LiveSnapshot = {
      assetId: a.assetId,
      name: a.name,
      tagId: r.tagid || tag,
      state: fresh ? 'Live' : 'Stale',
      gatewayId: r.gatewayid,
      rssi: r.rssi,
      lastSeenAt: r.lastSeen,
      floorPlanId: spot?.floorPlanId ?? null,
      zoneName: spot?.zoneName ?? null,
      floorName: spot?.floorName ?? null,
      x: spot?.x ?? null,
      y: spot?.y ?? null,
      note: spot
        ? `Tag ${r.tagid} was last heard by gateway ${r.gatewayid} (BLE proximity: shown at that gateway).`
        : `Tag ${r.tagid} was last heard by gateway ${r.gatewayid}, which is not plotted on a floor plan (Maps & Floor Plans).`,
    };
    this.live = d;
    // The asset went to another floor: show that floor
    const floor = this.floorOfPlan(d.floorPlanId);
    if (floor && floor !== this.mapFloorId) {
      this.setFloor(floor);
      this.liveTrail = [];
    }
    if (d.x === null || d.y === null) {
      // Heard by a gateway that is not on a floor plan: keep the last known point, greyed out, instead of dropping it
      if (this.livePoint) this.livePoint = { ...this.livePoint, state: 'Last known' };
      return;
    }
    this.livePoint = { x: d.x, y: d.y, label: d.name, state: d.state, assetId: a.assetId };
    const last = this.liveTrail[this.liveTrail.length - 1];
    if (!last || Math.hypot(last.x - d.x, last.y - d.y) > 0.3) this.liveTrail = [...this.liveTrail, { x: d.x, y: d.y }].slice(-60);
    // Zoom to the asset's marker once per asset, floor and zone (it glides to the new zone when it moves)
    const key = `${d.assetId}:${d.floorPlanId ?? ''}:${d.zoneName ?? ''}`;
    if (this.liveFocusKey !== key) {
      const first = !this.liveFocusKey;
      this.liveFocusKey = key;
      if (!first) setTimeout(() => this.focusAsset(d.assetId), 400);
    }
  }

  /** Stop: closes the socket (the server stops streaming) and removes the live marker. */
  stopLive(): void {
    this.liveSub?.unsubscribe();
    this.liveSub = undefined;
    this.followId = this.followName = this.followError = '';
    this.followState = '';
    this.live = null;
    this.livePoint = null;
    this.liveTrail = [];
    this.mapFocus = null;
    this.liveFocusKey = '';
  }

  get liveAgo(): string {
    return this.live?.lastSeenAt ? this.agoText(this.live.lastSeenAt) : '—';
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

  /** Exports the Assets table of the page (current site / filters). */
  exportRequest = (): ExportRequest => ({
    title: 'Real-Time Location', fileBase: 'real-time-location',
    columns: [
      { label: 'Asset ID', key: 'assetId' }, { label: 'Asset', key: 'name' }, { label: 'Type', value: (a: any) => a.assetType ?? '' },
      { label: 'Zone', value: (a: any) => a.location?.zoneName ?? '' }, { label: 'Floor', value: (a: any) => a.location?.floorName ?? '' }, { label: 'Gateway', value: (a: any) => a.location?.gatewayId ?? '' },
      { label: 'Movement', key: 'movement' }, { label: 'Freshness', key: 'freshness' }, { label: 'Last valid location', value: (a: any) => this.stamp(a.lastValidLocationAt) },
      { label: 'Source', key: 'locationSource' }, { label: 'Accuracy', key: 'accuracy' }, { label: 'Confidence', key: 'confidence' }, { label: 'Tracker', value: (a: any) => a.tags.join(', ') },
      { label: 'Registered location', value: (a: any) => a.expected?.zoneName ?? a.expected?.floorName ?? '' }, { label: 'Comparison', key: 'comparisonStatus' },
    ],
    filters: { Site: this.sites?.find((s: any) => s.id === this.site)?.name || this.site },
    rows: () => this.filtered,
  });

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
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(msg);
  }
}

/** Locate asset › LED / buzzer form (limits as in the backend's LocateCommands.cs). */
interface SignalForm {
  asset: RtlAsset;
  tagId: string;
  mode: 'led' | 'buzzer' | 'both';
  gatewayId: string;
  rssi: number | null;
  led: { onMs: number; offMs: number; cycles: number; color: number; brightness: number };
  buzzer: { beepMs: number; pauseMs: number; cycles: number };
  errors: Record<string, string>;
  status: string;
  statusKind: 'ok' | 'bad' | 'wait' | '';
  busy: boolean;
}
