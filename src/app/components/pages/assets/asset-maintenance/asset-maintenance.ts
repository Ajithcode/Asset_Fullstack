import { Component, DestroyRef, HostListener, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  Building2,
  CalendarCheck,
  CircleCheck,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Clock,
  Coins,
  Download,
  Ellipsis,
  EllipsisVertical,
  ExternalLink,
  FileText,
  Filter,
  Folder,
  Info,
  MessageSquarePlus,
  Package,
  MapPin,
  Plus,
  Search,
  Settings,
  Star,
  Wrench,
  X,
} from 'lucide-angular';
import DATA from './maintenance.json';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetRecord, TODAY, assetLocation, findAsset, toIsoDate } from '../asset-data';
import { MaintenanceTask, loadAssetDetail } from '../asset-details/asset-details';
import { WoDetail, buildWoDetail } from './maintenance-history';
import { PmSchedule, schedulesFromPlan } from './maintenance-pm';
import { PmSchedules, PmWorkOrderRequest } from './pm-schedules/pm-schedules';
import { ServiceRequest, SrKpis, srKpis } from './maintenance-sr';
import { ServiceRequests, SrWorkOrderRequest } from './service-requests/service-requests';
import { PartReservation, PartUsage, SparePart, isStocked } from './maintenance-parts';
import { SpareParts } from './spare-parts/spare-parts';

type Tab = 'overview' | 'workOrders' | 'pm' | 'predictive' | 'requests' | 'history' | 'costs' | 'parts' | 'documents';
type WoType = 'Preventive' | 'Corrective' | 'Inspection' | 'Service';
type WoStatus = 'Completed' | 'In Progress' | 'Scheduled' | 'Open';

interface WorkOrder {
  id: string;
  date: string;
  type: WoType;
  description: string;
  labor: number;
  parts: number;
  service: number;
  downtimeMin: number;
  reason: string;
  status: WoStatus;
  technician: string;
  /** Set on open work orders. */
  priority?: 'High' | 'Medium' | 'Low';
  rootCause?: string;
  resolution?: string;
  nextAction?: string;
}

/** Overview status of a work order: open ones past their date are overdue. */
type WoState = 'Completed' | 'In Progress' | 'Overdue' | 'Scheduled';
type ActivityKind = 'done' | 'request' | 'pm' | 'part' | 'created';

interface Activity {
  date: string;
  text: string;
  kind: ActivityKind;
}

interface MaintenanceData {
  year: number;
  lastYear: { cost: number; downtimeMin: number };
  lastMonthOpen: number;
  workOrders: WorkOrder[];
  serviceRequests: ServiceRequest[];
  /** Last month's service-request figures, for the header comparison. */
  srLastMonth?: { open: number; responseHours: number; resolutionDays: number; rating: number };
  /** Parts catalogue for this asset (stock, reorder levels, BOM). */
  spareParts: SparePart[];
  /** Parts issued to work orders; each work order's lines add up to its parts cost. */
  partsUsage: PartUsage[];
  partsReserved: PartReservation[];
  partsLastMonth?: { critical: number; lowStock: number };
  sensors: { name: string; value: number; unit: string; warn: number; alarm: number; trend: string; low?: boolean }[];
  overview: { serialNumber: string; warrantyYears: number; amcVendor: string };
  /** Changes made on this page during the session (newest first). */
  activity: Activity[];
  /** Preventive maintenance schedules; assets without a list start from their health plan. */
  pm?: PmSchedule[];
  pmLastYear?: number;
  pmTemplates?: { code: string; name: string }[];
}

interface Segment {
  key: string;
  label: string;
  color: string;
}

interface StackBar {
  month: string;
  x: number;
  rects: { y: number; h: number; color: string; top: boolean; d: string }[];
  total: number;
  parts: { label: string; value: number; color: string }[];
}

