import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule, Archive, BarChart3, Boxes, CircleCheck, Clock, Download, Eye, Hourglass, PackageX, Pencil, RefreshCw,
  Search, Settings2, Trash2, TriangleAlert, X,
} from 'lucide-angular';
import { apiErrorMessage } from '../../services/asset-api';
import { conditionTone, formatDate, statusTone } from '../../asset-status-condition-overview/status-condition.service';
import { DisposalState, LifecycleData, LifecycleEvent, LifecycleOverviewService, LifecycleRow, PHASES, Phase, stageLabel } from './lifecycle-overview.service';

type View = 'summary' | 'events' | 'analytics';
type ReplacementDue = '' | 'exceeded' | 'year' | 'later' | 'unknown';

interface Kpi {
  label: string;
  value: number;
  icon: typeof Boxes;
  hint: string;
  apply: () => void;
  active: () => boolean;
}

/** Life used at or above this share counts as nearing end of life. */
const NEAR_EOL = 80;
const PAGE_SIZE = 20;

/**
 * Assets > Asset Lifecycle (landing page): every asset's lifecycle stage, age, useful life, replacement date and
 * disposal state, with KPIs, filters, lifecycle events and analytics. All data comes from existing APIs (Asset
 * Status & Condition list, asset registrations, asset lifecycle records, asset disposals); nothing is sample data.
 * The single-asset lifecycle view is /assets/lifecycle/asset?tag=.
 */
@Component({
  selector: 'app-lifecycle-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './lifecycle-overview.html',
  styleUrls: [
    '../../asset-status-condition/asset-status-condition.css',
    '../../asset-status-condition/asset-status-condition-panels.css',
    '../../asset-status-condition-overview/asset-status-condition-overview.css',
    './lifecycle-overview.css',
  ],
})
export class LifecycleOverview implements OnInit {
  readonly icons = { Archive, BarChart3, Boxes, CircleCheck, Clock, Download, Eye, Hourglass, PackageX, Pencil, RefreshCw, Search, Settings2, Trash2, TriangleAlert, X };
  readonly phases = PHASES;
  readonly statusTone = statusTone;
  readonly conditionTone = conditionTone;
  readonly formatDate = formatDate;
  readonly stageLabel = stageLabel;
  readonly nearEol = NEAR_EOL;
  readonly pageSize = PAGE_SIZE;

