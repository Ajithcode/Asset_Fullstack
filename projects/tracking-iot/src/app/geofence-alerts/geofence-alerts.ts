import { Component, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { Observable, Subscription, catchError, forkJoin, of, switchMap } from 'rxjs';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  Bell,
  Box,
  Boxes,
  Building2,
  CalendarClock,
  Check,
  ChevronLeft,
  ChevronRight,
  Circle,
  Copy,
  Download,
  Ellipsis,
  History,
  Layers,
  Locate,
  LogIn,
  LogOut,
  MapPin,
  Minus,
  Pencil,
  Pentagon,
  Plus,
  Power,
  Search,
  Settings,
  Shapes,
  ShieldAlert,
  Timer,
  Trash2,
  TriangleAlert,
  User,
  Wifi,
  X,
} from 'lucide-angular';
import {
  AlertEvent, CONDITIONS, COOLDOWN_OPTIONS, CUSTOM_SCHEDULE, DEBOUNCE_OPTIONS, ESCALATE_OPTIONS, EVENT_STATUSES, MAP_ZONE_PREFIX, NOTIFY_EVERY,
  NOTIFY_REPEAT, EventStatus, OPEN_STATUSES, RULE_STATUSES, Rule, RuleStatus, SEVERITIES, Severity, TRIGGERS, Trigger, WEEKDAYS, Zone, ZoneShape,
  ZoneType, startOfToday,
} from './geofence-data';
import type * as Leaflet from 'leaflet';
import { apiErrorMessage } from '../shared/tracking-api';
import { MapDrawTools } from '../shared/map-draw-tools/map-draw-tools';
import { GeofenceMap, MapAlert, MapMode } from './geofence-map';
import { LL, areaM2 } from './geofence-geo';
import { DEFAULT_GEOFENCE_OPTIONS, GeofenceApiService, GeofenceOptionsDto, SiteOption } from './geofence-api.service';
import { AlertRuleApiService } from './alert-rule-api.service';
import { IoTAlertApiService, statusToApi } from './iot-alert-api.service';
import { GeofenceEventQuery, GeofenceKpis, GeofenceSummaryApi, GeofenceTarget } from './geofence-summary-api';
import { FloorMapApiService, FloorOption, FloorPlanRef, GatewayFeed, PlanPlot, PlanZone, ZoneOption } from './floor-map-api.service';
import { FloorPlanMap } from './floor-plan-map';
import { RtlApi, RtlEvent } from '../real-time-location/rtl-api';
import { DesktopAlertsService } from 'shared-ui';
import { RELAX_VALIDATION } from '../../../../../src/relax-validation/relax-validation';

type Tab = 'geofences' | 'rules' | 'history';
type RuleForm = Omit<Rule, 'id' | 'name' | 'zoneId' | 'enabled'>;
interface RuleDialog extends RuleForm { id: string; name: string; zoneId: string; enabled: boolean }
interface ZoneForm { id: string; name: string; type: ZoneType; category: string; site: string; color: string; groups: string[]; schedule: string; monitorEntry: boolean; monitorExit: boolean; shape: ZoneShape | ''; points: number; drawn?: LL[] }
interface Confirm { title: string; message: string; label: string; reason?: { label: string; value: string; required: boolean }; run: (reason: string) => void }
/** Assign / resolve / close with a note (kept in the alert's audit history). */
interface StatusDialog { event: AlertEvent; status: EventStatus; title: string; assignee: string; note: string; needsAssignee: boolean }

/** Default colour per zone type (matches the API's map colours). */
const TYPE_COLOR: Record<ZoneType, string> = { Allowed: '#2563EB', Warning: '#F5B40B', Restricted: '#EF4444' };

const DEFAULT_RULE: RuleForm = {
  trigger: '' as Trigger, dwellMin: 5, severity: '' as Severity, group: '', condition: '',
  createAlert: true, notify: false, team: '', workflow: false, workflowName: '',
  inApp: true, email: true, push: true,
  notifyEvery: 5, notifyRepeat: 1, notifySound: true,
  status: 'Active', description: '', assetIds: [], authorizedGroups: [], scheduleDays: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'], scheduleFrom: '08:00', scheduleTo: '18:00',
  debounceSec: 30, cooldownMin: 5, escalateAfterMin: 0, escalateTo: '', autoResolve: false, effectiveFrom: '', effectiveTo: '',
};

const LIST_PAGE = 6;
const HIST_PAGE = 10;
/** Summary and the visible alert lists are re-polled this often (ms). */
const POLL_MS = 30_000;
/** Recipients suggested before any rule names one (Administration teams). */
const DEFAULT_TEAMS = ['Security Team', 'Operations Team', 'Maintenance Team', 'Facility Management', 'Asset Custodian', 'Asset Owner'];

@Component({
  selector: 'app-geofence-alerts',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideAngularModule, GeofenceMap, MapDrawTools, FloorPlanMap],
  templateUrl: './geofence-alerts.html',
  styleUrls: ['./geofence-alerts.css', './geofence-alerts-sections.css', '../shared/iot-purple-theme.css', './geofence-alerts-v2.css'],
})
export class GeofenceAlerts implements OnInit, OnDestroy {
  readonly icons = { ArrowDown, ArrowUp, Bell, Box, Boxes, Building2, CalendarClock, Check, ChevronLeft, ChevronRight, Circle, Copy, Download, Ellipsis, History, Layers, Locate, LogIn, LogOut, MapPin, Minus, Pencil, Pentagon, Plus, Power, Search, Settings, Shapes, ShieldAlert, Timer, Trash2, TriangleAlert, User, Wifi, X };
  readonly conditions = CONDITIONS;
  readonly customSchedule = CUSTOM_SCHEDULE;
  readonly weekdays = WEEKDAYS;
  readonly severities = SEVERITIES;
  readonly ruleStatuses = RULE_STATUSES;
  readonly triggers = TRIGGERS;
  readonly eventStatuses = EVENT_STATUSES;
  readonly notifyEvery = NOTIFY_EVERY;
  readonly notifyRepeat = NOTIFY_REPEAT;
  readonly debounceOptions = DEBOUNCE_OPTIONS;
  readonly cooldownOptions = COOLDOWN_OPTIONS;
  readonly escalateOptions = ESCALATE_OPTIONS;
  /** Desktop notifications on this browser (Web Push). */
  readonly desktop = inject(DesktopAlertsService);

  enableDesktop(): void {
    this.desktop.enable().then((ok) => this.showToast(ok ? 'Desktop notifications are on for this browser.' : this.desktop.state === 'denied' ? 'Notifications are blocked for this site in the browser settings.' : 'Could not turn on desktop notifications.'));
  }
  /** Teams / workflows have no API list yet; the inputs accept free text (suggestions: Administration teams + existing rules). */
  get teams(): string[] {
    return [...new Set([...DEFAULT_TEAMS, ...this.rules.flatMap((r) => [r.team, r.escalateTo]).filter(Boolean)])].sort();
  }
  get workflows(): string[] {
    return [...new Set(['Restricted Zone Incident', ...this.rules.map((r) => r.workflowName).filter(Boolean)])].sort();
  }

  private readonly geofenceApi = inject(GeofenceApiService);
  private readonly ruleApi = inject(AlertRuleApiService);
  private readonly alertApi = inject(IoTAlertApiService);
  private readonly summaryApi = inject(GeofenceSummaryApi);
  private readonly rtlApi = inject(RtlApi);

  // option lists (GET /api/geofences/options)
  options: GeofenceOptionsDto = DEFAULT_GEOFENCE_OPTIONS;
  private masterSites: SiteOption[] = [];

  get groups(): string[] {
    return this.options.assetGroups;
  }
  get schedules(): string[] {
    return this.options.schedules;
  }
  get colors(): string[] {
    return this.options.mapColours;
  }
  get zoneTypes(): ZoneType[] {
    return this.options.zoneTypes as ZoneType[];
  }
  get shapes(): ZoneShape[] {
    return this.options.shapes as ZoneShape[];
  }
  get categories(): string[] {
    return this.options.categories ?? DEFAULT_GEOFENCE_OPTIONS.categories ?? [];
  }