interface WoForm {
  type: WoType;
  description: string;
  date: string;
  technician: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** Categorical palette (validated: lightness, chroma, CVD separation). Text never wears these colours. */
export const COST_SEGMENTS: Segment[] = [
  { key: 'labor', label: 'Labor', color: '#2563eb' },
  { key: 'parts', label: 'Parts', color: '#16a34a' },
  { key: 'service', label: 'External Service', color: '#eaa200' },
  { key: 'others', label: 'Others', color: '#7c3aed' },
];
const TYPE_COLORS: Record<WoType, string> = { Preventive: '#16a34a', Corrective: '#2563eb', Inspection: '#eaa200', Service: '#7c3aed' };
const REASON_COLORS: Record<string, string> = { Overheating: '#dc2626', 'Scheduled Maintenance': '#2563eb', 'Electrical Fault': '#eaa200', Inspection: '#7c3aed', Others: '#9ca3af' };
export const STATE_COLORS: Record<WoState, string> = { Completed: '#16a34a', 'In Progress': '#2563eb', Overdue: '#dc2626', Scheduled: '#8b5cf6' };
const STATES: WoState[] = ['Completed', 'In Progress', 'Overdue', 'Scheduled'];
const AMC_VENDORS: Record<string, string> = { 'Power Generation': 'ABC Services LLC', HVAC: 'CoolTech Facilities LLC', Electrical: 'Volt Power Services', 'IT Equipment': 'NetCare Solutions', 'Safety & Security': 'SecureGuard Systems', 'Material Handling': 'LiftPro Services' };
const REASON_ORDER = ['Overheating', 'Scheduled Maintenance', 'Electrical Fault', 'Inspection', 'Others'];
const SOURCE = DATA as unknown as MaintenanceData;
const NO_SCHEDULES: PmSchedule[] = [];
const CURRENT_USER = 'Rex Kumar';

// Chart geometry (SVG user units)
const CW = 440;
const CH = 150;
const PAD = { l: 34, r: 8, t: 8, b: 22 };

/**
 * DEMO MODE: GEN-00045 has its recorded maintenance; other assets get the same pattern scaled from their tag,
 * so every asset opens with consistent figures. Kept in memory per asset.
 */
const STORE = new Map<string, MaintenanceData>();
function dataFor(a: AssetRecord): MaintenanceData {
  let d = STORE.get(a.tag);
  if (d) return d;
  d = structuredClone(SOURCE);
  d.activity = [];
  if (a.tag !== 'GEN-00045') {
    let seed = 0;
    for (const ch of a.tag) seed = (seed * 31 + ch.charCodeAt(0)) >>> 0;
    const f = 0.45 + (seed % 100) / 90;
    const r10 = (n: number) => Math.round((n * f) / 10) * 10;
    const prefix = a.tag.split('-')[0];
    const ids = new Map<string, string>();
    d.workOrders = d.workOrders.map((w, i) => ({
      ...w, id: `WO-2026-${String(100 + ((seed + i * 7) % 800)).padStart(3, '0')}-${prefix}`, labor: r10(w.labor), parts: r10(w.parts), service: r10(w.service),
      downtimeMin: Math.round((w.downtimeMin * f) / 15) * 15,
    }));
    SOURCE.workOrders.forEach((w, i) => ids.set(w.id, d!.workOrders[i].id));
    // keep service requests pointing at this asset's work orders
    d.serviceRequests = d.serviceRequests.map((sr) => ({ ...sr, workOrder: ids.get(sr.workOrder) ?? '' }));
    d.lastYear = { cost: r10(d.lastYear.cost), downtimeMin: Math.round(d.lastYear.downtimeMin * f) };
    d.sensors = [];
    // other assets build their PM list from their own maintenance plan
    delete d.pm;
    d.pmLastYear = 0;
    d.overview = { serialNumber: a.serialNumber ?? `SN-${2020 + (seed % 5)}-${String(seed % 100000).padStart(5, '0')}`, warrantyYears: 5, amcVendor: AMC_VENDORS[a.category] ?? 'ABC Services LLC' };
    // parts lines follow this asset's work orders and their scaled parts cost
    const partsOf = new Map(d.workOrders.map((w) => [w.id, w.parts]));
    d.partsUsage = d.partsUsage.map((u) => ({ ...u, wo: ids.get(u.wo) ?? u.wo, unitCost: Math.max(1, Math.round(u.unitCost * f)) }));
    for (const [wo, target] of partsOf) {
      const lines = d.partsUsage.filter((u) => u.wo === wo);
      if (!lines.length) continue;
      const diff = target - lines.reduce((t, u) => t + u.qty * u.unitCost, 0);
      const sundries = lines.find((u) => u.partNo === 'SP-00001');
      if (sundries && sundries.unitCost + diff >= 0) sundries.unitCost += diff;
      else if (diff > 0) d.partsUsage.push({ wo, date: lines[0].date, partNo: 'SP-00001', qty: 1, unitCost: diff });
    }
  }
  STORE.set(a.tag, d);
  return d;
}

@Component({
  selector: 'app-asset-maintenance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, PmSchedules, ServiceRequests, SpareParts],
  templateUrl: './asset-maintenance.html',
  styleUrls: ['./asset-maintenance.css', './asset-maintenance-charts.css', './asset-maintenance-overview.css', './asset-maintenance-history.css'],
})
export class AssetMaintenance implements OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, Building2, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, Clock, Coins, Download, Ellipsis, ExternalLink, FileText, Folder, Info, MapPin, Plus,
    Search, Settings, Wrench, X, CalendarCheck, CircleCheck, MessageSquarePlus, Package, EllipsisVertical, Filter, Star,
  };
  readonly tabs: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'workOrders', label: 'Work Orders' },
    { key: 'pm', label: 'Preventive Maintenance' },
    { key: 'predictive', label: 'Predictive Maintenance' },
    { key: 'requests', label: 'Service Requests' },
    { key: 'history', label: 'Maintenance History' },
    { key: 'costs', label: 'Costs & Downtime' },
    { key: 'parts', label: 'Spare Parts' },
    { key: 'documents', label: 'Documents' },
  ];
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly segments = COST_SEGMENTS;
  readonly types: WoType[] = ['Preventive', 'Corrective', 'Inspection', 'Service'];
  readonly statuses: WoStatus[] = ['Completed', 'In Progress', 'Scheduled', 'Open'];
  readonly typeColors = TYPE_COLORS;
  readonly reasonColors = REASON_COLORS;
  readonly cw = CW;
  readonly ch = CH;
  readonly pad = PAD;
  readonly pageSizes = [10, 25, 50];
  readonly Math = Math;

  asset: AssetRecord = findAsset('GEN-00045') ?? ASSET_RECORDS[0];
  data: MaintenanceData = dataFor(this.asset);
  health: number | null = null;
  plan: MaintenanceTask[] = [];
  tab: Tab = 'overview';
  /** Commissioning date from the asset's lifecycle record (Installation Date). */
  commissioned = '';
  costYearRange: 'year' | 'h2' = 'year';
  hoverTrend: number | null = null;
  hoverCost: number | null = null;
  hoverState: WoState | null = null;
  readonly stateColors = STATE_COLORS;
  /** Narrower chart width for the small cost card, so its labels stay readable. */
  readonly cw2 = 300;

  // Period: whole year by default; the date range narrows everything on the Costs tab
  period: 'year' | 'quarter' | 'h1' | 'custom' = 'year';
  fromDate = `${SOURCE.year}-01-01`;
  toDate = `${SOURCE.year}-12-31`;
  rangeOpen = false;
  costChartRange: 'year' | 'h2' = 'year';
  downChartRange: 'year' | 'h2' = 'year';

  // Details table
  typeFilter: WoType | '' = '';
  statusFilter: WoStatus | '' = '';
  timeFilter: '' | '30' | '90' = '';
  search = '';
  readonly checked = new Set<string>();
  pageSize = 10;
  page = 0;

  // Hover layers
  hoverBar: StackBar | null = null;
  hoverMonth: number | null = null;
  hoverType: WoType | null = null;

  menuOpen = '';
  woForm: WoForm | null = null;
  woError = '';
  viewWo: WorkOrder | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    // ?tag=GEN-00045 opens an asset (the asset detail tabs pass it)
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((p) => {
        this.setAsset(findAsset(p.get('tag')) ?? this.asset);
        // ?tab=costs opens a section directly
        const tab = p.get('tab') as Tab | null;
        if (tab && this.tabs.some((t) => t.key === tab)) this.tab = tab;
      });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  private setAsset(a: AssetRecord): void {
    this.asset = a;
    this.data = dataFor(a);
    this.health = null;
    this.plan = [];
    this.commissioned = '';
    this.checked.clear();
    this.histChecked.clear();
    this.selectedWoId = null;
    this.histPage = 0;
    this.page = 0;
    loadAssetDetail(a.tag).then((d) => {
      if (this.asset.tag !== a.tag || !d) return;
      this.health = d.health.score;
      this.plan = d.health.maintenancePlan;
      this.commissioned = d.status?.lifecycle?.commissionedOn ?? '';
    });
  }

  get location(): string {
    return assetLocation(this.asset);
  }

  // ===== Health gauge =====

  get healthBand(): string {
    const s = this.health ?? 0;
    return s >= 70 ? 'Excellent' : s >= 50 ? 'Good' : s >= 30 ? 'Fair' : 'Poor';
  }

  /** Gauge arc: 270° sweep, filled to the score. */
  gaugeDash(score: number): string {
    const c = 2 * Math.PI * 34;
    const sweep = c * 0.75;
    return `${(sweep * Math.max(0, Math.min(100, score))) / 100} ${c}`;
  }

  get gaugeTrack(): string {
    const c = 2 * Math.PI * 34;
    return `${c * 0.75} ${c}`;
  }

  // ===== Work orders & period =====

  cost(w: WorkOrder): number {
    return w.labor + w.parts + w.service;
  }

  /** Completed work orders inside the selected period (the basis for every cost and downtime figure). */
  get periodOrders(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status === 'Completed' && w.date >= this.fromDate && w.date <= this.toDate);
  }

  setPeriod(p: 'year' | 'quarter' | 'h1' | 'custom'): void {
    this.period = p;
    const y = this.data.year;
    if (p === 'year') [this.fromDate, this.toDate] = [`${y}-01-01`, `${y}-12-31`];
    if (p === 'h1') [this.fromDate, this.toDate] = [`${y}-01-01`, `${y}-06-30`];
    if (p === 'quarter') [this.fromDate, this.toDate] = [`${y}-07-01`, `${y}-09-30`];
    if (p === 'custom') this.rangeOpen = true;
    this.page = 0;
  }

  onRange(): void {
    this.period = 'custom';
    if (this.toDate < this.fromDate) this.toDate = this.fromDate;
    this.page = 0;
  }

  get rangeLabel(): string {
    const f = (s: string) => new Date(s + 'T00:00').toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    return `${f(this.fromDate)} - ${f(this.toDate)}`;
  }

  get openOrders(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status !== 'Completed');
  }

  get openDelta(): number {
    return this.openOrders.length - this.data.lastMonthOpen;
  }

  get totals(): { labor: number; parts: number; service: number; total: number; downtime: number; jobs: number } {
    const o = this.periodOrders;
    const labor = o.reduce((t, w) => t + w.labor, 0);
    const parts = o.reduce((t, w) => t + w.parts, 0);
    const service = o.reduce((t, w) => t + w.service, 0);
    return { labor, parts, service, total: labor + parts + service, downtime: o.reduce((t, w) => t + w.downtimeMin, 0), jobs: o.length };
  }

  share(n: number): number {
    const t = this.totals.total;
    return t ? Math.round((n / t) * 100) : 0;
  }

  /** Change vs last year, as a whole percentage (negative = lower). */
  vsLastYear(now: number, last: number): number {
    return last ? Math.round(((now - last) / last) * 100) : 0;
  }

  get costChange(): number {
    return this.vsLastYear(this.totals.total, this.data.lastYear.cost);
  }

  get downtimeChange(): number {
    return this.vsLastYear(this.totals.downtime, this.data.lastYear.downtimeMin);
  }

  fmtHours(min: number): string {
    const h = min / 60;
    return `${Number.isInteger(h) ? h : h.toFixed(1)} Hours`;
  }

  fmtDuration(min: number): string {
    if (!min) return '—';
    const h = Math.floor(min / 60);
    const m = min % 60;
    return h ? `${h}h${m ? ' ' + m + 'm' : ''}` : `${m}m`;
  }

  /** Next preventive task from the asset's maintenance plan. */
  /** The asset's PM schedules (shared with the Preventive Maintenance tab, which edits them). */
  get pmSchedules(): PmSchedule[] {
    if (!this.data.pm) {
      if (!this.plan.length) return NO_SCHEDULES;
      this.data.pm = schedulesFromPlan(this.plan, this.asset.manufacturer);
    }
    return this.data.pm;
  }

  /** Earliest upcoming PM (overdue ones show on the Preventive Maintenance tab). */
  get nextPm(): { date: string; task: string; days: number } | null {
    const today = toIsoDate(TODAY);
    const due = this.pmSchedules.filter((p) => p.nextDue >= today).sort((a, b) => a.nextDue.localeCompare(b.nextDue))[0];
    if (!due) return null;
    const days = Math.round((new Date(due.nextDue + 'T00:00').getTime() - TODAY.getTime()) / 86_400_000);
    return { date: due.nextDue, task: due.name, days };
  }

  // ===== Service requests =====

  @ViewChild(ServiceRequests) private srTab?: ServiceRequests;
  /** Service request waiting for the work order being created from it. */
  private pendingRequest: string | null = null;

  get srKpis(): SrKpis {
    return srKpis(this.data.serviceRequests, this.data.srLastMonth);
  }

  /** Header button: the Service Requests tab creates requests, every other tab work orders. */
  headerCreate(): void {
    if (this.tab === 'requests' && this.srTab) this.srTab.openForm();
    else this.openWorkOrder();
  }

  openSrWorkOrder(req: SrWorkOrderRequest): void {
    this.woError = '';
    this.woForm = { type: 'Corrective', description: req.description, date: toIsoDate(TODAY), technician: req.technician };
    this.pendingRequest = req.requestId;
  }

  showWorkOrder(id: string): void {
    this.viewWo = this.data.workOrders.find((w) => w.id === id) ?? null;
    if (!this.viewWo) this.showToast(`${id} is not in this asset's work orders.`);
  }

  openPmWorkOrder(req: PmWorkOrderRequest): void {
    this.pendingRequest = null;
    this.woError = '';
    this.woForm = { type: 'Preventive', description: req.description, date: req.date, technician: req.technician };
  }

  // ===== Cost trend (stacked bars) =====

  private monthsFor(range: 'year' | 'h2'): number[] {
    return range === 'year' ? MONTHS.map((_, i) => i) : [6, 7, 8, 9, 10, 11];
  }

  /** Round the axis up to a whole step so ticks are 0, 2K, 4K … */
  private niceMax(v: number, step: number): number {
    return Math.max(step, Math.ceil(v / step) * step);
  }

  get costBars(): { bars: StackBar[]; ticks: { y: number; label: string }[]; barW: number } {
    const months = this.monthsFor(this.costChartRange);
    const orders = this.data.workOrders.filter((w) => w.status === 'Completed' && w.date.startsWith(String(this.data.year)));
    const sums = months.map((m) => {
      const list = orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m);
      return { labor: list.reduce((t, w) => t + w.labor, 0), parts: list.reduce((t, w) => t + w.parts, 0), service: list.reduce((t, w) => t + w.service, 0), others: 0 };
    });
    const max = this.niceMax(Math.max(...sums.map((s) => s.labor + s.parts + s.service + s.others)), 1000);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / months.length;
    const barW = Math.min(16, slot * 0.55);
    const bars: StackBar[] = months.map((m, i) => {
      const s = sums[i] as Record<string, number>;
      const x = PAD.l + slot * i + (slot - barW) / 2;
      let y = CH - PAD.b;
      const present = COST_SEGMENTS.filter((seg) => s[seg.key] > 0);
      const rects = present.map((seg, k) => {
        const h = Math.max(1, (s[seg.key] / max) * plotH);
        // 2px surface gap between stacked segments
        const gap = k ? 2 : 0;
        y -= h;
        const top = k === present.length - 1;
        const rh = h - gap;
        const r = top ? Math.min(4, rh) : 0;
        const d = `M${x},${y + rh}V${y + r}${r ? `Q${x},${y} ${x + r},${y}` : ''}H${x + barW - r}${r ? `Q${x + barW},${y} ${x + barW},${y + r}` : ''}V${y + rh}Z`;
        return { y, h: rh, color: seg.color, top, d };
      });
      return {
        month: MONTHS[m], x, rects, total: s['labor'] + s['parts'] + s['service'] + s['others'],
        parts: COST_SEGMENTS.map((seg) => ({ label: seg.label, value: s[seg.key], color: seg.color })),
      };
    });
    const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => ({ y: CH - PAD.b - f * plotH, label: f ? `${Math.round((max * f) / 100) / 10}K` : '0' }));
    return { bars, ticks, barW };
  }

  // ===== Downtime trend (line) =====

  get downLine(): { points: { x: number; y: number; month: string; hours: number }[]; path: string; area: string; ticks: { y: number; label: string }[] } {
    const months = this.monthsFor(this.downChartRange);
    const orders = this.data.workOrders.filter((w) => w.status === 'Completed' && w.date.startsWith(String(this.data.year)));
    const hours = months.map((m) => orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m).reduce((t, w) => t + w.downtimeMin, 0) / 60);
    const max = this.niceMax(Math.max(...hours), 1);
    const plotH = CH - PAD.t - PAD.b;
    const step = (CW - PAD.l - PAD.r - 16) / Math.max(1, months.length - 1);
    const points = months.map((m, i) => ({ x: PAD.l + 8 + step * i, y: CH - PAD.b - (hours[i] / max) * plotH, month: MONTHS[m], hours: hours[i] }));
    const path = points.map((p, i) => `${i ? 'L' : 'M'}${p.x},${p.y}`).join('');
    const area = `${path}L${points[points.length - 1].x},${CH - PAD.b}L${points[0].x},${CH - PAD.b}Z`;
    const ticks = Array.from({ length: max + 1 }, (_, i) => ({ y: CH - PAD.b - (i / max) * plotH, label: String(i) })).filter((_, i, a) => a.length <= 6 || i % 2 === 0);
    return { points, path, area, ticks };
  }

  // ===== Cost by type (donut) =====

  get byType(): { type: WoType; value: number; pct: number; color: string; dash: string; offset: number }[] {
    const total = this.totals.total || 1;
    const c = 2 * Math.PI * 56;
    let acc = 0;
    return this.types.map((type) => {
      const value = this.periodOrders.filter((w) => w.type === type).reduce((t, w) => t + this.cost(w), 0);
      const len = (value / total) * c;
      // 2px surface gap between segments
      const seg = { type, value, pct: Math.round((value / total) * 100), color: TYPE_COLORS[type], dash: `${Math.max(0, len - 2)} ${c}`, offset: -acc };
      acc += len;
      return seg;
    });
  }

  // ===== Downtime by reason & breakdown =====

  get byReason(): { reason: string; min: number; pct: number; width: number; color: string }[] {
    const total = this.totals.downtime || 1;
    const rows = REASON_ORDER.map((reason) => ({ reason, min: this.periodOrders.filter((w) => w.reason === reason).reduce((t, w) => t + w.downtimeMin, 0) }));
    const max = Math.max(1, ...rows.map((r) => r.min));
    return rows.map((r) => ({ ...r, pct: Math.round((r.min / total) * 100), width: (r.min / max) * 100, color: REASON_COLORS[r.reason] }));
  }

  get breakdown(): { label: string; value: number; pct: number; jobs: number }[] {
    const o = this.periodOrders;
    const t = this.totals;
    return [
      { label: 'Labor', value: t.labor, pct: this.share(t.labor), jobs: o.filter((w) => w.labor > 0).length },
      { label: 'Spare Parts', value: t.parts, pct: this.share(t.parts), jobs: o.filter((w) => w.parts > 0).length },
      { label: 'External Service', value: t.service, pct: this.share(t.service), jobs: o.filter((w) => w.service > 0).length },
    ];
  }

  // ===== Details table =====

  get detailRows(): WorkOrder[] {
    const q = this.search.trim().toLowerCase();
    const since = this.timeFilter ? toIsoDate(new Date(TODAY.getTime() - Number(this.timeFilter) * 86_400_000)) : '';
    return this.data.workOrders
      // only jobs that took the asset out of service
      .filter((w) => w.downtimeMin > 0 && w.date >= this.fromDate && w.date <= this.toDate)
      .filter((w) => (!this.typeFilter || w.type === this.typeFilter) && (!this.statusFilter || w.status === this.statusFilter) && (!since || w.date >= since))
      .filter((w) => !q || `${w.id} ${w.description} ${w.type} ${w.technician}`.toLowerCase().includes(q))
      .sort((a, b) => b.date.localeCompare(a.date));
  }

  get pageRows(): WorkOrder[] {
    return this.detailRows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.detailRows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showingLabel(): string {
    const n = this.detailRows.length;
    return n ? `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records` : 'No records';
  }

  onFilter(): void {
    this.page = 0;
  }

  toggleCheck(id: string, on: boolean): void {
    if (on) this.checked.add(id);
    else this.checked.delete(id);
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((w) => this.checked.has(w.id));
  }

  toggleAll(on: boolean): void {
    for (const w of this.pageRows) this.toggleCheck(w.id, on);
  }

  typeClass(t: WoType): string {
    return 'tp--' + t.toLowerCase();
  }

  statusClass(s: WoStatus): string {
    return s === 'Completed' ? 'st--done' : s === 'In Progress' ? 'st--progress' : 'st--open';
  }

  // ===== Overview tab =====

  fmtDate(iso: string): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '—';
  }

  private addYears(iso: string, years: number): string {
    if (!iso) return '';
    const d = new Date(iso + 'T00:00');
    d.setFullYear(d.getFullYear() + years);
    return toIsoDate(d);
  }

  get overviewRows(): { label: string; value: string; crit?: boolean }[] {
    const a = this.asset;
    const o = this.data.overview;
    const installed = this.commissioned || a.purchaseDate || '';
    return [
      { label: 'Asset Tag', value: a.tag },
      { label: 'Asset Name', value: a.name },
      { label: 'Location', value: this.location },
      { label: 'Department', value: `${a.department} Department` },
      { label: 'Asset Type', value: a.type },
      { label: 'Manufacturer', value: a.manufacturer },
      { label: 'Model', value: a.model },
      { label: 'Serial Number', value: a.serialNumber ?? o.serialNumber },
      { label: 'Installation Date', value: this.fmtDate(installed) },
      { label: 'Warranty Expiry', value: this.fmtDate(this.addYears(installed, o.warrantyYears)) },
      { label: 'AMC Vendor', value: o.amcVendor },
      { label: 'Criticality', value: a.criticality, crit: true },
    ];
  }

  woState(w: WorkOrder): WoState {
    if (w.status === 'Completed') return 'Completed';
    if (w.status === 'In Progress') return 'In Progress';
    return w.date < toIsoDate(TODAY) ? 'Overdue' : 'Scheduled';
  }

  stateClass(s: WoState): string {
    return s === 'Completed' ? 'st--done' : s === 'In Progress' ? 'st--progress' : s === 'Overdue' ? 'st--overdue' : 'st--open';
  }

  private get yearOrders(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.date.startsWith(String(this.data.year)));
  }

  /** Donut of this year's work orders by state; 2px surface gaps between segments. */
  get statusSplit(): { total: number; rows: { state: WoState; count: number; pct: number; color: string; dash: string; offset: number }[] } {
    const orders = this.yearOrders;
    const total = orders.length;
    const C = 2 * Math.PI * 52;
    let acc = 0;
    const rows = STATES.map((state) => {
      const count = orders.filter((w) => this.woState(w) === state).length;
      const len = total ? (count / total) * C : 0;
      const row = { state, count, pct: total ? Math.round((count / total) * 100) : 0, color: STATE_COLORS[state], dash: `${Math.max(0, len - 2)} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  /** Count shown in the donut centre while a status is hovered. */
  get hoverCount(): number | null {
    return this.hoverState ? (this.statusSplit.rows.find((r) => r.state === this.hoverState)?.count ?? 0) : null;
  }

  /** Stacked monthly work-order counts by state. */
  get trendBars(): { bars: { month: string; x: number; rects: { d: string; color: string }[]; counts: { state: WoState; count: number; color: string }[]; total: number }[]; ticks: { y: number; label: string }[]; barW: number } {
    const orders = this.yearOrders;
    const counts = MONTHS.map((_, m) => STATES.map((state) => ({ state, count: orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m && this.woState(w) === state).length, color: STATE_COLORS[state] })));
    const max = this.niceMax(Math.max(...counts.map((c) => c.reduce((t, x) => t + x.count, 0))), 4);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / 12;
    const barW = Math.min(16, slot * 0.55);
    const bars = counts.map((c, m) => {
      const x = PAD.l + slot * m + (slot - barW) / 2;
      const present = c.filter((v) => v.count > 0);
      let y = CH - PAD.b;
      const rects = present.map((v, k) => {
        const h = (v.count / max) * plotH;
        y -= h;
        const rh = h - (k ? 2 : 0);
        const top = k === present.length - 1;
        const rr = top ? Math.min(4, rh) : 0;
        const d = `M${x},${y + rh}V${y + rr}${rr ? `Q${x},${y} ${x + rr},${y}` : ''}H${x + barW - rr}${rr ? `Q${x + barW},${y} ${x + barW},${y + rr}` : ''}V${y + rh}Z`;
        return { d, color: v.color };
      });
      return { month: MONTHS[m], x, rects, counts: c, total: c.reduce((t, v) => t + v.count, 0) };
    });
    const ticks = [0, 1, 2, 3, 4].map((i) => ({ y: CH - PAD.b - (i / 4) * plotH, label: String((max / 4) * i) }));
    return { bars, ticks, barW };
  }

  /** Next preventive tasks and scheduled work orders. */
  get upcoming(): { date: string; type: WoType; description: string }[] {
    const today = toIsoDate(TODAY);
    const fromPlan = this.pmSchedules
      .filter((p) => p.nextDue >= today)
      .map((p) => ({ date: p.nextDue, type: (p.category === 'Inspection' ? 'Inspection' : 'Preventive') as WoType, description: p.name }));
    const fromOrders = this.data.workOrders.filter((w) => w.status !== 'Completed' && w.date >= today).map((w) => ({ date: w.date, type: w.type, description: w.description }));
    return [...fromOrders, ...fromPlan].sort((a, b) => a.date.localeCompare(b.date));
  }

  protected logActivity(text: string, kind: ActivityKind): void {
    this.data.activity.unshift({ date: toIsoDate(TODAY), text, kind });
  }

  /** Latest events from work orders, service requests, PM tasks and spare-part issues. */
  get recentActivity(): (Activity & { ago: string })[] {
    const events: Activity[] = [
      ...this.data.activity,
      ...this.history.slice(0, 6).map((w) => ({ date: w.date, text: `${w.id} marked as Completed`, kind: 'done' as const })),
      ...this.data.serviceRequests.map((r) => ({ date: r.date, text: `Service request ${r.id} created`, kind: 'request' as const })),
      ...this.pmSchedules.filter((p) => p.lastPerformed).map((p) => ({ date: p.lastPerformed, text: `PM performed (${p.name})`, kind: 'pm' as const })),
      ...this.latestIssues.map((x) => ({ date: x.date, text: `Spare part issued (${x.name})`, kind: 'part' as const })),
    ];
    // stable sort keeps this session's entries ahead of older ones on the same date
    return events
      .sort((a, b) => b.date.localeCompare(a.date))
      .slice(0, 4)
      .map((e) => ({ ...e, ago: this.ago(e.date) }));
  }

  /** Latest issue of each stocked part, newest first (for the activity feed). */
  private get latestIssues(): { date: string; name: string }[] {
    const seen = new Set<string>();
    const out: { date: string; name: string }[] = [];
    for (const u of [...this.data.partsUsage].sort((a, b) => b.date.localeCompare(a.date))) {
      const p = this.data.spareParts.find((x) => x.partNo === u.partNo);
      if (!p || !isStocked(p) || seen.has(u.partNo)) continue;
      seen.add(u.partNo);
      out.push({ date: u.date, name: p.name });
      if (out.length === 3) break;
    }
    return out;
  }

  private ago(iso: string): string {
    const days = Math.round((TODAY.getTime() - new Date(iso + 'T00:00').getTime()) / 86_400_000);
    if (days <= 0) return 'Today';
    if (days === 1) return '1 day ago';
    if (days < 30) return `${days} days ago`;
    const months = Math.round(days / 30);
    return months === 1 ? '1 month ago' : `${months} months ago`;
  }

  activityIcon(kind: ActivityKind) {
    return kind === 'done' ? CircleCheck : kind === 'request' ? Wrench : kind === 'pm' ? CalendarCheck : kind === 'part' ? Package : MessageSquarePlus;
  }

  /** Monthly maintenance cost (completed work orders) for the overview bar chart, up to the current month. */
  get costMonths(): { bars: { month: string; x: number; d: string; value: number }[]; ticks: { y: number; label: string }[]; barW: number } {
    const last = this.data.year === TODAY.getFullYear() ? TODAY.getMonth() : 11;
    const from = this.costYearRange === 'year' ? 0 : Math.max(0, last - 5);
    const months = MONTHS.map((_, i) => i).filter((m) => m >= from && m <= last);
    const orders = this.yearOrders.filter((w) => w.status === 'Completed');
    const values = months.map((m) => orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m).reduce((t, w) => t + this.cost(w), 0));
    const max = this.niceMax(Math.max(...values), 1000);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (this.cw2 - PAD.l - PAD.r) / months.length;
    const barW = Math.min(22, slot * 0.6);
    const bars = months.map((m, i) => {
      const x = PAD.l + slot * i + (slot - barW) / 2;
      const h = (values[i] / max) * plotH;
      const y = CH - PAD.b - h;
      const rr = Math.min(4, h);
      const d = h ? `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + barW - rr}Q${x + barW},${y} ${x + barW},${y + rr}V${y + h}Z` : '';
      return { month: MONTHS[m], x, d, value: values[i] };
    });
    const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => ({ y: CH - PAD.b - f * plotH, label: f ? `${Math.round((max * f) / 100) / 10}K` : '0' }));
    return { bars, ticks, barW };
  }

  // ===== Maintenance History tab =====

  histSearch = '';
  histType: WoType | '' = '';
  histStatus = '';
  histTech = '';
  histDowntimeOnly = false;
  histFrom = `${SOURCE.year - 1}-01-01`;
  histTo = `${SOURCE.year}-12-31`;
  histRangeOpen = false;
  histAdvOpen = false;
  histPageSize = 10;
  histPage = 0;
  readonly histChecked = new Set<string>();
  /** Work order shown in the side panel; null = first row. */
  selectedWoId: string | null = null;
  panelOpen = true;
  detailTab: 'details' | 'tasks' | 'parts' | 'costs' | 'documents' = 'details';
  histCostYear = SOURCE.year;
  histDownYear = SOURCE.year;
  hoverDot: { w: WorkOrder; x: number } | null = null;
  hoverHistCost: number | null = null;
  hoverHistDown: number | null = null;
  private readonly detailCache = new Map<string, WoDetail>();

  get histYears(): number[] {
    return [this.data.year, this.data.year - 1];
  }

  get histTechs(): string[] {
    return [...new Set(this.history.map((w) => w.technician))].sort();
  }

  get histStatuses(): string[] {
    return [...new Set(this.history.map((w) => w.status))];
  }

  get histRows(): WorkOrder[] {
    const q = this.histSearch.trim().toLowerCase();
    return this.history
      .filter((w) => w.date >= this.histFrom && w.date <= this.histTo)
      .filter((w) => (!this.histType || w.type === this.histType) && (!this.histStatus || w.status === this.histStatus) && (!this.histTech || w.technician === this.histTech))
      .filter((w) => !this.histDowntimeOnly || w.downtimeMin > 0)
      .filter((w) => !q || `${w.id} ${w.description} ${w.technician} ${w.type}`.toLowerCase().includes(q));
  }

  get histPageRows(): WorkOrder[] {
    return this.histRows.slice(this.histPage * this.histPageSize, (this.histPage + 1) * this.histPageSize);
  }

  get histPageCount(): number {
    return Math.max(1, Math.ceil(this.histRows.length / this.histPageSize));
  }

  get histPages(): number[] {
    return Array.from({ length: this.histPageCount }, (_, i) => i);
  }

  get histShowing(): string {
    const n = this.histRows.length;
    if (!n) return 'No records';
    return `Showing ${this.histPage * this.histPageSize + 1} to ${Math.min(n, (this.histPage + 1) * this.histPageSize)} of ${n} records`;
  }

  get histRangeLabel(): string {
    return `${this.fmtDate(this.histFrom)} - ${this.fmtDate(this.histTo)}`;
  }

  get histAdvCount(): number {
    return (this.histTech ? 1 : 0) + (this.histDowntimeOnly ? 1 : 0);
  }

  onHistFilter(): void {
    this.histPage = 0;
  }

  clearHistAdvanced(): void {
    this.histTech = '';
    this.histDowntimeOnly = false;
    this.onHistFilter();
  }

  get histAllChecked(): boolean {
    return this.histPageRows.length > 0 && this.histPageRows.every((w) => this.histChecked.has(w.id));
  }

  toggleHistAll(on: boolean): void {
    for (const w of this.histPageRows) on ? this.histChecked.add(w.id) : this.histChecked.delete(w.id);
  }

  toggleHistCheck(id: string, on: boolean): void {
    on ? this.histChecked.add(id) : this.histChecked.delete(id);
  }

  selectWo(w: WorkOrder): void {
    this.selectedWoId = w.id;
    this.panelOpen = true;
    this.detailTab = 'details';
    this.menuOpen = '';
  }

  get selectedWo(): WorkOrder | null {
    if (!this.panelOpen) return null;
    return (this.selectedWoId && this.data.workOrders.find((w) => w.id === this.selectedWoId)) || this.histRows[0] || null;
  }

  get woDetail(): WoDetail | null {
    const w = this.selectedWo;
    if (!w) return null;
    const key = `${this.asset.tag}|${w.id}|${this.cost(w)}`;
    let d = this.detailCache.get(key);
    if (!d) this.detailCache.set(key, (d = buildWoDetail(w)));
    return d;
  }

  get relatedRequest(): MaintenanceData['serviceRequests'][number] | null {
    const w = this.selectedWo;
    return (w && this.data.serviceRequests.find((r) => r.workOrder === w.id)) || null;
  }

  /** "2026-09-25 09:00" → "25 Sep 2026 09:00" */
  fmtDateTime(v: string): string {
    return `${this.fmtDate(v.slice(0, 10))} ${v.slice(11)}`;
  }

  /** Every listed work order as a dot on one time axis, coloured by type. */
  get timeline(): { dots: { w: WorkOrder; x: number; y: number; color: string }[]; ticks: { x: number; label: string }[] } {
    const t0 = new Date(this.histFrom + 'T00:00').getTime();
    const end = this.histTo < toIsoDate(TODAY) ? this.histTo : toIsoDate(TODAY);
    const t1 = Math.max(t0 + 86_400_000, new Date(end + 'T00:00').getTime());
    const width = CW - PAD.l - PAD.r;
    const xOf = (t: number) => PAD.l + ((t - t0) / (t1 - t0)) * width;
    const rows = [...this.histRows].reverse();
    const dots = rows.map((w, i) => {
      const x = xOf(new Date(w.date + 'T00:00').getTime());
      // nudge dots that would sit on top of the previous one
      const prev = i ? xOf(new Date(rows[i - 1].date + 'T00:00').getTime()) : -99;
      return { w, x, y: Math.abs(x - prev) < 9 ? CH / 2 - 12 : CH / 2, color: TYPE_COLORS[w.type] };
    });
    const ticks: { x: number; label: string }[] = [];
    const start = new Date(t0);
    const months = Math.round((t1 - t0) / (30.4 * 86_400_000));
    const every = months > 15 ? 3 : months > 7 ? 2 : 1;
    for (let d = new Date(start.getFullYear(), start.getMonth(), 1); d.getTime() <= t1; d.setMonth(d.getMonth() + every)) {
      ticks.push({ x: xOf(d.getTime()), label: `${MONTHS[d.getMonth()]} ${d.getFullYear()}` });
    }
    return { dots, ticks: ticks.filter((t) => t.x >= PAD.l - 1) };
  }

  /** Months of a year up to the current month (whole year for past years). */
  private monthsOf(year: number): number[] {
    const last = year === TODAY.getFullYear() ? TODAY.getMonth() : 11;
    return MONTHS.map((_, i) => i).filter((m) => m <= last);
  }

  get histCostBars(): { bars: { month: string; x: number; d: string; value: number }[]; ticks: { y: number; label: string }[]; barW: number } {
    const months = this.monthsOf(this.histCostYear);
    const orders = this.history.filter((w) => w.date.startsWith(String(this.histCostYear)));
    const values = months.map((m) => orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m).reduce((t, w) => t + this.cost(w), 0));
    const max = this.niceMax(Math.max(...values), 2000);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / months.length;
    const barW = Math.min(18, slot * 0.55);
    const bars = months.map((m, i) => {
      const x = PAD.l + slot * i + (slot - barW) / 2;
      const h = (values[i] / max) * plotH;
      const y = CH - PAD.b - h;
      const rr = Math.min(4, h);
      const d = h ? `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + barW - rr}Q${x + barW},${y} ${x + barW},${y + rr}V${y + h}Z` : '';
      return { month: MONTHS[m], x, d, value: values[i] };
    });
    const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => ({ y: CH - PAD.b - f * plotH, label: f ? `${Math.round((max * f) / 100) / 10}K` : '0' }));
    return { bars, ticks, barW };
  }

  get histDownLine(): { points: { x: number; y: number; month: string; min: number }[]; path: string; area: string; ticks: { y: number; label: string }[] } {
    const months = this.monthsOf(this.histDownYear);
    const orders = this.history.filter((w) => w.date.startsWith(String(this.histDownYear)));
    const mins = months.map((m) => orders.filter((w) => Number(w.date.slice(5, 7)) - 1 === m).reduce((t, w) => t + w.downtimeMin, 0));
    const max = this.niceMax(Math.max(...mins) / 60, 1);
    const plotH = CH - PAD.t - PAD.b;
    const step = (this.cw2 - PAD.l - PAD.r - 12) / Math.max(1, months.length - 1);
    const points = months.map((m, i) => ({ x: PAD.l + 6 + step * i, y: CH - PAD.b - (mins[i] / 60 / max) * plotH, month: MONTHS[m], min: mins[i] }));
    const path = points.map((p, i) => `${i ? 'L' : 'M'}${p.x},${p.y}`).join('');
    const area = `${path}L${points[points.length - 1].x},${CH - PAD.b}L${points[0].x},${CH - PAD.b}Z`;
    const ticks = Array.from({ length: max + 1 }, (_, i) => ({ y: CH - PAD.b - (i / max) * plotH, label: String(i) }));
    return { points, path, area, ticks };
  }

  exportHistory(): void {
    this.menuOpen = '';
    const rows = this.histRows
      .filter((w) => !this.histChecked.size || this.histChecked.has(w.id))
      .map((w, i) => ({
        '#': i + 1, Date: w.date, 'Work Order #': w.id, Type: w.type, Description: w.description, 'Performed By': w.technician,
        'Downtime (min)': w.downtimeMin, 'Cost (AED)': this.cost(w), Status: w.status,
      }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Maintenance History');
    XLSX.writeFile(book, `${this.asset.tag}_Maintenance_History.xlsx`);
  }

  // ===== Other tabs =====

  get history(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status === 'Completed').sort((a, b) => b.date.localeCompare(a.date));
  }

  get allOrders(): WorkOrder[] {
    return [...this.data.workOrders].sort((a, b) => b.date.localeCompare(a.date));
  }

  sensorState(s: MaintenanceData['sensors'][number]): 'Normal' | 'Warning' | 'Alarm' {
    if (s.low) return s.value <= s.alarm ? 'Alarm' : s.value <= s.warn ? 'Warning' : 'Normal';
    return s.value >= s.alarm ? 'Alarm' : s.value >= s.warn ? 'Warning' : 'Normal';
  }

  // ===== Actions =====

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.rangeOpen = false;
    this.histRangeOpen = false;
  }

  openWorkOrder(type: WoType = 'Corrective'): void {
    this.menuOpen = '';
    this.woError = '';
    this.woForm = { type, description: '', date: toIsoDate(TODAY), technician: 'Ahmed Khan' };
    this.pendingRequest = null;
  }

  saveWorkOrder(): void {
    const f = this.woForm;
    if (!f) return;
    if (!f.description.trim() || !f.date) return void (this.woError = 'Enter the description and the date.');
    const n = Math.max(0, ...this.data.workOrders.map((w) => Number(w.id.split('-')[2]) || 0)) + 1;
    const id = `WO-${this.data.year}-${String(n).padStart(3, '0')}`;
    this.data.workOrders = [
      { id, date: f.date, type: f.type, description: f.description.trim(), labor: 0, parts: 0, service: 0, downtimeMin: 0, reason: '', status: 'Open', technician: f.technician.trim() || CURRENT_USER },
      ...this.data.workOrders,
    ];
    this.woForm = null;
    if (this.pendingRequest) {
      this.srTab?.linkWorkOrder(this.pendingRequest, id, f.technician.trim() || CURRENT_USER);
      this.pendingRequest = null;
    }
    this.logActivity(`${id} created (${f.type})`, 'created');
    this.showToast(`${id} created for ${this.asset.tag}.`);
  }

  complete(w: WorkOrder): void {
    w.status = 'Completed';
    this.menuOpen = '';
    this.logActivity(`${w.id} marked as Completed`, 'done');
    this.showToast(`${w.id} marked completed. Add its costs from the work order.`);
  }

  export(kind: 'xlsx' | 'csv' = 'xlsx'): void {
    this.menuOpen = '';
    const rows = this.detailRows
      .filter((w) => !this.checked.size || this.checked.has(w.id))
      .map((w, i) => ({
        '#': i + 1, Date: w.date, 'Work Order #': w.id, Type: w.type, Description: w.description, 'Cost (AED)': this.cost(w), 'Labor Cost': w.labor, 'Parts Cost': w.parts,
        'Service Cost': w.service, 'Downtime (min)': w.downtimeMin, 'Downtime Reason': w.reason, Status: w.status, Technician: w.technician,
      }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Costs & Downtime');
    XLSX.writeFile(book, `${this.asset.tag}_Maintenance_Costs.${kind}`, { bookType: kind });
  }

  print(): void {
    this.menuOpen = '';
    window.print();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen = '';
    this.woForm = this.viewWo = null;
    this.rangeOpen = false;
    this.histRangeOpen = false;
  }

  protected showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }

  goAsset(): void {
    this.router.navigate(['/assets/status-condition'], { queryParams: { tag: this.asset.tag } });
  }
}