  private readonly api = inject(LifecycleOverviewService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  view: View = 'summary';
  rows: LifecycleRow[] = [];
  events: LifecycleEvent[] = [];
  loading = false;
  loadError = '';
  page = 1;
  detail: LifecycleRow | null = null;

  // ===== Filters =====
  search = '';
  site = '';
  category = '';
  assetType = '';
  phase: Phase | '' = '';
  status = '';
  condition = '';
  criticality = '';
  disposal: DisposalState | '' = '';
  due: ReplacementDue = '';
  /** Life used band from the KPIs: near (>= 80% and < 100%) or exceeded (100%). */
  life: '' | 'near' | 'exceeded' = '';
  from = '';
  to = '';

  kpis: Kpi[] = [];

  ngOnInit(): void {
    // Links from elsewhere (asset detail tabs) open one asset's lifecycle: /assets/lifecycle?tag=
    const tag = this.route.snapshot.queryParamMap.get('tag');
    if (tag) {
      this.router.navigate(['/assets/lifecycle/asset'], { queryParams: { tag }, replaceUrl: true });
      return;
    }
    this.refresh();
  }

  refresh(): void {
    this.loading = true;
    this.loadError = '';
    this.api.load().subscribe({
      next: (d: LifecycleData) => {
        this.rows = d.rows;
        this.events = d.events;
        this.loading = false;
        this.kpis = this.buildKpis();
        if (this.detail) this.detail = this.rows.find((r) => r.asset.assetRegistrationId === this.detail!.asset.assetRegistrationId) ?? null;
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load the asset lifecycle data.');
      },
    });
  }

  // ===== KPIs =====

  private count(test: (r: LifecycleRow) => boolean): number {
    return this.rows.filter(test).length;
  }

  private buildKpis(): Kpi[] {
    const only = (set: () => void) => () => {
      this.clearFilters();
      this.view = 'summary';
      set();
    };
    return [
      { label: 'Total Assets', value: this.rows.length, icon: Boxes, hint: 'All registered assets', apply: only(() => undefined), active: () => !this.hasFilters },
      { label: 'In Service', value: this.count((r) => r.phase === 'In Service'), icon: CircleCheck, hint: 'Current stage Operation, Maintenance or Upgrade', apply: only(() => (this.phase = 'In Service')), active: () => this.phase === 'In Service' },
      { label: 'Commissioning', value: this.count((r) => r.phase === 'Commissioning'), icon: Settings2, hint: 'Current stage Installation or Commissioning', apply: only(() => (this.phase = 'Commissioning')), active: () => this.phase === 'Commissioning' },
      { label: 'Nearing End of Life', value: this.count((r) => r.lifeUsedPct != null && r.lifeUsedPct >= NEAR_EOL && r.lifeUsedPct < 100), icon: Hourglass, hint: `${NEAR_EOL}% or more of the expected useful life used`, apply: only(() => (this.life = 'near')), active: () => this.life === 'near' },
      { label: 'End of Life Exceeded', value: this.count((r) => r.lifeUsedPct === 100), icon: TriangleAlert, hint: 'Older than the expected useful life', apply: only(() => (this.life = 'exceeded')), active: () => this.life === 'exceeded' },
      { label: 'Retired Assets', value: this.count((r) => r.phase === 'Retired'), icon: Archive, hint: 'Current stage Decommissioning', apply: only(() => (this.phase = 'Retired')), active: () => this.phase === 'Retired' },
      { label: 'Pending Disposal', value: this.count((r) => ['Draft', 'Pending Approval', 'Approved'].includes(r.disposal)), icon: Clock, hint: 'In a draft, pending or approved disposal request', apply: only(() => (this.disposal = 'Pending Approval')), active: () => this.disposal === 'Pending Approval' },
      { label: 'Disposed Assets', value: this.count((r) => r.disposal === 'Completed'), icon: PackageX, hint: 'Disposal request completed', apply: only(() => (this.disposal = 'Completed')), active: () => this.disposal === 'Completed' },
    ];
  }

  /** Assets with no lifecycle stage recorded yet. */
  get untracked(): number {
    return this.count((r) => r.phase === 'Not tracked');
  }

  // ===== Filter choices =====

  private distinct(pick: (r: LifecycleRow) => string | null): string[] {
    return [...new Set(this.rows.map(pick).filter((v): v is string => !!v))].sort();
  }

  get sites(): string[] { return this.distinct((r) => r.asset.siteName); }
  get categories(): string[] { return this.distinct((r) => r.asset.category); }
  get assetTypes(): string[] { return this.distinct((r) => (!this.category || r.asset.category === this.category ? r.asset.assetType : null)); }
  get statuses(): string[] { return this.distinct((r) => r.asset.status); }
  get conditions(): string[] { return this.distinct((r) => r.asset.condition); }
  get criticalities(): string[] { return this.distinct((r) => r.asset.criticality); }

  // ===== Asset Summary =====

  replacementDue(r: LifecycleRow): ReplacementDue {
    if (!r.plannedReplacement) return 'unknown';
    const t = new Date(r.plannedReplacement).getTime();
    return t < Date.now() ? 'exceeded' : t < Date.now() + 365 * 86_400_000 ? 'year' : 'later';
  }

  get filtered(): LifecycleRow[] {
    const q = this.search.trim().toLowerCase();
    const from = this.from ? new Date(`${this.from}T00:00:00`).getTime() : null;
    const to = this.to ? new Date(`${this.to}T23:59:59`).getTime() : null;
    return this.rows.filter((r) => {
      const a = r.asset;
      const acquired = r.acquisitionDate ? new Date(r.acquisitionDate).getTime() : null;
      return (
        (!q || [a.assetRegistrationId, a.assetTag, a.assetName, a.siteName, a.category, a.assetType].some((v) => (v ?? '').toLowerCase().includes(q))) &&
        (!this.site || a.siteName === this.site) &&
        (!this.category || a.category === this.category) &&
        (!this.assetType || a.assetType === this.assetType) &&
        (!this.phase || r.phase === this.phase) &&
        (!this.status || a.status === this.status) &&
        (!this.condition || a.condition === this.condition) &&
        (!this.criticality || a.criticality === this.criticality) &&
        (!this.disposal || (this.disposal === 'Pending Approval' ? ['Draft', 'Pending Approval', 'Approved'].includes(r.disposal) : r.disposal === this.disposal)) &&
        (!this.due || this.replacementDue(r) === this.due) &&
        (!this.life || (this.life === 'exceeded' ? r.lifeUsedPct === 100 : r.lifeUsedPct != null && r.lifeUsedPct >= NEAR_EOL && r.lifeUsedPct < 100)) &&
        (from === null || (acquired !== null && acquired >= from)) &&
        (to === null || (acquired !== null && acquired <= to))
      );
    });
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / PAGE_SIZE));
  }

  get pageRows(): LifecycleRow[] {
    const p = Math.min(this.page, this.pageCount);
    return this.filtered.slice((p - 1) * PAGE_SIZE, p * PAGE_SIZE);
  }

  setPage(p: number): void {
    this.page = Math.min(Math.max(1, p), this.pageCount);
  }

  onFilter(): void {
    this.page = 1;
  }

  get hasFilters(): boolean {
    return !!(this.search || this.site || this.category || this.assetType || this.phase || this.status || this.condition || this.criticality || this.disposal || this.due || this.life || this.from || this.to);
  }

  clearFilters(): void {
    this.search = this.site = this.category = this.assetType = this.status = this.condition = this.criticality = this.from = this.to = '';
    this.phase = '';
    this.disposal = '';
    this.due = '';
    this.life = '';
    this.page = 1;
  }

  ageLabel(r: LifecycleRow): string {
    if (r.ageYears == null) return '-';
    if (r.ageYears < 1) return `${Math.max(0, Math.round(r.ageYears * 12))} mo`;
    return `${r.ageYears.toFixed(1)} yrs`;
  }

  lifeLabel(r: LifecycleRow): string {
    if (r.usefulLifeYears == null) return '-';
    const y = r.usefulLifeYears;
    return y < 1 ? `${Math.round(y * 12)} mo` : `${Number.isInteger(y) ? y : y.toFixed(1)} yrs`;
  }

  lifeTone(r: LifecycleRow): string {
    const p = r.lifeUsedPct;
    return p == null ? '' : p >= 100 ? 'meter-fill--critical' : p >= NEAR_EOL ? 'meter-fill--warn' : 'meter-fill--good';
  }

  phaseTone(p: Phase): string {
    return p === 'In Service' ? 'pill--good' : p === 'Commissioning' || p === 'Acquisition' || p === 'Planning' ? 'pill--warn' : p === 'Retired' ? 'pill--serious' : p === 'Disposal' ? 'pill--critical' : '';
  }

  disposalTone(d: DisposalState): string {
    return d === 'Completed' ? 'pill--critical' : d === 'Approved' ? 'pill--serious' : d === 'Pending Approval' || d === 'Draft' ? 'pill--warn' : '';
  }

  /** An asset with an active or completed disposal request can't get another one. */
  canRequestDisposal(r: LifecycleRow): boolean {
    return r.disposal === 'Not Started';
  }

  // ===== Details panel and navigation =====

  openDetail(r: LifecycleRow): void {
    this.detail = r;
  }

  viewLifecycle(r: LifecycleRow): void {
    this.router.navigate(['/assets/lifecycle/asset'], { queryParams: { tag: r.asset.assetTag || r.asset.assetRegistrationId } });
  }

  /** Disposal: the New Disposal request opens with this asset's tag filled in. */
  requestDisposal(r: LifecycleRow): void {
    if (!this.canRequestDisposal(r)) return;
    this.router.navigate(['/assets/disposal/new-disposal'], { queryParams: { assets: r.asset.assetTag || r.asset.assetRegistrationId } });
  }

  openDisposal(r: LifecycleRow): void {
    if (r.disposalRequest) this.router.navigate(['/assets/disposal/new-disposal'], { queryParams: { id: r.disposalRequest.id } });
  }

  // ===== Lifecycle Events =====

  get filteredEvents(): LifecycleEvent[] {
    const q = this.search.trim().toLowerCase();
    const from = this.from ? `${this.from}T00:00:00` : '';
    const to = this.to ? `${this.to}T23:59:59` : '';
    return this.events.filter(
      (e) =>
        (!q || [e.assetTag, e.assetName, e.title, e.kind, e.by].some((v) => v.toLowerCase().includes(q))) &&
        (!from || e.date >= from) &&
        (!to || e.date <= to),
    );
  }

  // ===== Lifecycle Analytics =====

  get stageDistribution(): { label: string; count: number; pct: number }[] {
    const total = this.rows.length || 1;
    return [...PHASES, 'Not tracked' as Phase].map((p) => {
      const count = this.count((r) => r.phase === p);
      return { label: p, count, pct: Math.round((count / total) * 100) };
    });
  }

  get ageProfile(): { label: string; count: number; pct: number }[] {
    const bands: [string, (y: number) => boolean][] = [
      ['Under 2 years', (y) => y < 2], ['2–5 years', (y) => y >= 2 && y < 5], ['5–10 years', (y) => y >= 5 && y < 10], ['10+ years', (y) => y >= 10],
    ];
    const total = this.rows.length || 1;
    const out = bands.map(([label, test]) => {
      const count = this.count((r) => r.ageYears != null && test(r.ageYears));
      return { label, count, pct: Math.round((count / total) * 100) };
    });
    const unknown = this.count((r) => r.ageYears == null);
    return [...out, { label: 'No acquisition date', count: unknown, pct: Math.round((unknown / total) * 100) }];
  }

  get nearingEndOfLife(): LifecycleRow[] {
    return this.rows.filter((r) => r.lifeUsedPct != null && r.lifeUsedPct >= NEAR_EOL).sort((a, b) => (b.lifeUsedPct ?? 0) - (a.lifeUsedPct ?? 0)).slice(0, 10);
  }

  /** Planned replacements per year (overdue first), from the useful life. */
  get replacementForecast(): { label: string; count: number; pct: number }[] {
    const by = new Map<string, number>();
    const now = new Date().getFullYear();
    for (const r of this.rows) {
      if (!r.plannedReplacement) continue;
      const y = new Date(r.plannedReplacement).getFullYear();
      const key = new Date(r.plannedReplacement).getTime() < Date.now() ? 'Overdue' : `${y}`;
      if (key === 'Overdue' || y <= now + 9) by.set(key, (by.get(key) ?? 0) + 1);
    }
    const max = Math.max(1, ...by.values());
    return [...by.entries()].sort((a, b) => (a[0] === 'Overdue' ? -1 : b[0] === 'Overdue' ? 1 : a[0].localeCompare(b[0]))).map(([label, count]) => ({ label, count, pct: Math.round((count / max) * 100) }));
  }

  get disposalBacklog(): { label: string; count: number }[] {
    return (['Draft', 'Pending Approval', 'Approved', 'Completed'] as DisposalState[]).map((s) => ({ label: s, count: this.count((r) => r.disposal === s) }));
  }

  // ===== Export =====

  exportCsv(): void {
    const cell = (v: unknown) => `"${`${v ?? ''}`.replace(/"/g, '""')}"`;
    const head = ['Asset ID', 'Asset Tag', 'Asset Name', 'Category', 'Type', 'Site', 'Acquisition Date', 'Commissioning Date', 'Lifecycle Stage', 'Phase', 'Operational Status', 'Condition', 'Age (years)', 'Expected Useful Life (years)', 'Life Used %', 'Planned Replacement', 'Disposal Status'];
    const lines = this.filtered.map((r) =>
      [r.asset.assetRegistrationId, r.asset.assetTag, r.asset.assetName, r.asset.category, r.asset.assetType, r.asset.siteName, r.acquisitionDate, r.commissioningDate, stageLabel(r.stageKey), r.phase, r.asset.status, r.asset.condition,
        r.ageYears?.toFixed(1), r.usefulLifeYears, r.lifeUsedPct, r.plannedReplacement, r.disposal].map(cell).join(','),
    );
    const blob = new Blob([[head.map(cell).join(','), ...lines].join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'asset-lifecycle.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.detail = null;
  }

  trackRow = (_: number, r: LifecycleRow) => r.asset.assetRegistrationId;
}
