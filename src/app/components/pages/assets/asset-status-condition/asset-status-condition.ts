import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Activity,
  CalendarDays,
  ChartColumn,
  ChevronDown,
  ClipboardCheck,
  ClipboardList,
  Download,
  Gauge,
  HeartPulse,
  Pencil,
  Plus,
  Power,
  Printer,
  ScrollText,
  Timer,
  TriangleAlert,
  Wrench,
  X,
} from 'lucide-angular';
import {
  ASSET_RECORDS,
  ASSET_TYPE_ICONS,
  AssetHeader,
  AssetTabLink,
  assetHeader,
  assetTabs,
  conditionPill,
  criticalityPill,
  findAsset,
  statusPill,
} from '../asset-data';
import {
  AlertItem,
  AlertStatus,
  Assessment,
  Condition,
  EnvReading,
  LifecycleInfo,
  MonthMetric,
  OperationalDetails,
  Reading,
  TimelineEvent,
  cloneDetail,
  loadAssetDetail,
} from '../asset-details/asset-details';

type SubTab = 'operational' | 'assessment' | 'performance' | 'environment' | 'alarms';
type TrendRange = 'all' | '3y' | '1y';
type ReadingState = 'Normal' | 'Warning' | 'Critical';

interface ChartPoint {
  a: Assessment;
  x: number;
  y: number;
}

const CONDITIONS: Condition[] = ['Excellent', 'Good', 'Fair', 'Poor', 'Critical'];
/** Health score drawn at each condition gridline of the trend chart (evenly spaced). */
const LEVEL_SCORE: Record<Condition, number> = { Excellent: 100, Good: 85, Fair: 70, Poor: 55, Critical: 40 };
const SCORE_MIN = 40;
const SCORE_MAX = 100;

/** Chart geometry in SVG user units; the SVG scales to its container width. */
const CHART = { w: 560, h: 210, left: 64, right: 18, top: 14, bottom: 30 };

const CURRENT_USER = 'Rex Kumar';
const MONTH = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

@Component({
  selector: 'app-asset-status-condition',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-status-condition.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-status-condition.css', './asset-status-condition-panels.css'],
})
export class AssetStatusCondition implements OnDestroy {
  readonly icons = {
    Activity, CalendarDays, ChartColumn, ChevronDown, ClipboardCheck, ClipboardList, Download, Gauge, HeartPulse,
    Pencil, Plus, Power, Printer, ScrollText, Timer, TriangleAlert, Wrench, X,
  };

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Status & Condition');
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly statusPill = statusPill;
  readonly conditionPill = conditionPill;
  readonly criticalityPill = criticalityPill;

  /** False until the asset's data file has loaded, or when it has none (the page then says so). */
  hasData = false;
  loading = false;

  readonly subTabs: { key: SubTab; label: string }[] = [
    { key: 'operational', label: 'Operational Status' },
    { key: 'assessment', label: 'Condition Assessment' },
    { key: 'performance', label: 'Performance Metrics' },
    { key: 'environment', label: 'Environmental Readings' },
    { key: 'alarms', label: 'Alarms & Events' },
  ];
  subTab: SubTab = 'operational';

  readonly conditions = CONDITIONS;
  readonly operationalStatuses = ['Active', 'Standby', 'Under Maintenance', 'Out of Service', 'Inactive'];
  /** Modes that apply to this kind of asset, from its data file. */
  operatingModes: string[] = [];
  readonly lifecycleStages = ['Planned', 'Procured', 'Installed', 'In Operation', 'Under Overhaul', 'In Storage', 'Decommissioning', 'Disposed'];

  // ===== Data (DEMO MODE: loaded from the asset's data file and kept in memory) =====
  assessments: Assessment[] = [];
  timeline: TimelineEvent[] = [];
  private nextAssessmentId = 1;

  availability = 0;
  utilization = 0;
  lastMaintenance = '';