  zones: Zone[] = [];
  rules: Rule[] = [];
  /** Indoor (floor plan) + outdoor monitored areas with live figures (GET /api/geofence-alerts/summary). */
  targets: GeofenceTarget[] = [];
  summaryKpis: GeofenceKpis | null = null;
  /** Newest alerts for the Recent Alert Events card. */
  recentEvents: AlertEvent[] = [];

  loading = false;
  loadError = '';
  saving = false;

  tab: Tab = 'geofences';
  selectedId = '';
  menuOpen = '';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private pollTimer?: ReturnType<typeof setInterval>;

  // map
  satellite = false;
  drawMap: Leaflet.Map | null = null;
  showAssets = true;
  showGateways = true;
  showLabels = true;
  layersOpen = false;
  @ViewChild(GeofenceMap) private gmap?: GeofenceMap;
  mapMode: MapMode = 'view';
  editId = '';
  drawCount = 0;
  zoomInfo = { zoom: 2, min: 2, max: 20 };
  /** Site shown while drawing a zone for another site. */
  private drawSite = '';
  /** Site picked in the map's site selector when it has no zone to select. */
  private pickedSite = '';
  /** Zone form waiting for a shape drawn on the map. */
  private pendingForm: ZoneForm | null = null;
  private editDraft: LL[] | null = null;

  // list / filters
  listSearch = '';
  listSite = '';
  listKind: '' | 'Indoor' | 'Outdoor' = '';
  listPage = 0;
  ruleZone = '';
  ruleSearch = '';
  ruleStatus: '' | RuleStatus = '';

  // event history (server paged)
  histMode: 'alerts' | 'movements' = 'alerts';
  hDate: '' | 'today' | 'yesterday' | '7d' | '30d' = '7d';
  hZone = '';
  hType = '';
  hStatus = '';
  hSeverity = '';
  hSearch = '';
  hPage = 1;
  hRows: AlertEvent[] = [];
  hTotal = 0;
  hPages = 1;
  hTypes: string[] = [];
  hLoading = false;
  private hSub?: Subscription;
  private searchTimer?: ReturnType<typeof setTimeout>;
  // zone movement events (location events, not alerts)
  mRange: '24h' | '7d' = '24h';
  mType = '';
  mPage = 1;
  mRows: RtlEvent[] = [];
  mTotal = 0;
  mPages = 1;
  mTypes: string[] = [];

  // forms & dialogs
  builder: RuleForm & { name: string } = { ...DEFAULT_RULE, name: '' };
  zoneForm: ZoneForm | null = null;
  ruleDialog: RuleDialog | null = null;
  confirmBox: Confirm | null = null;
  statusDialog: StatusDialog | null = null;
  detail: AlertEvent | null = null;
  formError = '';

  /** ?newRule=1&zone=zone:{id}&asset={name} (Real-Time Location › Create Alert): open a new rule for that zone. */
  private readonly route = inject(ActivatedRoute);
  private ruleRequestHandled = false;

  private openRequestedRule(): void {
    const q = this.route.snapshot.queryParamMap;
    if (this.ruleRequestHandled || !q.get('newRule')) return;
    this.ruleRequestHandled = true;
    this.tab = 'rules';
    this.openRule();
    const zone = q.get('zone');
    const asset = q.get('asset');
    if (this.ruleDialog) {
      if (zone) this.ruleDialog.zoneId = zone;
      if (asset) {
        this.ruleDialog.name = `${asset} location alert`;
        this.ruleDialog.assetIds = [asset];
      }
    }
  }

  ngOnInit(): void {
    this.load();
    this.loadFloors();
    this.pollTimer = setInterval(() => this.refreshLive(), POLL_MS);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    clearTimeout(this.searchTimer);
    clearInterval(this.pollTimer);
    this.planSub?.unsubscribe();
    this.feedSub?.unsubscribe();
    this.hSub?.unsubscribe();
  }

  // ===== floor plan view (Building > Floor, then Zone) =====
  private readonly floorApi = inject(FloorMapApiService);
  floorOptions: FloorOption[] = [];
  /** Floors request finished (the dropdown says when there are none). */
  floorsLoaded = false;
  private zoneOptions: ZoneOption[] = [];
  private floorPlans = new Map<string, FloorPlanRef>();
  /** Selected floor; '' = outdoor geofence map. */
  floorId = '';
  /** Selected Master Data zone of the floor; '' = the whole floor. */
  floorZoneId = '';
  planZones: PlanZone[] = [];
  planPlots: PlanPlot[] = [];
  planLoading = false;
  private planError = '';
  /** Live assets per gateway (lower-case gateway id) for the zones' "Total Assets". */
  gatewayFeed: Record<string, GatewayFeed> = {};
  private planSub?: Subscription;
  private feedSub?: Subscription;
  /** Zone to pick once the floor's zones are loaded (selecting an indoor geofence from the list). */
  private pendingZoneCode = '';

  private loadFloors(): void {
    forkJoin({
      locations: this.floorApi.locations(),
      plans: this.floorApi.floorPlans().pipe(catchError(() => of(new Map<string, FloorPlanRef>()))),
    }).subscribe({
      next: ({ locations, plans }) => {
        this.floorOptions = locations.floors;
        this.zoneOptions = locations.zones;
        this.floorPlans = plans;
        this.floorsLoaded = true;
        this.loadMapZoneTargets(locations.floors, plans);
        // open on the first floor that has a floor plan (the outdoor geofence map stays one pick away)
        const first = this.floorOptions.find((f) => plans.has(f.id));
        if (first && !this.floorId && this.mapMode === 'view') this.selectFloor(first.id);
      },
      error: (err) => {
        this.floorsLoaded = true;
        this.showToast(apiErrorMessage(err, 'Could not load buildings and floors.'));
      },
    });
  }

  /** Floor plan zones (drawn in Maps & Floor Plans) that alert rules can watch: "zone:{id}". */
  mapZoneRuleTargets: { id: string; label: string; gateways: number }[] = [];

  /** The rule dialog's floor zone has no gateway plotted in it: it can never raise alerts. */
  get ruleZoneWithoutGateway(): boolean {
    const id = this.ruleDialog?.zoneId ?? '';
    return id.startsWith(MAP_ZONE_PREFIX) && this.mapZoneRuleTargets.find((t) => t.id === id)?.gateways === 0;
  }

  private loadMapZoneTargets(floors: FloorOption[], plans: Map<string, FloorPlanRef>): void {
    const list = [...plans.values()];
    if (!list.length) return;
    const perPlan$ = list.map((p) => forkJoin({ zones: this.floorApi.zones(p.id).pipe(catchError(() => of<PlanZone[]>([]))), plots: this.floorApi.plots(p.id).pipe(catchError(() => of<PlanPlot[]>([]))) }));
    forkJoin(perPlan$).subscribe((perPlan) => {
      const targets: { id: string; label: string; gateways: number }[] = [];
      perPlan.forEach(({ zones, plots }, i) => {
        const floor = floors.find((f) => f.id === list[i].locationId)?.label ?? list[i].name;
        for (const z of zones) {
          // live alerts need a gateway (device) plotted inside the zone
          const gateways = plots.filter((p) => p.kind === 'device' && p.zoneId === z.id).length;
          targets.push({ id: MAP_ZONE_PREFIX + z.id, label: `${z.name} (${z.code}) · ${floor}${gateways ? '' : ' — no gateway'}`, gateways });
        }
      });
      this.mapZoneRuleTargets = targets.sort((a, b) => a.label.localeCompare(b.label));
    });
  }

  /** Zones of the selected floor (Master Data). */
  get floorZones(): ZoneOption[] {
    return this.zoneOptions.filter((z) => z.floorId === this.floorId);
  }

  get floorPlan(): FloorPlanRef | undefined {
    return this.floorPlans.get(this.floorId);
  }

  /** Floor plan file, else the floor picture from Master Data. */
  get floorImage(): string | null {
    return this.floorPlan?.fileUrl ?? this.floorOptions.find((f) => f.id === this.floorId)?.image ?? null;
  }

  /** The floor picture from Master Data (fallback when the plan file is missing). */
  get floorMdmImage(): string | null {
    return this.floorOptions.find((f) => f.id === this.floorId)?.image ?? null;
  }

