import { Component, DestroyRef, ElementRef, HostListener, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Activity,
  ArrowDown,
  ArrowRight,
  ArrowUp,
  CalendarCheck,
  CalendarDays,
  ChartColumn,
  ChartPie,
  ChevronDown,
  CircleCheck,
  ClipboardList,
  Download,
  HeartPulse,
  Info,
  Lightbulb,
  Pencil,
  Play,
  Printer,
  ShieldCheck,
  TrendingUp,
  TriangleAlert,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetHeader, AssetTabLink, assetHeader, assetTabs, findAsset } from '../asset-data';
import { HealthData, HealthRecord, HealthStatus, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';

type Range = 6 | 12;

type DetailsForm = Pick<HealthData, 'method' | 'assessedBy' | 'nextAssessment' | 'remarks'>;

interface TrendPoint {
  x: number;
  y: number;
  r: HealthRecord;
}

const CURRENT_USER = 'Rex Kumar';

/** Trend chart geometry in SVG user units; the SVG scales to its container. */
const CHART = { w: 600, h: 230, left: 40, right: 20, top: 20, bottom: 30 };
/** Score ring and breakdown donut radii. */
const RING_R = 30;
const DONUT_R = 50;

/** Shown under the health status. */
const STATUS_ADVICE: Record<HealthStatus, string> = {
  Healthy: 'No immediate action required',
  Good: 'Monitor at the next assessment',
  Fair: 'Plan corrective maintenance',
  Poor: 'Corrective action required',
  Critical: 'Immediate action required',
};

/** Health of each asset, kept in memory so edits survive moving between tabs (DEMO MODE). */
const HEALTH = new Map<string, HealthData>();

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function parseDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function addMonths(iso: string, months: number): string {
  const d = parseDate(iso);
  return toIsoDate(new Date(d.getFullYear(), d.getMonth() + months, d.getDate()));
}

export function healthStatus(score: number): HealthStatus {
  return score >= 85 ? 'Healthy' : score >= 70 ? 'Good' : score >= 55 ? 'Fair' : score >= 40 ? 'Poor' : 'Critical';
}

@Component({
  selector: 'app-asset-health-score',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-health-score.html',
  // Shares the Status & Condition page's header, tab, table and modal styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-health-score.css',
  ],
})
export class AssetHealthScore implements OnDestroy {
  readonly icons = {
    Activity, ArrowDown, ArrowRight, ArrowUp, CalendarCheck, CalendarDays, ChartColumn, ChartPie, ChevronDown, CircleCheck, ClipboardList,
    Download, HeartPulse, Info, Lightbulb, Pencil, Play, Printer, ShieldCheck, TrendingUp, TriangleAlert, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly chart = CHART;
  readonly ringR = RING_R;
  readonly donutR = DONUT_R;
  readonly ringLength = 2 * Math.PI * RING_R;
  readonly donutLength = 2 * Math.PI * DONUT_R;
  readonly yTicks = [0, 25, 50, 75, 100];

  @ViewChild('historyEl') private historyEl?: ElementRef<HTMLElement>;

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Asset Health Score');

  loading = false;
  health: HealthData | null = null;
  /** Share of register assets that are Healthy or Good; null until every asset has loaded. */
  healthIndex: number | null = null;

  trendRange: Range = 6;
  historyRange: Range = 12;
  /** History shows six rows until "View All". */
  historyAll = false;
  insightsAll = false;
  hoverIndex: number | null = null;

  actionsOpen = false;
  detailsForm: DetailsForm | null = null;
  planOpen = false;
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  readonly today = toIsoDate(new Date());

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => this.load(params.get('tag')));
    this.loadIndex();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  private load(tag: string | null): void {
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    this.health = HEALTH.get(record.tag) ?? null;
    this.historyAll = this.insightsAll = false;
    this.hoverIndex = null;
    this.detailsForm = null;
    this.planOpen = false;
    this.closeMenus();
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      if (detail) this.health = this.stateFor(record.tag, detail.health);
    });
  }

  private stateFor(tag: string, fromFile: HealthData): HealthData {
    let state = HEALTH.get(tag);
    if (!state) {
      state = cloneDetail(fromFile);
      HEALTH.set(tag, state);
    }
    return state;
  }

  /** Loads every asset's health once, for the Health Index card. */
  private loadIndex(): void {
    Promise.all(ASSET_RECORDS.map((a) => loadAssetDetail(a.tag).then((d) => (d ? this.stateFor(a.tag, d.health) : null)))).then(() => {
      this.refreshIndex();
    });
  }

  private refreshIndex(): void {
    const all = ASSET_RECORDS.map((a) => HEALTH.get(a.tag)).filter((h): h is HealthData => !!h);
    this.healthIndex = all.length ? Math.round((all.filter((h) => h.status === 'Healthy' || h.status === 'Good').length / all.length) * 100) : null;
  }

  // ===== Tones =====

  statusTone(status: string): string {
    return status === 'Active' || status === 'Checked Out' ? 'good' : status === 'Disposed' ? 'critical' : 'warn';
  }

  conditionTone(condition: string): string {
    return condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : 'critical';
  }

  criticalityTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  healthTone(status: string): string {
    return status === 'Healthy' || status === 'Good' ? 'good' : status === 'Fair' ? 'warn' : status === 'Poor' ? 'serious' : 'critical';
  }

  scoreTone(score: number): string {
    return this.healthTone(healthStatus(score));
  }

  advice(status: HealthStatus): string {
    return STATUS_ADVICE[status];
  }

  // ===== KPI cards =====

  /** Stroke dash for the score ring. */
  ringDash(score: number): string {
    const on = (score / 100) * this.ringLength;
    return `${on.toFixed(1)} ${(this.ringLength - on).toFixed(1)}`;
  }

  /** Change against the previous monthly assessment, in percent. */
  get monthChange(): number | null {
    const h = this.health?.history;
    if (!h || h.length < 2 || !h[1].score) return null;
    return Math.round(((h[0].score - h[1].score) / h[1].score) * 1000) / 10;
  }

  /** Small sparkline of the last eight scores, oldest first. */
  get sparkline(): string {
    const pts = (this.health?.history ?? []).slice(0, 8).reverse();
    if (pts.length < 2) return '';
    const lo = Math.min(...pts.map((p) => p.score)) - 2;
    const hi = Math.max(...pts.map((p) => p.score)) + 2;
    return pts.map((p, i) => `${((i / (pts.length - 1)) * 150).toFixed(1)},${(36 - ((p.score - lo) / (hi - lo)) * 32).toFixed(1)}`).join(' ');
  }

  get daysToNextAssessment(): number | null {
    const next = this.health?.nextAssessment;
    if (!next) return null;
    return Math.round((parseDate(next).getTime() - parseDate(this.today).getTime()) / 86_400_000);
  }

  // ===== Trend chart =====

  get trendPoints(): TrendPoint[] {
    const recs = (this.health?.history ?? []).slice(0, this.trendRange).reverse();
    const plotW = CHART.w - CHART.left - CHART.right;
    const plotH = CHART.h - CHART.top - CHART.bottom;
    return recs.map((r, i) => ({
      r,
      x: CHART.left + (recs.length > 1 ? (i / (recs.length - 1)) * plotW : plotW / 2),
      y: CHART.top + (1 - r.score / 100) * plotH,
    }));
  }

  yFor(score: number): number {
    return CHART.top + (1 - score / 100) * (CHART.h - CHART.top - CHART.bottom);
  }

  get trendLine(): string {
    return this.trendPoints.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  }

  get trendArea(): string {
    const p = this.trendPoints;
    if (!p.length) return '';
    const base = this.yFor(0);
    return `M${p[0].x.toFixed(1)},${base} ` + p.map((q) => `L${q.x.toFixed(1)},${q.y.toFixed(1)}`).join(' ') + ` L${p[p.length - 1].x.toFixed(1)},${base} Z`;
  }

  get hoverPoint(): TrendPoint | null {
    return this.hoverIndex === null ? null : this.trendPoints[this.hoverIndex] ?? null;
  }

  // ===== Breakdown donut =====

  /** One arc per component, sized by its weight. */
  get donutArcs(): { dash: string; offset: number; key: string }[] {
    let start = 0;
    return (this.health?.breakdown ?? []).map((c) => {
      const len = (c.weight / 100) * this.donutLength;
      const gap = 2;
      const arc = { key: c.key, dash: `${Math.max(0, len - gap).toFixed(1)} ${(this.donutLength - len + gap).toFixed(1)}`, offset: -start };
      start += len;
      return arc;
    });
  }

  // ===== History =====

  get historyRows(): HealthRecord[] {
    const rows = (this.health?.history ?? []).slice(0, this.historyRange);
    return this.historyAll ? rows : rows.slice(0, 6);
  }

  get historyTotal(): number {
    return Math.min(this.historyRange, this.health?.history.length ?? 0);
  }

  viewTrendDetails(): void {
    this.historyAll = true;
    this.historyEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ===== Actions =====

  /** Records a manual health check now (the score comes from the current breakdown). */
  runHealthCheck(): void {
    this.closeMenus();
    const h = this.health;
    if (!h) return;
    if (this.asset.status === 'Disposed') {
      this.showToast('This asset has been disposed; health checks are no longer recorded.');
      return;
    }
    const score = Math.round(h.breakdown.reduce((s, c) => s + (c.score * c.weight) / 100, 0));
    const status = healthStatus(score);
    h.history = [{ date: this.today, score, status, trigger: 'Manual', comments: 'Manual health check', assessedBy: CURRENT_USER }, ...h.history];
    Object.assign(h, { score, status, assessmentDate: this.today, nextAssessment: addMonths(this.today, 1), assessedBy: CURRENT_USER });
    this.refreshIndex();
    this.showToast(`Health check recorded: ${score} / 100 (${status}).`);
  }

  openDetailsEdit(): void {
    this.closeMenus();
    if (!this.health) return;
    this.formError = '';
    const { method, assessedBy, nextAssessment, remarks } = this.health;
    this.detailsForm = { method, assessedBy, nextAssessment, remarks };
  }

  saveDetails(): void {
    const f = this.detailsForm;
    if (!f || !this.health) return;
    if (!f.method.trim() || !f.assessedBy.trim()) {
      this.formError = 'Enter the assessment method and who assessed the asset.';
      return;
    }
    Object.assign(this.health, { method: f.method.trim(), assessedBy: f.assessedBy.trim(), nextAssessment: f.nextAssessment, remarks: f.remarks.trim() });
    this.detailsForm = null;
    this.showToast('Asset health details saved.');
  }

  openPlan(): void {
    this.closeMenus();
    this.planOpen = true;
  }

  isOverdue(date: string): boolean {
    return !!date && date < this.today;
  }

  exportHistory(): void {
    this.closeMenus();
    const h = this.health;
    if (!h) return;
    const lines = [
      ['Date', 'Health Score', 'Status', 'Trigger', 'Comments', 'Assessed By'],
      ...h.history.map((r) => [r.date, r.score, r.status, r.trigger, r.comments, r.assessedBy]),
    ];
    const csv = lines.map((l) => l.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${this.asset.tag}_health_history.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  print(): void {
    this.closeMenus();
    window.print();
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
    this.detailsForm = null;
    this.planOpen = false;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