  operational: OperationalDetails = {
    status: '', statusSince: '', operatingMode: '', loadLabel: 'Current Load', currentLoad: 0, loadUnit: '', capacityLabel: 'Rated Capacity',
    ratedCapacity: 0, ratedUnit: '', capacityFactor: null, runningHours: 0, lastStart: '', lastStop: '', location: '', controlledBy: '', remarks: '',
  };

  lifecycle: LifecycleInfo = { stage: '', commissionedOn: '', expectedLifeYears: 0, nextAssessment: '', nextMaintenance: '' };

  readingsUpdatedAt = '';
  readings: Reading[] = [];
  alerts: AlertItem[] = [];
  environment: EnvReading[] = [];
  /** Heading of the performance table's consumption column, e.g. "Fuel Used (L)". */
  consumptionLabel = '';
  performance: MonthMetric[] = [];

  // ===== UI state =====
  trendRange: TrendRange = 'all';
  /** Index into chartPoints under the pointer / keyboard focus; null shows the latest point. */
  hoverIndex: number | null = null;
  actionsOpen = false;
  timelineOpen = false;
  readingsOpen = false;

  /** Assessment dialog: id null = new assessment. */
  assessmentForm: (Omit<Assessment, 'id'> & { id: number | null }) | null = null;
  operationalForm: OperationalDetails | null = null;
  lifecycleForm: LifecycleInfo | null = null;
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  /** Today as yyyy-MM-dd (local), for ages and overdue checks. */
  readonly today = toIsoDate(new Date());

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => this.load(params.get('tag')));
  }

  private load(tag: string | null): void {
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    this.hasData = false;
    this.closeMenus();
    this.trendRange = 'all';
    this.hoverIndex = null;
    this.assessmentForm = null;
    this.operationalForm = null;
    this.lifecycleForm = null;
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      if (!detail) return;
      const s = cloneDetail(detail.status);
      this.assessments = s.assessments;
      this.timeline = s.timeline;
      this.nextAssessmentId = Math.max(0, ...s.assessments.map((a) => a.id)) + 1;
      this.availability = s.availability;
      this.utilization = s.utilization;
      this.lastMaintenance = s.lastMaintenance;
      this.operational = s.operational;
      this.operatingModes = s.operatingModes;
      this.lifecycle = s.lifecycle;
      this.readingsUpdatedAt = s.readingsUpdatedAt;
      this.readings = s.readings;
      this.alerts = s.alerts;
      this.environment = s.environment;
      this.consumptionLabel = s.consumptionLabel;
      this.performance = s.performance;
      this.hasData = true;
    });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  /** Pill tone for the header's criticality chip (classes from the panels stylesheet). */
  criticalityTone(criticality: string): string {
    return criticality === 'Critical' ? 'critical' : criticality === 'Low' ? 'good' : 'warn';
  }

  // ===== Derived values =====

  /** Assessments oldest first. */
  get sortedAssessments(): Assessment[] {
    return [...this.assessments].sort((a, b) => a.date.localeCompare(b.date));
  }

  get latest(): Assessment | undefined {
    const list = this.sortedAssessments;
    return list[list.length - 1];
  }

  /** Newest first, for the assessment history table. */
  get assessmentHistory(): Assessment[] {
    return [...this.sortedAssessments].reverse();
  }

  get recentTimeline(): TimelineEvent[] {
    return this.sortedTimeline.slice(0, 4);
  }

  get sortedTimeline(): TimelineEvent[] {
    return [...this.timeline].sort((a, b) => b.date.localeCompare(a.date));
  }

  /** Load as a share of rated real power (kVA × power factor). */
  /** Load as a percentage of rated capacity, or null when the two are not comparable. */
  get loadPercent(): number | null {
    const { capacityFactor, ratedCapacity, currentLoad } = this.operational;
    if (capacityFactor === null) return null;
    const rated = ratedCapacity * capacityFactor;
    return rated ? Math.round((currentLoad / rated) * 100) : 0;
  }

  get ageText(): string {
    return formatMonths(monthsBetween(this.lifecycle.commissionedOn, this.today));
  }

  get remainingLifeText(): string {
    const left = this.lifecycle.expectedLifeYears * 12 - monthsBetween(this.lifecycle.commissionedOn, this.today);
    return left > 0 ? formatMonths(left) : 'End of expected life reached';
  }

  isOverdue(date: string): boolean {
    return !!date && date < this.today;
  }

  conditionTone(condition: Condition | string | undefined): string {
    switch (condition) {
      case 'Excellent':
      case 'Good':
        return 'good';
      case 'Fair':
        return 'warn';
      case 'Poor':
        return 'serious';
      default:
        return 'critical';
    }
  }

  statusTone(status: string): string {
    return status === 'Active' ? 'good' : status === 'Standby' || status === 'Under Maintenance' ? 'warn' : 'critical';
  }

  readingText(r: Reading): string {
    return r.value.toFixed(r.decimals ?? 0);
  }

  readingState(r: Reading): ReadingState {
    if (r.min !== undefined && r.value < r.min) return 'Warning';
    if (r.max !== undefined && r.value > r.max) return 'Warning';
    return 'Normal';
  }

  readingPercent(r: Reading): number {
    return r.scaleMax ? Math.min(100, Math.max(0, (r.value / r.scaleMax) * 100)) : 0;
  }

  readingRange(r: Reading): string {
    if (r.min !== undefined && r.max !== undefined) return `${r.min} – ${r.max} ${r.unit}`.trim();
    if (r.min !== undefined) return `≥ ${r.min} ${r.unit}`.trim();
    if (r.max !== undefined) return `≤ ${r.max} ${r.unit}`.trim();
    return '-';
  }

  envState(e: EnvReading): ReadingState {
    const ok = e.limitType === 'max' ? e.value <= e.limit : e.value >= e.limit;
    if (ok) return e.limitType === 'max' && e.value > e.limit * 0.9 ? 'Warning' : 'Normal';
    return 'Critical';
  }

  get activeAlerts(): AlertItem[] {
    return this.alerts.filter((a) => a.status !== 'Resolved');
  }

  get sortedAlerts(): AlertItem[] {
    return [...this.alerts].sort((a, b) => b.raisedAt.localeCompare(a.raisedAt));
  }

  // ===== Condition trend chart =====

  readonly chart = CHART;

  get plotWidth(): number {
    return CHART.w - CHART.left - CHART.right;
  }

  get plotBottom(): number {
    return CHART.h - CHART.bottom;
  }

  get rangedAssessments(): Assessment[] {
    if (this.trendRange === 'all') return this.sortedAssessments;
    const months = this.trendRange === '3y' ? 36 : 12;
    const since = toIsoDate(addMonths(new Date(), -months));
    return this.sortedAssessments.filter((a) => a.date >= since);
  }

  /** Time span shown on the x-axis, padded to whole half-years. */
  private get domain(): [number, number] {
    const list = this.rangedAssessments;
    if (!list.length) return [0, 1];
    const first = parseDate(list[0].date);
    const last = parseDate(list[list.length - 1].date);
    const start = new Date(first.getFullYear(), first.getMonth() < 6 ? 0 : 6, 1);
    const endMonth = last.getMonth() < 6 ? 6 : 12;
    const end = new Date(last.getFullYear(), endMonth, 1);
    return [start.getTime(), end.getTime()];
  }

  private xFor(date: string | Date): number {
    const [d0, d1] = this.domain;
    const t = (typeof date === 'string' ? parseDate(date) : date).getTime();
    return CHART.left + ((t - d0) / (d1 - d0 || 1)) * this.plotWidth;
  }

  yFor(score: number): number {
    const clamped = Math.min(SCORE_MAX, Math.max(SCORE_MIN, score));
    return CHART.top + ((SCORE_MAX - clamped) / (SCORE_MAX - SCORE_MIN)) * (this.plotBottom - CHART.top);
  }

  get chartPoints(): ChartPoint[] {
    return this.rangedAssessments.map((a) => ({ a, x: this.xFor(a.date), y: this.yFor(a.healthScore) }));
  }

  /** Line segments, each coloured by the condition it leads to. */
  get chartSegments(): { x1: number; y1: number; x2: number; y2: number; tone: string }[] {
    const p = this.chartPoints;
    return p.slice(1).map((pt, i) => ({ x1: p[i].x, y1: p[i].y, x2: pt.x, y2: pt.y, tone: this.conditionTone(pt.a.overall) }));
  }

  get yLevels(): { label: Condition; y: number }[] {
    return CONDITIONS.map((label) => ({ label, y: this.yFor(LEVEL_SCORE[label]) }));
  }

  /** Half-year ticks (Jan / Jul) across the domain. */
  get xTicks(): { label: string; x: number }[] {
    const [d0, d1] = this.domain;
    const ticks: { label: string; x: number }[] = [];
    for (let d = new Date(d0); d.getTime() < d1; d = addMonths(d, 6)) {
      ticks.push({ label: `${MONTH[d.getMonth()]} ${d.getFullYear()}`, x: this.xFor(d) });
    }
    return ticks;
  }

  /** Hit areas: each point owns the band halfway to its neighbours, so hovering anywhere in a column works. */
  get hitBands(): { x: number; w: number }[] {
    const p = this.chartPoints;
    return p.map((pt, i) => {
      const left = i === 0 ? CHART.left : (p[i - 1].x + pt.x) / 2;
      const right = i === p.length - 1 ? CHART.w - CHART.right : (pt.x + p[i + 1].x) / 2;
      return { x: left, w: Math.max(1, right - left) };
    });
  }

  get focusPoint(): ChartPoint | undefined {
    const p = this.chartPoints;
    return p[this.hoverIndex ?? p.length - 1];
  }

  /** Tooltip position as % of the chart box, flipped to the left near the right edge. */
  get tooltipStyle(): Record<string, string> {
    const pt = this.focusPoint;
    if (!pt) return {};
    const leftPct = (pt.x / CHART.w) * 100;
    const topPct = (pt.y / CHART.h) * 100;
    return {
      left: `${leftPct}%`,
      top: `${topPct}%`,
      transform: leftPct > 60 ? 'translate(calc(-100% - 12px), -110%)' : 'translate(12px, -110%)',
    };
  }

  onTrendRangeChange(): void {
    this.hoverIndex = null;
  }

  // ===== Menus =====

  toggleActions(event: Event): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.timelineOpen = this.readingsOpen = false;
    this.assessmentForm = null;
    this.operationalForm = null;
    this.lifecycleForm = null;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  // ===== Assessments =====

  openNewAssessment(): void {
    this.closeMenus();
    this.formError = '';
    const base = this.latest;
    this.assessmentForm = {
      id: null,
      date: this.today,
      assessedBy: CURRENT_USER,
      overall: base?.overall ?? 'Good',
      healthScore: base?.healthScore ?? 85,
      physical: base?.physical ?? 'Good',
      performance: base?.performance ?? 'Good',
      safety: base?.safety ?? 'Good',
      remarks: '',
    };
  }

  openEditAssessment(a: Assessment | undefined): void {
    if (!a) return;
    this.formError = '';
    this.assessmentForm = { ...a };
  }

  saveAssessment(): void {
    const f = this.assessmentForm;
    if (!f) return;
    const score = Number(f.healthScore);
    if (!f.date || !f.assessedBy.trim()) {
      this.formError = 'Enter the assessment date and who assessed it.';
      return;
    }
    if (!(score >= 0 && score <= 100)) {
      this.formError = 'Health score must be between 0 and 100.';
      return;
    }
    if (f.date > this.today) {
      this.formError = 'The assessment date cannot be in the future.';
      return;
    }
    const previous = this.latest?.overall;
    const record: Assessment = { ...f, id: f.id ?? this.nextAssessmentId++, healthScore: Math.round(score), assessedBy: f.assessedBy.trim(), remarks: f.remarks.trim() };
    if (f.id === null) {
      this.assessments = [...this.assessments, record];
      const changed = previous && previous !== record.overall;
      this.timeline = [
        ...this.timeline,
        {
          date: record.date,
          title: changed ? `Condition changed to ${record.overall}` : `Condition assessed as ${record.overall}`,
          by: record.assessedBy,
          condition: record.overall,
        },
      ];
      if (this.isOverdue(this.lifecycle.nextAssessment) || this.lifecycle.nextAssessment <= record.date) {
        // Schedule the next assessment six months on
        this.lifecycle.nextAssessment = toIsoDate(addMonths(parseDate(record.date), 6));
      }
      this.showToast('Assessment recorded.');
    } else {
      this.assessments = this.assessments.map((a) => (a.id === record.id ? record : a));
      this.showToast('Assessment updated.');
    }
    this.hoverIndex = null;
    this.assessmentForm = null;
  }

  // ===== Operational details / lifecycle =====

  openOperationalEdit(): void {
    this.formError = '';
    this.operationalForm = { ...this.operational };
  }

  saveOperational(): void {
    const f = this.operationalForm;
    if (!f) return;
    if (!f.status || !f.statusSince) {
      this.formError = 'Enter the operational status and since when it applies.';
      return;
    }
    if (f.currentLoad < 0 || f.ratedCapacity <= 0 || f.runningHours < 0) {
      this.formError = 'Load, rated capacity and running hours must be positive numbers.';
      return;
    }
    if (f.status !== this.operational.status) {
      this.timeline = [...this.timeline, { date: f.statusSince, title: `Status changed to ${f.status}`, by: CURRENT_USER, status: f.status }];
    }
    this.operational = { ...f, location: f.location.trim(), controlledBy: f.controlledBy.trim(), remarks: f.remarks.trim() };
    this.operationalForm = null;
    this.showToast('Operational details saved.');
  }

  openLifecycleEdit(): void {
    this.formError = '';
    this.lifecycleForm = { ...this.lifecycle };
  }

  saveLifecycle(): void {
    const f = this.lifecycleForm;
    if (!f) return;
    if (!f.stage || !(f.expectedLifeYears > 0)) {
      this.formError = 'Enter the lifecycle stage and an expected useful life above zero.';
      return;
    }
    this.lifecycle = { ...f };
    this.lifecycleForm = null;
    this.showToast('Lifecycle details saved.');
  }

  // ===== Alarms =====

  setAlertStatus(alert: AlertItem, status: AlertStatus): void {
    alert.status = status;
    this.showToast(`Alert marked ${status.toLowerCase()}.`);
  }

  // ===== Export / print =====

  exportAssessments(): void {
    this.closeMenus();
    const head = ['Date', 'Assessed By', 'Overall Condition', 'Health Score', 'Physical', 'Performance', 'Safety', 'Remarks'];
    const rows = this.assessmentHistory.map((a) => [a.date, a.assessedBy, a.overall, `${a.healthScore}%`, a.physical, a.performance, a.safety, a.remarks]);
    const csv = [head, ...rows].map((row) => row.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.asset.tag}-condition-assessments.csv`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url));
  }

  print(): void {
    this.closeMenus();
    window.print();
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}

// ===== Date helpers (local time, yyyy-MM-dd strings) =====

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function parseDate(iso: string): Date {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

function addMonths(d: Date, months: number): Date {
  return new Date(d.getFullYear(), d.getMonth() + months, d.getDate());
}

/** Whole months from one date to another. */
function monthsBetween(fromIso: string, toIso: string): number {
  const a = parseDate(fromIso);
  const b = parseDate(toIso);
  return Math.max(0, (b.getFullYear() - a.getFullYear()) * 12 + b.getMonth() - a.getMonth() - (b.getDate() < a.getDate() ? 1 : 0));
}

/** 44 -> "3 Years 8 Months". */
function formatMonths(total: number): string {
  const years = Math.floor(total / 12);
  const months = total % 12;
  const parts = [];
  if (years) parts.push(`${years} Year${years === 1 ? '' : 's'}`);
  if (months || !years) parts.push(`${months} Month${months === 1 ? '' : 's'}`);
  return parts.join(' ');
}