  /** The drawn zone shown alone: undefined = whole floor, null = the picked zone is not drawn on the plan yet. */
  get floorScope(): string | null | undefined {
    const z = this.zoneOptions.find((o) => o.id === this.floorZoneId);
    if (!z) return undefined;
    const code = z.code.trim().toUpperCase();
    return this.planZones.find((p) => p.code.trim().toUpperCase() === code)?.id ?? null;
  }

  get floorNote(): string {
    if (this.planLoading) return 'Loading floor plan…';
    if (this.planError) return this.planError;
    if (!this.floorImage) return 'No floor plan for this floor yet - upload one in Maps & Floor Plans.';
    if (this.floorScope === null) return 'This zone is not drawn on the floor plan yet - draw it in Maps & Floor Plans.';
    return '';
  }

  /** Devices / assets on the map now (floor, or the picked zone). */
  floorCount(kind: 'device' | 'asset'): number {
    const scope = this.floorScope;
    return this.planPlots.filter((p) => p.kind === kind && (scope === undefined || (!!scope && p.zoneId === scope))).length;
  }

  selectFloor(id: string): void {
    this.floorId = id;
    this.floorZoneId = '';
    this.planZones = [];
    this.planPlots = [];
    this.planError = '';
    this.planSub?.unsubscribe();
    if (!id) return;
    this.startFeed();
    const plan = this.floorPlan;
    if (!plan) return;
    this.planLoading = true;
    this.planSub = forkJoin({ zones: this.floorApi.zones(plan.id), plots: this.floorApi.plots(plan.id) }).subscribe({
      next: ({ zones, plots }) => {
        if (id !== this.floorId) return;
        this.planZones = zones;
        this.planPlots = plots;
        this.planLoading = false;
        this.applyPendingZone();
      },
      error: (err) => {
        this.planLoading = false;
        this.planError = apiErrorMessage(err, 'Could not load the floor plan.');
      },
    });
  }

  /** Floor zone picked in the tab row: select the matching indoor geofence too. */
  onFloorZone(id: string): void {
    this.floorZoneId = id;
    const code = this.zoneOptions.find((o) => o.id === id)?.code.trim().toUpperCase();
    const drawn = code ? this.planZones.find((p) => p.code.trim().toUpperCase() === code) : undefined;
    if (drawn) this.selectedId = MAP_ZONE_PREFIX + drawn.id;
  }

  private applyPendingZone(): void {
    if (!this.pendingZoneCode) return;
    const code = this.pendingZoneCode;
    this.pendingZoneCode = '';
    this.floorZoneId = this.floorZones.find((z) => z.code.trim().toUpperCase() === code)?.id ?? '';
  }

  /** Live gateway feed, started with the first floor shown. */
  private startFeed(): void {
    if (this.feedSub) return;
    this.feedSub = this.floorApi.gatewaysLive().subscribe((m) => {
      if (!m.data) return;
      const next: Record<string, GatewayFeed> = {};
      for (const g of m.data) next[g.gatewayId.toLowerCase()] = g;
      this.gatewayFeed = next;
    });
  }

  // ===== loading =====
  load(): void {
    this.loading = true;
    this.loadError = '';
    forkJoin({
      zones: this.geofenceApi.list(),
      rules: this.ruleApi.list(),
      options: this.geofenceApi.options().pipe(catchError(() => of(DEFAULT_GEOFENCE_OPTIONS))),
      sites: this.geofenceApi.sites(),
    }).subscribe({
      next: ({ zones, rules, options, sites }) => {
        this.zones = zones;
        this.rules = rules;
        this.options = { ...DEFAULT_GEOFENCE_OPTIONS, ...options };
        this.masterSites = sites;
        if (this.builder.group && !this.groups.includes(this.builder.group)) this.builder.group = '';
        this.loading = false;
        this.refreshLive();
        this.openRequestedRule();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load geofences.');
        this.showToast(this.loadError);
      },
    });
  }

  /** KPIs, the zone list figures, the recent alerts and the visible history page. */
  refreshLive(): void {
    this.loadSummary();
    this.loadRecent();
    if (this.tab === 'history') this.loadHistory(true);
  }

  private loadSummary(): void {
    this.summaryApi.summary(startOfToday()).subscribe({
      next: (s) => {
        this.targets = s.targets;
        this.summaryKpis = s.kpis;
        if (!this.selectedId || !this.targetById(this.selectedId)) this.selectedId = this.zones[0]?.id ?? s.targets[0]?.id ?? '';
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not load the geofence summary.')),
    });
  }

  private loadRecent(): void {
    this.summaryApi.events({ page: 1, pageSize: 5 }).subscribe({ next: (p) => (this.recentEvents = p.items), error: () => undefined });
  }

  private fail(err: unknown, fallback: string): void {
    this.saving = false;
    this.showToast(apiErrorMessage(err, fallback));
  }

  private replaceZone(z: Zone): void {
    const i = this.zones.findIndex((x) => x.id === z.id);
    if (i >= 0) this.zones[i] = { ...z, geoRev: (this.zones[i].geoRev ?? 0) + 1 };
    else this.zones.push(z);
    this.zones = [...this.zones];
    this.loadSummary();
  }

  // ===== options =====
  get sites(): string[] {
    return [...new Set([...this.masterSites.map((s) => s.name), ...this.zones.map((z) => z.site), ...this.targets.map((t) => t.siteName ?? '')].filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  private siteIdOf(name: string): string | null {
    return this.masterSites.find((s) => s.name === name)?.id ?? this.zones.find((z) => z.site === name && z.siteId)?.siteId ?? null;
  }

  // ===== KPIs =====
  get kpi() {
    const k = this.summaryKpis;
    const today = k?.alertsToday ?? 0;
    const yesterday = k?.alertsYesterday ?? 0;
    return {
      active: k?.activeGeofences ?? this.zones.filter((z) => z.status === 'Active').length,
      outdoor: k?.outdoorActive ?? 0,
      indoor: k?.indoorMonitored ?? 0,
      monitored: k?.monitoredAssets ?? 0,
      tagged: k?.taggedAssets ?? 0,
      today,
      yesterday,
      todayPct: yesterday ? Math.round(((today - yesterday) / yesterday) * 100) : null,
      open: k?.openAlerts ?? 0,
      unack: k?.unacknowledged ?? 0,
      escalated: k?.escalated ?? 0,
    };
  }

  signed(n: number, suffix = ''): string {
    return `${n > 0 ? '+' : n < 0 ? '−' : '±'}${Math.abs(n)}${suffix}`;
  }

  // ===== selection =====
  targetById(id: string): GeofenceTarget | undefined {
    return this.targets.find((t) => t.id === id);
  }

  /** The selected list row (indoor zone or outdoor geofence). */
  get selectedTarget(): GeofenceTarget | undefined {
    return this.targetById(this.selectedId);
  }

  /** The selected outdoor geofence (null when an indoor zone is selected). */
  get selected(): Zone | null {
    if (this.selectedId.startsWith(MAP_ZONE_PREFIX)) return null;
    return this.zones.find((z) => z.id === this.selectedId) ?? (this.selectedId ? null : this.zones[0] ?? null);
  }

  get selectedName(): string {
    return this.selected?.name ?? this.selectedTarget?.name ?? '';
  }

  selectZone(z: Zone, recenter = false): void {
    if (this.mapMode !== 'view') return;
    this.selectedId = z.id;
    this.pickedSite = '';
    if (recenter) {
      if (this.floorId) this.selectFloor('');
      setTimeout(() => this.locate(), 80);
    }
  }

  selectZoneById(id: string): void {
    const z = this.zoneById(id);
    if (z) this.selectZone(z);
  }

  /** List row → the zone on its map: an outdoor geofence on the site map, an indoor zone on its floor plan. */
  selectTarget(t: GeofenceTarget, show = false): void {
    if (this.mapMode !== 'view') return;
    if (t.kind === 'Outdoor') {
      const z = this.zoneById(t.id);
      if (z) this.selectZone(z, show);
      return;
    }
    this.selectedId = t.id;
    if (!show && !this.floorId) return;
    const floor = [...this.floorPlans.entries()].find(([, p]) => p.id === t.floorPlanId)?.[0];
    if (!floor) { if (show) this.showToast('This zone\'s floor plan is not available.'); return; }
    const code = (t.code ?? '').trim().toUpperCase();
    if (floor !== this.floorId) {
      this.pendingZoneCode = code;
      this.selectFloor(floor);
    } else {
      this.pendingZoneCode = code;
      this.applyPendingZone();
    }
  }

  zoneById(id: string): Zone | undefined {
    return this.zones.find((z) => z.id === id);
  }

  zoneColor(id: string): string {
    return this.zoneById(id)?.color ?? (id.startsWith(MAP_ZONE_PREFIX) ? '#7c3aed' : '#9ca3af');
  }

  ruleCount(id: string): number {
    return this.rules.filter((r) => r.zoneId === id).length;
  }

  /** Dwell thresholds come from the zone's rules (different groups can have different limits). */
  dwellRules(id: string): Rule[] {
    return this.rules.filter((r) => r.zoneId === id && r.trigger === 'dwell');
  }

  shapeLabel(z: Zone): string {
    if (z.shape === 'Circle') return z.radiusM ? `Circle (${Math.round(z.radiusM)} m radius)` : 'Circle (not placed)';
    if (!z.latlngs?.length) return `${z.shape} (not drawn)`;
    return z.shape === 'Rectangle' ? 'Rectangle' : `Polygon (${z.points} points)`;
  }

  targetShape(t: GeofenceTarget): string {
    const z = t.kind === 'Outdoor' ? this.zoneById(t.id) : undefined;
    if (z) return this.shapeLabel(z);
    return t.kind === 'Indoor' ? `Floor-plan ${(t.shape || 'polygon').toLowerCase()}` : t.shape ?? '—';
  }

  targetArea(t: GeofenceTarget): string {
    const z = t.kind === 'Outdoor' ? this.zoneById(t.id) : undefined;
    if (z) return this.zoneArea(z);
    return t.areaM2 ? this.fmtArea(t.areaM2) : '';
  }

  /** Building · floor for indoor zones, the site for outdoor geofences. */
  targetPlace(t: GeofenceTarget): string {
    return t.kind === 'Indoor' ? [t.buildingName, t.floorName].filter(Boolean).join(' · ') || t.locationPath || '—' : '—';
  }

  shortId(id: string): string {
    return id.startsWith(MAP_ZONE_PREFIX) ? 'Z-' + id.slice(MAP_ZONE_PREFIX.length, MAP_ZONE_PREFIX.length + 6).toUpperCase() : 'G-' + id.slice(-6).toUpperCase();
  }

  fmtWhen(d: Date | string | null | undefined): string {
    if (!d) return '—';
    const t = typeof d === 'string' ? new Date(d) : d;
    const t0 = startOfToday().getTime();
    const hm = `${String(t.getHours()).padStart(2, '0')}:${String(t.getMinutes()).padStart(2, '0')}`;
    if (t.getTime() >= t0) return hm;
    if (t.getTime() >= t0 - 86400000) return `Yesterday ${hm}`;
    return `${t.getDate()} ${t.toLocaleString('en-US', { month: 'short' })} ${hm}`;
  }

  fmtStamp(d: Date | string): string {
    const t = typeof d === 'string' ? new Date(d) : d;
    return `${t.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })} ${String(t.getHours()).padStart(2, '0')}:${String(t.getMinutes()).padStart(2, '0')}:${String(t.getSeconds()).padStart(2, '0')}`;
  }

  fmtDuration(sec: number | null | undefined): string {
    if (sec === null || sec === undefined) return '—';
    if (sec < 60) return `${sec}s`;
    const m = Math.round(sec / 60);
    return m < 60 ? `${m} min` : `${Math.floor(m / 60)} h ${m % 60} min`;
  }

  typeClass(t: string | null): string {
    return 'zt--' + (t ?? 'none').toLowerCase().replace(/[^a-z]+/g, '-');
  }

  evClass(t: string): string {
    return /unauthori|lost/i.test(t) ? 'ev--alert' : /dwell|remain/i.test(t) ? 'ev--dwell' : /exit|left|depart/i.test(t) ? 'ev--out' : 'ev--in';
  }

  stClass(s: EventStatus): string {
    return 'est--' + s.toLowerCase();
  }

  sevClass(s: string): string {
    return 'sev--' + (s || 'none').toLowerCase();
  }

  ruleStClass(s: RuleStatus): string {
    return 'rst--' + s.toLowerCase();
  }

  triggerLabel(t: Trigger, dwell = 0): string {
    if (t === 'dwell') return `Dwells longer than ${dwell} min`;
    return TRIGGERS.find((x) => x.value === t)?.label ?? '';
  }

  scheduleLabel(r: RuleForm): string {
    if (r.condition !== CUSTOM_SCHEDULE) return r.condition || '—';
    return `${r.scheduleDays.join(', ') || 'No days'} ${r.scheduleFrom}–${r.scheduleTo}`;
  }

  scopeLabel(r: RuleForm): string {
    const base = r.assetIds.length ? `${r.assetIds.length} specific asset${r.assetIds.length === 1 ? '' : 's'}` : r.group || '—';
    return r.trigger === 'unauthorized' && r.authorizedGroups.length ? `${base} · allowed: ${r.authorizedGroups.join(', ')}` : base;
  }

  actionsSummary(r: RuleForm): string {
    const out: string[] = [];
    if (r.createAlert) out.push('Create alert');
    if (r.notify) out.push(`Notify ${r.team || 'team'}`);
    if (r.workflow) out.push(`Workflow: ${r.workflowName || '—'}`);
    if (r.escalateAfterMin > 0) out.push(`Escalate to ${r.escalateTo || '—'} after ${r.escalateAfterMin} min`);
    if (r.autoResolve) out.push('Auto-resolve on exit');
    return out.join(' · ') || 'No actions';
  }

  channels(r: RuleForm): string {
    return [r.inApp ? 'In-app' : '', r.email ? 'Email' : '', r.push ? 'Push' : ''].filter(Boolean).join(', ') || '—';
  }

  // ===== map =====
  get mapSite(): string {
    return this.drawSite || this.pickedSite || (this.selected ? this.selected.site : this.sites[0] ?? '');
  }

  set mapSite(site: string) {
    const first = this.zones.find((z) => z.site === site);
    if (first) this.selectZone(first);
    else this.pickedSite = site;
  }

  get mapArea(): string {
    return this.mapSite;
  }

  /** Signature of everything the map draws; the map only redraws when it changes. */
  get mapRev(): string {
    const zs = this.zones.filter((z) => z.site === this.mapSite).map((z) => [z.id, z.name, z.type, z.color, z.status, z.shape, z.points, z.geoRev ?? 0, z.monitoredCount].join(':'));
    return zs.join('|') + '#' + this.mapAlerts.map((a) => a.key).join(',');
  }

  private alertCache: { sig: string; list: MapAlert[] } = { sig: '', list: [] };

  /** Newest new (unacknowledged) alert per outdoor geofence. */
  get mapAlerts(): MapAlert[] {
    const open = this.targets.filter((t) => t.kind === 'Outdoor' && t.latestOpenAlert);
    const sig = open.map((t) => t.latestOpenAlert!.alertId).join(',');
    if (sig !== this.alertCache.sig) {
      this.alertCache = {
        sig,
        list: open.map((t) => {
          const a = t.latestOpenAlert!;
          return { key: a.alertId, zoneId: t.id, assetCode: a.assetName, title: `${a.eventType}: ${a.assetName}`, sub: `${t.name} · ${this.fmtWhen(a.raisedAt)} · New` };
        }),
      };
    }
    return this.alertCache.list;
  }

  zoomIn(): void {
    this.gmap?.zoomIn();
  }

  zoomOut(): void {
    this.gmap?.zoomOut();
  }

  locate(): void {
    this.gmap?.locate();
  }

  resetView(): void {
    this.gmap?.resetView();
  }

  onAlertClick(): void {
    this.openHistory('7d', 'Open');
  }

  // ----- draw a new / replacement outline -----
  startDraw(): void {
    const f = this.zoneForm;
    if (!f) return;
    if (!f.site) { this.formError = 'Choose a site before drawing the outline.'; return; }
    this.pendingForm = f;
    this.zoneForm = null;
    this.drawSite = f.site;
    this.drawCount = 0;
    if (this.floorId) this.selectFloor('');
    this.mapMode = 'draw';
    this.tab = 'geofences';
  }

  onDrawDone(pts: LL[]): void {
    const f = this.pendingForm;
    this.mapMode = 'view';
    this.drawSite = '';
    this.pendingForm = null;
    if (!f) return;
    this.formError = '';
    if (f.shape === 'Rectangle' && pts.length !== 4) {
      this.formError = `A rectangle needs exactly 4 corners; ${pts.length} were drawn.`;
    } else if (pts.length > this.options.maxPolygonPoints) {
      this.formError = `A zone can have at most ${this.options.maxPolygonPoints} corners.`;
    } else {
      f.drawn = pts;
      if (f.shape === 'Circle') f.shape = 'Polygon';
      f.points = pts.length;
      this.showToast(`Outline drawn: ${pts.length} points, about ${this.fmtArea(areaM2(pts))}.`);
    }
    this.zoneForm = f;
  }

  finishDraw(): void {
    this.gmap?.finishDraw();
  }

  undoPoint(): void {
    this.gmap?.undoPoint();
  }

  cancelDraw(): void {
    this.mapMode = 'view';
    this.drawSite = '';
    this.zoneForm = this.pendingForm;
    this.pendingForm = null;
  }

  // ----- drag the corners of an existing zone -----
  canEditShape(z: Zone | null | undefined): boolean {
    return !!z && z.shape === 'Polygon' && !!z.latlngs?.length;
  }

  startEditShape(z: Zone): void {
    this.menuOpen = '';
    if (!this.canEditShape(z)) return;
    if (this.floorId) this.selectFloor('');
    this.selectedId = z.id;
    this.editId = z.id;
    this.editDraft = null;
    this.mapMode = 'edit';
    setTimeout(() => this.locate(), 80);
  }

  onEditChange(pts: LL[]): void {
    this.editDraft = pts;
  }

  saveShape(): void {
    const z = this.zoneById(this.editId);
    const pts = this.editDraft;
    this.mapMode = 'view';
    this.editId = '';
    this.editDraft = null;
    if (!z || !pts) return;
    this.geofenceApi.update({ ...z, latlngs: pts, points: pts.length }).subscribe({
      next: (saved) => {
        this.replaceZone(saved);
        this.showToast(`“${z.name}” outline saved: ${pts.length} points, about ${this.fmtArea(areaM2(pts))}.`);
      },
      error: (err) => {
        this.replaceZone(z);
        this.fail(err, 'Could not save the outline.');
      },
    });
  }

  cancelShape(): void {
    this.mapMode = 'view';
    this.editId = '';
    this.editDraft = null;
  }

  /** m² below one hectare, hectares above (computed from the saved geometry, never typed in). */
  fmtArea(m2: number): string {
    return m2 >= 10000 ? `${(m2 / 10000).toFixed(2)} ha` : `${Math.round(m2).toLocaleString()} m²`;
  }

  drawnArea(f: ZoneForm): number {
    return f.drawn ? areaM2(f.drawn) : 0;
  }

  zoneArea(z: Zone): string {
    if (z.shape === 'Circle' && z.radiusM) return this.fmtArea(Math.PI * z.radiusM * z.radiusM);
    return z.latlngs?.length ? this.fmtArea(areaM2(z.latlngs)) : '';
  }

  // ===== geofence list (indoor + outdoor) =====
  get listFiltered(): GeofenceTarget[] {
    const q = this.listSearch.trim().toLowerCase();
    return this.targets.filter((t) =>
      (!this.listSite || t.siteName === this.listSite) && (!this.listKind || t.kind === this.listKind)
      && (!q || `${this.shortId(t.id)} ${t.name} ${t.code ?? ''} ${t.type ?? ''} ${t.category ?? ''} ${t.siteName ?? ''} ${t.buildingName ?? ''} ${t.floorName ?? ''}`.toLowerCase().includes(q)));
  }

  get listPages(): number {
    return Math.max(1, Math.ceil(this.listFiltered.length / LIST_PAGE));
  }

  get listPageNo(): number {
    return Math.min(this.listPage, this.listPages - 1);
  }

  get listRows(): GeofenceTarget[] {
    const p = this.listPageNo;
    return this.listFiltered.slice(p * LIST_PAGE, p * LIST_PAGE + LIST_PAGE);
  }

  get listRange(): string {
    const n = this.listFiltered.length;
    if (!n) return '0 of 0';
    const p = this.listPageNo;
    return `${p * LIST_PAGE + 1}–${Math.min(n, p * LIST_PAGE + LIST_PAGE)} of ${n}`;
  }

  pageList(d: number): void {
    this.listPage = Math.min(Math.max(this.listPageNo + d, 0), this.listPages - 1);
  }

  showRules(id: string): void {
    this.ruleZone = id;
    this.ruleSearch = '';
    this.tab = 'rules';
  }

  showAlerts(id: string): void {
    this.hZone = id;
    this.openHistory('30d', 'Unresolved', true);
  }

  // ===== zone actions =====
  openZone(z?: Zone): void {
    this.menuOpen = '';
    this.formError = '';
    this.zoneForm = z
      ? { id: z.id, name: z.name, type: z.type, category: z.category ?? '', site: z.site, color: z.color, groups: [...z.groups], schedule: z.schedule, monitorEntry: z.monitorEntry, monitorExit: z.monitorExit, shape: z.shape, points: z.points || this.options.defaultPolygonPoints }
      : { id: '', name: '', type: 'Allowed', category: '', site: '', color: TYPE_COLOR.Allowed, groups: this.groups.slice(0, 1), schedule: '', monitorEntry: true, monitorExit: true, shape: '', points: this.options.defaultPolygonPoints };
  }

  get editingDrawn(): boolean {
    const f = this.zoneForm;
    if (!f || !f.id) return false;
    const z = this.zoneById(f.id);
    return !!z && !!z.latlngs?.length && f.shape !== 'Circle';
  }

  /** Unique monitored assets of the zone being edited (computed by the API). */
  get formAssetCount(): number | null {
    const f = this.zoneForm;
    return f?.id ? this.targetById(f.id)?.monitoredAssets ?? null : null;
  }

  toggleFormGroup(g: string): void {
    const f = this.zoneForm;
    if (!f) return;
    f.groups = f.groups.includes(g) ? f.groups.filter((x) => x !== g) : [...f.groups, g];
  }

  setFormType(t: ZoneType): void {
    if (!this.zoneForm) return;
    this.zoneForm.type = t;
    if (!this.zoneForm.id) this.zoneForm.color = TYPE_COLOR[t];
  }

  saveZone(): void {
    const f = this.zoneForm;
    if (!f || this.saving) return;
    const name = f.name.trim();
    if (!RELAX_VALIDATION && (!name)) { this.formError = 'Enter a zone name.'; return; }
    if (!RELAX_VALIDATION && (!f.site)) { this.formError = 'Choose a site.'; return; }
    if (!RELAX_VALIDATION && (!f.schedule)) { this.formError = 'Choose a schedule.'; return; }
    if (!f.shape) { this.formError = 'Choose a shape.'; return; }
    if (!RELAX_VALIDATION && (this.zones.some((z) => z.id !== f.id && z.site === f.site && z.name.toLowerCase() === name.toLowerCase()))) { this.formError = `“${name}” already exists at ${f.site}.`; return; }
    if (!RELAX_VALIDATION && (!f.groups.length)) { this.formError = 'Select at least one asset group.'; return; }
    const { minPolygonPoints: min, maxPolygonPoints: max } = this.options;
    const points = Math.min(max, Math.max(min, Math.round(f.points || this.options.defaultPolygonPoints)));
    const existing = f.id ? this.zoneById(f.id) : undefined;
    const base: Zone = existing
      ? { ...existing }
      : { id: '', name, type: f.type, site: f.site, siteId: null, status: 'Active', shape: f.shape, points, color: f.color, groups: [], schedule: f.schedule, monitorEntry: true, monitorExit: true, dwellMin: null, monitoredCount: 0, lastEventAt: null, createdDaysAgo: 0 };
    const zone: Zone = {
      ...base, name, type: f.type, category: f.category.trim(), site: f.site, siteId: this.siteIdOf(f.site), color: f.color, groups: f.groups, schedule: f.schedule,
      monitorEntry: f.monitorEntry, monitorExit: f.monitorExit, shape: f.shape, points,
    };
    if (f.shape === 'Circle') {
      zone.latlngs = undefined;
      if (existing?.shape !== 'Circle') { zone.center = undefined; zone.radiusM = undefined; }
    } else {
      zone.center = undefined;
      zone.radiusM = undefined;
      if (f.drawn) zone.latlngs = f.drawn;
      if (zone.latlngs?.length) zone.points = zone.latlngs.length;
      if (f.shape === 'Rectangle' && zone.latlngs && zone.latlngs.length !== 4) zone.latlngs = undefined;
    }
    this.saving = true;
    this.formError = '';
    const req = existing ? this.geofenceApi.update(zone) : this.geofenceApi.create(zone);
    req.subscribe({
      next: (saved) => {
        this.saving = false;
        this.replaceZone(saved);
        this.zoneForm = null;
        this.pickedSite = '';
        if (existing) {
          this.showToast(`Geofence “${name}” updated.`);
        } else {
          this.selectedId = saved.id;
          this.listSite = '';
          this.listSearch = '';
          this.showToast(`Geofence “${name}” created at ${saved.site}.`);
        }
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, 'Could not save the geofence.');
      },
    });
  }

  /** Copies the zone (disabled) and its rules (as drafts), then opens the copy to rename and review before enabling. */
  duplicateZone(z: Zone): void {
    this.menuOpen = '';
    let name = `${z.name} (copy)`;
    for (let i = 2; this.zones.some((x) => x.site === z.site && x.name.toLowerCase() === name.toLowerCase()); i++) name = `${z.name} (copy ${i})`;
    const zoneRules = this.rules.filter((r) => r.zoneId === z.id);
    this.geofenceApi
      .create({ ...z, id: '', name, status: 'Disabled', statusReason: `Duplicated from “${z.name}” - review before enabling`, lastEventAt: null })
      .pipe(
        switchMap((copy) => {
          this.replaceZone(copy);
          this.selectedId = copy.id;
          const reqs = zoneRules.map(({ id: _id, ...r }) => this.ruleApi.create({ ...r, zoneId: copy.id, status: 'Draft', enabled: false }));
          return forkJoin({ copy: of(copy), rules: reqs.length ? forkJoin(reqs) : of([] as Rule[]) });
        }),
      )
      .subscribe({
        next: ({ copy, rules }) => {
          this.rules = [...this.rules, ...rules];
          this.showToast(`Copied “${z.name}” (disabled) with ${rules.length} draft rule${rules.length === 1 ? '' : 's'}. Review it, then enable it.`);
          this.openZone(copy);
        },
        error: (err) => this.fail(err, 'Could not duplicate the geofence.'),
      });
  }

  private patchZone(z: Zone, patch: Partial<Zone>, message: (saved: Zone) => string): void {
    this.geofenceApi.update({ ...z, ...patch }).subscribe({
      next: (saved) => {
        this.replaceZone(saved);
        this.showToast(message(saved));
      },
      error: (err) => this.fail(err, 'Could not update the geofence.'),
    });
  }

  /** Disabling asks why (recorded with who and when); history is kept, the zone's rules stop being evaluated. */
  toggleZone(z: Zone): void {
    this.menuOpen = '';
    const disabling = z.status === 'Active';
    this.confirmBox = {
      title: disabling ? 'Disable geofence' : 'Enable geofence',
      message: disabling
        ? `Stop monitoring “${z.name}”? Its ${this.ruleCount(z.id)} rule(s) stop raising alerts and pending dwell timers are dropped. Past events and alerts are kept.`
        : `Resume monitoring “${z.name}” and evaluating its rules?`,
      label: disabling ? 'Disable' : 'Enable',
      reason: { label: 'Reason', value: '', required: disabling },
      run: (reason) => this.patchZone(z, { status: disabling ? 'Disabled' : 'Active', statusReason: reason }, (s) => `“${s.name}” ${s.status === 'Active' ? 'enabled - monitoring resumed' : 'disabled - no alerts will be raised'}.`),
    };
  }

  deleteZone(z: Zone): void {
    this.menuOpen = '';
    const zoneRules = this.rules.filter((r) => r.zoneId === z.id);
    const n = zoneRules.length;
    this.confirmBox = {
      title: 'Delete geofence',
      message: `Delete “${z.name}” (${z.site}) and its ${n} alert rule${n === 1 ? '' : 's'}? Past alerts are kept in the history.`,
      label: 'Delete',
      run: () => {
        const deletes: Observable<unknown> = n ? forkJoin(zoneRules.map((r) => this.ruleApi.delete(r.id))) : of(null);
        deletes.pipe(switchMap(() => this.geofenceApi.delete(z.id))).subscribe({
          next: () => {
            this.zones = this.zones.filter((x) => x.id !== z.id);
            this.rules = this.rules.filter((r) => r.zoneId !== z.id);
            if (this.selectedId === z.id) this.selectedId = this.zones[0] ? this.zones[0].id : '';
            if (this.ruleZone === z.id) this.ruleZone = '';
            this.loadSummary();
            this.showToast(`Geofence “${z.name}” deleted.`);
          },
          error: (err) => {
            this.ruleApi.list().subscribe({ next: (rules) => (this.rules = rules), error: () => undefined });
            this.fail(err, 'Could not delete the geofence.');
          },
        });
      },
    };
  }

  toggleMonitor(z: Zone, which: 'entry' | 'exit'): void {
    const patch = which === 'entry' ? { monitorEntry: !z.monitorEntry } : { monitorExit: !z.monitorExit };
    this.patchZone(z, patch, (s) => `Asset ${which} detection ${(which === 'entry' ? s.monitorEntry : s.monitorExit) ? 'enabled' : 'disabled'} for “${s.name}”.`);
  }

  /** Indoor zones are drawn in Maps & Floor Plans; "disabling" one here pauses (or resumes) all of its rules. */
  toggleIndoorMonitoring(t: GeofenceTarget): void {
    this.menuOpen = '';
    const zoneRules = this.rules.filter((r) => r.zoneId === t.id);
    if (!zoneRules.length) { this.openRule(undefined, t.id); return; }
    const pausing = zoneRules.some((r) => r.status === 'Active');
    this.confirmBox = {
      title: pausing ? 'Pause zone monitoring' : 'Resume zone monitoring',
      message: pausing ? `Pause all ${zoneRules.length} rule(s) of “${t.name}”? Location events are still recorded; no alerts are raised.` : `Activate all ${zoneRules.length} rule(s) of “${t.name}”?`,
      label: pausing ? 'Pause' : 'Resume',
      run: () =>
        forkJoin(zoneRules.map((r) => this.ruleApi.update({ ...r, status: pausing ? 'Disabled' : 'Active', enabled: !pausing }))).subscribe({
          next: (saved) => {
            const byId = new Map(saved.map((r) => [r.id, r]));
            this.rules = this.rules.map((r) => byId.get(r.id) ?? r);
            this.loadSummary();
            this.showToast(`${saved.length} rule(s) of “${t.name}” ${pausing ? 'paused' : 'activated'}.`);
          },
          error: (err) => this.fail(err, 'Could not update the zone rules.'),
        }),
    };
  }

  // ===== rules =====
  private autoName(f: RuleForm): string {
    const who = f.assetIds.length === 1 ? f.assetIds[0] : f.group;
    if (f.trigger === 'dwell') return `${who} dwell over ${f.dwellMin} min`;
    const verb = TRIGGERS.find((t) => t.value === f.trigger)?.label.replace(/^Asset /, '').toLowerCase() ?? '';
    return `${who} ${verb}${f.condition === CONDITIONS[0] ? '' : ' — ' + f.condition.toLowerCase()}`;
  }

  /** A rule needs a trigger, scope, condition and at least one meaningful action. */
  private ruleProblem(f: RuleForm): string {
    if (RELAX_VALIDATION) return ''; // TEMP: relaxed validation (testing build)
    if (!f.trigger) return 'Choose a trigger.';
    if (!f.severity) return 'Choose a severity.';
    if (!f.group) return 'Choose an asset group.';
    if (!f.condition) return 'Choose a condition.';
    if (f.condition === CUSTOM_SCHEDULE && (!f.scheduleDays.length || !f.scheduleFrom || !f.scheduleTo)) return 'Choose the days and hours of the custom schedule.';
    if (f.trigger === 'dwell' && !(f.dwellMin >= 1)) return 'Enter the dwell time in minutes.';
    if (f.trigger === 'unauthorized' && !f.authorizedGroups.length) return 'Choose the asset groups that are allowed to enter.';
    if (!f.createAlert && !f.notify && !f.workflow) return 'Choose at least one action for the rule.';
    if (f.notify && !f.team.trim()) return 'Enter the team to notify.';
    if (f.workflow && !f.workflowName.trim()) return 'Enter the workflow to start.';
    if (f.escalateAfterMin > 0 && !f.escalateTo.trim()) return 'Enter who the alert is escalated to.';
    if (f.effectiveFrom && f.effectiveTo && f.effectiveTo < f.effectiveFrom) return 'The rule\'s end date is before its start date.';
    return '';
  }

  saveBuilder(): void {
    const target = this.selectedId;
    if (!target || this.saving) return;
    const problem = this.ruleProblem(this.builder);
    if (problem) { this.showToast(problem); return; }
    const { name, ...form } = this.builder;
    this.saving = true;
    this.ruleApi.create({ ...form, name: name.trim() || this.autoName(form), zoneId: target, enabled: form.status === 'Active' }).subscribe({
      next: (rule) => {
        this.saving = false;
        this.rules = [...this.rules, rule];
        this.loadSummary();
        this.showToast(`Rule “${rule.name}” saved to ${this.zoneName(target)} (${this.ruleCount(target)} rules).`);
      },
      error: (err) => this.fail(err, 'Could not save the alert rule.'),
    });
  }

  resetBuilder(): void {
    this.builder = { ...DEFAULT_RULE, name: '', scheduleDays: [...DEFAULT_RULE.scheduleDays] };
  }

  get rulesFiltered(): Rule[] {
    const q = this.ruleSearch.trim().toLowerCase();
    return this.rules.filter((r) => (!this.ruleZone || r.zoneId === this.ruleZone) && (!this.ruleStatus || r.status === this.ruleStatus)
      && (!q || `${r.id} ${r.name} ${r.description} ${this.zoneName(r.zoneId)} ${r.group} ${r.assetIds.join(' ')}`.toLowerCase().includes(q)));
  }

  get enabledRules(): number {
    return this.rules.filter((r) => r.status === 'Active').length;
  }

  zoneName(id: string): string {
    return this.zoneById(id)?.name ?? this.targetById(id)?.name ?? this.mapZoneRuleTargets.find((t) => t.id === id)?.label ?? this.rules.find((r) => r.zoneId === id)?.zoneLabel ?? '—';
  }

  /** Rule targets for the dropdowns: indoor zones and outdoor geofences. */
  get indoorTargets(): GeofenceTarget[] {
    return this.targets.filter((t) => t.kind === 'Indoor');
  }

  openRule(r?: Rule, zoneId?: string): void {
    this.menuOpen = '';
    this.formError = '';
    // New rule: the zone asked for, else the selected / viewed zone
    const scope = this.floorId ? this.floorScope : undefined;
    const floorZone = scope ? MAP_ZONE_PREFIX + scope : '';
    const target = zoneId || this.ruleZone || this.selectedId || floorZone || this.zones[0]?.id || '';
    this.ruleDialog = r ? { ...r, assetIds: [...r.assetIds], authorizedGroups: [...r.authorizedGroups], scheduleDays: [...r.scheduleDays] } : { ...DEFAULT_RULE, scheduleDays: [...DEFAULT_RULE.scheduleDays], id: '', name: '', zoneId: target, enabled: true };
    if (zoneId) this.tab = 'rules';
  }

  saveRuleDialog(): void {
    const d = this.ruleDialog;
    if (!d || this.saving) return;
    if (!RELAX_VALIDATION && (!d.zoneId)) { this.formError = 'Choose a geofence.'; return; }
    const problem = this.ruleProblem(d);
    if (problem) { this.formError = problem; return; }
    const name = d.name.trim() || this.autoName(d);
    const body = { ...d, name, enabled: d.status === 'Active' };
    this.saving = true;
    this.formError = '';
    const req = d.id ? this.ruleApi.update(body) : this.ruleApi.create(body);
    req.subscribe({
      next: (saved) => {
        this.saving = false;
        this.rules = d.id ? this.rules.map((r) => (r.id === saved.id ? saved : r)) : [...this.rules, saved];
        this.ruleDialog = null;
        this.loadSummary();
        this.showToast(d.id ? `Rule “${saved.name}” updated.` : `Rule “${saved.name}” added to ${this.zoneName(saved.zoneId)}.`);
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, 'Could not save the alert rule.');
      },
    });
  }

  toggleRule(r: Rule): void {
    const status: RuleStatus = r.status === 'Active' ? 'Disabled' : 'Active';
    this.ruleApi.update({ ...r, status, enabled: status === 'Active' }).subscribe({
      next: (saved) => {
        this.rules = this.rules.map((x) => (x.id === saved.id ? saved : x));
        this.loadSummary();
        this.showToast(`Rule “${saved.name}” ${saved.status === 'Active' ? 'activated' : 'disabled'}.`);
      },
      error: (err) => this.fail(err, 'Could not update the alert rule.'),
    });
  }

  deleteRule(r: Rule): void {
    this.confirmBox = {
      title: 'Delete alert rule',
      message: `Delete rule “${r.name}” on ${this.zoneName(r.zoneId)}? Alerts it raised are kept.`,
      label: 'Delete',
      run: () =>
        this.ruleApi.delete(r.id).subscribe({
          next: () => {
            this.rules = this.rules.filter((x) => x.id !== r.id);
            this.loadSummary();
            this.showToast(`Rule “${r.name}” deleted.`);
          },
          error: (err) => this.fail(err, 'Could not delete the alert rule.'),
        }),
    };
  }

  toggleList(list: string[], v: string): void {
    const i = list.indexOf(v);
    if (i >= 0) list.splice(i, 1);
    else list.push(v);
  }

  /** "AST-1, AST-2" ↔ assetIds. */
  assetsText(f: RuleForm): string {
    return f.assetIds.join(', ');
  }

  setAssetsText(f: RuleForm, text: string): void {
    f.assetIds = text.split(/[,\n]/).map((s) => s.trim()).filter(Boolean);
  }

  // ===== event history (alerts, server paged) =====
  private historyQuery(page: number, pageSize: number): GeofenceEventQuery {
    const t0 = startOfToday().getTime();
    const from = this.hDate === 'today' ? t0 : this.hDate === 'yesterday' ? t0 - 86400000 : this.hDate === '7d' ? t0 - 6 * 86400000 : this.hDate === '30d' ? t0 - 29 * 86400000 : null;
    const to = this.hDate === 'yesterday' ? t0 : null;
    return {
      page, pageSize,
      from: from === null ? undefined : new Date(from).toISOString(),
      to: to === null ? undefined : new Date(to).toISOString(),
      targetId: this.hZone || undefined,
      eventType: this.hType || undefined,
      status: this.hStatus || undefined,
      severity: this.hSeverity || undefined,
      search: this.hSearch.trim() || undefined,
    };
  }

  loadHistory(silent = false): void {
    if (this.histMode === 'movements') { this.loadMovements(); return; }
    this.hSub?.unsubscribe();
    if (!silent) this.hLoading = true;
    this.hSub = this.summaryApi.events(this.historyQuery(this.hPage, HIST_PAGE)).subscribe({
      next: (p) => {
        this.hRows = p.items;
        this.hTotal = p.total;
        this.hPages = p.totalPages;
        this.hPage = p.page;
        this.hTypes = p.eventTypes;
        this.hLoading = false;
      },
      error: (err) => {
        this.hLoading = false;
        if (!silent) this.showToast(apiErrorMessage(err, 'Could not load alert events.'));
      },
    });
  }

  /** Filter changed: back to page 1 (search waits for typing to pause). */
  hFilter(debounce = false): void {
    this.hPage = 1;
    clearTimeout(this.searchTimer);
    if (debounce) this.searchTimer = setTimeout(() => this.loadHistory(), 350);
    else this.loadHistory();
  }

  pageHist(d: number): void {
    const next = Math.min(Math.max(this.hPage + d, 1), this.hPages);
    if (next === this.hPage) return;
    this.hPage = next;
    this.loadHistory();
  }

  get hRange(): string {
    if (!this.hTotal) return '0 of 0';
    return `${(this.hPage - 1) * HIST_PAGE + 1}–${Math.min(this.hTotal, this.hPage * HIST_PAGE)} of ${this.hTotal}`;
  }

  get newInView(): AlertEvent[] {
    return this.hRows.filter((e) => e.status === 'New');
  }

  openHistory(date: 'today' | '7d' | '30d', status = '', keepZone = false): void {
    this.hDate = date;
    this.hStatus = status;
    if (!keepZone) this.hZone = '';
    this.hType = '';
    this.hSeverity = '';
    this.hSearch = '';
    this.histMode = 'alerts';
    this.tab = 'history';
    this.hFilter();
  }

  clearHistoryFilters(): void {
    this.openHistory('7d');
  }

  setTab(t: Tab): void {
    this.tab = t;
    if (t === 'history') this.loadHistory();
  }

  setHistMode(m: 'alerts' | 'movements'): void {
    this.histMode = m;
    this.loadHistory();
  }

  // ----- zone movements: location events (every entry / exit), which are not alerts -----
  loadMovements(): void {
    this.hLoading = true;
    this.rtlApi.events(this.mRange, '', this.mPage, HIST_PAGE, '', this.mType).subscribe({
      next: (p) => {
        this.mRows = p.items;
        this.mTotal = p.total;
        this.mPages = p.totalPages;
        this.mPage = p.page;
        this.mTypes = p.eventTypes;
        this.hLoading = false;
      },
      error: (err) => {
        this.hLoading = false;
        this.showToast(apiErrorMessage(err, 'Could not load zone movements.'));
      },
    });
  }

  pageMoves(d: number): void {
    const next = Math.min(Math.max(this.mPage + d, 1), this.mPages);
    if (next === this.mPage) return;
    this.mPage = next;
    this.loadMovements();
  }

  // ----- alert lifecycle: New → Acknowledged → Assigned → Investigating → Resolved → Closed -----
  private afterStatus(): void {
    this.loadSummary();
    this.loadRecent();
    if (this.tab === 'history') this.loadHistory(true);
  }

  private setStatus(list: AlertEvent[], status: EventStatus, done: (n: number) => string, assignee?: string, note?: string): void {
    this.menuOpen = '';
    if (!list.length) return;
    forkJoin(list.map((e) => this.alertApi.setStatus(e.id, statusToApi(status), assignee, note))).subscribe({
      next: (rows) => {
        if (this.detail && list.some((e) => e.id === this.detail!.id)) this.detail = null;
        this.afterStatus();
        this.showToast(done(rows.length));
      },
      error: (err) => {
        this.afterStatus();
        this.fail(err, 'Could not update the alert status.');
      },
    });
  }

  acknowledge(e: AlertEvent): void {
    this.setStatus([e], 'Acknowledged', () => `${e.id} acknowledged.`);
  }

  acknowledgeAll(): void {
    this.setStatus(this.newInView, 'Acknowledged', (n) => `${n} alert${n === 1 ? '' : 's'} acknowledged.`);
  }

  investigate(e: AlertEvent): void {
    this.setStatus([e], 'Investigating', () => `${e.id} marked as investigating.`);
  }

  reopen(e: AlertEvent): void {
    this.setStatus([e], 'New', () => `${e.id} reopened.`);
  }

  /** Assign / Resolve / Close ask for an assignee or a note. */
  openStatus(e: AlertEvent, status: 'Assigned' | 'Resolved' | 'Closed'): void {
    this.menuOpen = '';
    this.formError = '';
    const title = status === 'Assigned' ? `Assign ${e.id}` : status === 'Resolved' ? `Resolve ${e.id}` : `Close ${e.id}`;
    this.statusDialog = { event: e, status, title, assignee: e.assignedTo ?? '', note: '', needsAssignee: status === 'Assigned' };
  }

  saveStatus(): void {
    const d = this.statusDialog;
    if (!d) return;
    if (!RELAX_VALIDATION && (d.needsAssignee && !d.assignee.trim())) { this.formError = 'Choose who the alert is assigned to.'; return; }
    if (!RELAX_VALIDATION && (d.status === 'Resolved' && !d.note.trim())) { this.formError = 'Describe how the alert was resolved.'; return; }
    this.statusDialog = null;
    const verb = d.status === 'Assigned' ? `assigned to ${d.assignee.trim()}` : d.status.toLowerCase();
    this.setStatus([d.event], d.status, () => `${d.event.id} ${verb}.`, d.assignee.trim() || undefined, d.note.trim() || undefined);
  }

  viewZone(e: AlertEvent): void {
    this.menuOpen = '';
    this.detail = null;
    const t = this.targetById(e.zoneId);
    if (!t) { this.showToast('This alert is not linked to a geofence.'); return; }
    this.tab = 'geofences';
    this.selectTarget(t, true);
  }

  openDetail(e: AlertEvent): void {
    this.menuOpen = '';
    this.detail = e;
  }

  isOpen(e: AlertEvent): boolean {
    return OPEN_STATUSES.includes(e.status);
  }

  /** Every page of the current filter (up to 2,000 rows). */
  exportHistory(): void {
    const size = 200;
    const pages = Math.min(10, Math.max(1, Math.ceil(this.hTotal / size)));
    forkJoin(Array.from({ length: pages }, (_, i) => this.summaryApi.events(this.historyQuery(i + 1, size)))).subscribe({
      next: (res) => {
        const rows = res.flatMap((p) => p.items).map((e) => ({
          'Alert ID': e.id, Timestamp: e.ts.toLocaleString('en-GB'), Asset: e.assetCode, 'Asset Name': e.assetSub, Geofence: e.zoneName, Rule: e.ruleName ?? '',
          Event: e.type, Severity: e.severity, Duration: this.fmtDuration(e.durationSec), Description: e.message, Owner: e.owner, Status: e.status,
          Escalated: e.escalatedTo ?? '', History: (e.history ?? []).map((h) => `${this.fmtStamp(h.at)} ${h.action} (${h.by})${h.note ? ': ' + h.note : ''}`).join(' | '),
        }));
        const book = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Alert Events');
        XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.targets.map((t) => ({
          ID: this.shortId(t.id), Geofence: t.name, Kind: t.kind, Type: t.type ?? '', Category: t.category ?? '', Site: t.siteName ?? '', 'Building / Floor': this.targetPlace(t),
          Shape: this.targetShape(t), Area: this.targetArea(t), 'Monitored Assets': t.monitoredAssets, Rules: t.rules, 'Active Alerts': t.activeAlerts, Status: t.status,
          'Last Event': t.lastEventAt ? new Date(t.lastEventAt).toLocaleString('en-GB') : '',
        }))), 'Geofences');
        XLSX.writeFile(book, `Geofence_Alerts_${new Date().toISOString().slice(0, 10)}.xlsx`);
        this.showToast(`Exported ${rows.length} alert events to Excel.`);
      },
      error: (err) => this.fail(err, 'Could not export the alert events.'),
    });
  }

  // ===== shared =====
  runConfirm(): void {
    const c = this.confirmBox;
    if (!c) return;
    if (c.reason?.required && !c.reason.value.trim()) { this.formError = `Enter the ${c.reason.label.toLowerCase()}.`; return; }
    this.confirmBox = null;
    this.formError = '';
    c.run(c.reason?.value.trim() ?? '');
  }

  toggleMenu(name: string, e: Event): void {
    e.stopPropagation();
    this.layersOpen = false;
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.layersOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.mapMode === 'draw') { this.cancelDraw(); return; }
    if (this.mapMode === 'edit') { this.cancelShape(); return; }
    this.menuOpen = '';
    this.layersOpen = false;
    this.zoneForm = null;
    this.ruleDialog = null;
    this.confirmBox = null;
    this.statusDialog = null;
    this.detail = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}
