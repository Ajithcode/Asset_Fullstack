import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  ChartColumn,
  ClipboardList,
  Columns3,
  Download,
  Eye,
  FilterX,
  History,
  Layers,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  TriangleAlert,
  X,
} from 'lucide-angular';
import { Subject, Subscription, debounceTime, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../services/asset-api';
import { FilterSelect, FilterSelectOption } from '../../shared/filter-select/filter-select';
import {
  CriticalityApiService,
  CriticalityAssessmentRow,
  CriticalityAssetRow,
  CriticalityDistribution,
  CriticalityFilterOptions,
  CriticalityFilters,
  CriticalityHistoryFilters,
  CriticalityKpis,
  EMPTY_FILTERS,
} from '../criticality-api.service';

type View = 'assets' | 'history';

interface Column {
  key: string;
  label: string;
  /** Server sort key; columns without one are not sortable. */
  sort?: string;
}

interface Kpi {
  key: string;
  label: string;
  value: number;
  color?: string | null;
  icon: typeof Layers;
  /** Filters this card sets when clicked (the other card filters are cleared). */
  apply: Partial<Pick<CriticalityFilters, 'level' | 'approvalStatus' | 'overdue'>>;
}

/** Table columns in display order; keys match the export columns of the API. */
const COLUMNS: Column[] = [
  { key: 'assetId', label: 'Asset ID', sort: 'assetId' },
  { key: 'assetName', label: 'Asset Name', sort: 'assetName' },
  { key: 'category', label: 'Category / Type', sort: 'category' },
  { key: 'site', label: 'Site / Location', sort: 'site' },
  { key: 'department', label: 'Department / Owner', sort: 'department' },
  { key: 'operationalStatus', label: 'Operational Status', sort: 'operationalStatus' },
  { key: 'condition', label: 'Current Condition', sort: 'condition' },
  { key: 'level', label: 'Criticality Level', sort: 'level' },
  { key: 'score', label: 'Score', sort: 'score' },
  { key: 'assessmentDate', label: 'Assessment Date', sort: 'assessmentDate' },
  { key: 'assessedBy', label: 'Assessed By', sort: 'assessedBy' },
  { key: 'nextReviewDate', label: 'Next Review Date', sort: 'nextReviewDate' },
  { key: 'approvalStatus', label: 'Approval Status', sort: 'approvalStatus' },
];

/** Table column → export columns (a combined table column exports as two spreadsheet columns). */
const EXPORT_KEYS: Record<string, string[]> = {
  category: ['category', 'assetType'],
  site: ['site', 'location'],
  department: ['department', 'owner'],
};

const DEFAULT_HIDDEN = ['condition', 'assessedBy'];
const COLUMNS_STORAGE_KEY = 'a360.assetCriticality.columns';
const PAGE_SIZE = 20;

const STATUS_LABELS: Record<string, string> = {
  PendingApproval: 'Pending Approval',
  Submitted: 'Submitted',
  Draft: 'Draft',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
  Unassessed: 'Unassessed',
};

/** Fallback colours when the MDM level has none. */
const LEVEL_COLORS: Record<string, string> = { critical: '#C62828', high: '#EF6C00', medium: '#F9A825', low: '#2E7D32' };

/**
 * Assets › Asset Criticality (menu page): every registered asset with its approved criticality, KPI cards, filters,
 * the Assessment History tab, the level distribution and an Excel export. One asset's details open at
 * /assets/criticality/asset/:tag.
 */
@Component({
  selector: 'app-criticality-landing',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FilterSelect],
  templateUrl: './criticality-landing.html',
  styleUrls: [
    '../../asset-status-condition/asset-status-condition.css',
    '../../asset-status-condition/asset-status-condition-panels.css',
    './criticality-landing.css',
  ],
})
export class CriticalityLanding implements OnInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, ChartColumn, ClipboardList, Columns3, Download, Eye, FilterX, History, Layers, Plus, RefreshCw,
    Search, ShieldAlert, TriangleAlert, X,
  };
  readonly columns = COLUMNS;
  readonly pageSize = PAGE_SIZE;

  private readonly api = inject(CriticalityApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly search$ = new Subject<void>();
  private subs: Subscription[] = [];
  private listSub?: Subscription;
  private summarySub?: Subscription;

  view: View = 'assets';
  filters: CriticalityFilters = { ...EMPTY_FILTERS };
  history: CriticalityHistoryFilters = { search: '', assetRegistrationId: '', approvalStatus: '', assessor: '', from: '', to: '' };
  /** Asset tag shown in the history filter chip when the list is limited to one asset. */
  historyAssetLabel = '';
  sortBy = 'assetId';
  sortDir: 'asc' | 'desc' = 'asc';
  page = 1;

  options: CriticalityFilterOptions | null = null;
  kpis: Kpi[] = [];
  kpiData: CriticalityKpis | null = null;
  distribution: CriticalityDistribution | null = null;
  rows: CriticalityAssetRow[] = [];
  assessments: CriticalityAssessmentRow[] = [];
  total = 0;
  loading = false;
  loadError = '';
  exporting = false;
  showAnalytics = true;
  showColumnMenu = false;
  hidden = new Set<string>(DEFAULT_HIDDEN);

  /** Row opened in the side panel. */
  selected: CriticalityAssetRow | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.hidden = loadHiddenColumns();
    const q = this.route.snapshot.queryParamMap;
    if (q.get('view') === 'history') this.view = 'history';
    this.subs.push(this.search$.pipe(debounceTime(300)).subscribe(() => this.reload()));
    this.subs.push(this.api.filterOptions().subscribe({ next: (o) => (this.options = o), error: () => (this.options = null) }));
    this.reload();
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    this.listSub?.unsubscribe();
    this.summarySub?.unsubscribe();
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  reload(page = 1): void {
    this.page = page;
    this.loadError = '';
    this.loading = true;
    this.listSub?.unsubscribe();

    if (this.view === 'assets') {
      this.loadSummary();
      this.listSub = this.api.assets(this.filters, page, PAGE_SIZE, this.sortBy, this.sortDir).subscribe({
        next: (res) => {
          this.rows = res.items ?? [];
          this.total = res.totalCount ?? 0;
          this.loading = false;
          if (this.selected) this.selected = this.rows.find((r) => r.assetRegistrationId === this.selected!.assetRegistrationId) ?? this.selected;
        },
        error: (err) => this.fail(err),
      });
    } else {
      this.listSub = this.api.assessments(this.history, page, PAGE_SIZE).subscribe({
        next: (res) => {
          this.assessments = res.items ?? [];
          this.total = res.totalCount ?? 0;
          this.loading = false;
        },
        error: (err) => this.fail(err),
      });
    }
  }

  /** KPI cards and distribution use the same filters as the table. */
  private loadSummary(): void {
    this.summarySub?.unsubscribe();
    this.summarySub = forkJoin({ kpis: this.api.kpis(this.filters), distribution: this.api.distribution(this.filters) }).subscribe({
      next: ({ kpis, distribution }) => {
        this.kpiData = kpis;
        this.kpis = buildKpis(kpis);
        this.distribution = distribution;
      },
      error: () => {
        this.kpiData = null;
        this.kpis = [];
        this.distribution = null;
      },
    });
  }

  private fail(err: unknown): void {
    this.loading = false;
    this.rows = [];
    this.assessments = [];
    this.total = 0;
    this.loadError = apiErrorMessage(err, 'Could not load asset criticality.');
  }

  refresh(): void {
    this.reload(this.page);
  }

  onSearch(): void {
    this.search$.next();
  }

  setView(view: View): void {
    if (this.view === view) return;
    this.view = view;
    this.total = 0;
    this.selected = null;
    this.reload();
  }

  // ===== Filters =====

  pickKpi(k: Kpi): void {
    this.filters = { ...this.filters, level: '', approvalStatus: '', overdue: false, ...k.apply };
    this.view = 'assets';
    this.reload();
  }

  isKpiActive(k: Kpi): boolean {
    const f = this.filters;
    const want = { level: '', approvalStatus: '', overdue: false, ...k.apply };
    return f.level === want.level && f.approvalStatus === want.approvalStatus && f.overdue === want.overdue;
  }

  get hasFilters(): boolean {
    return Object.entries(this.filters).some(([, v]) => v === true || (typeof v === 'string' && v !== ''));
  }

  clearFilters(): void {
    this.filters = { ...EMPTY_FILTERS };
    this.history = { search: '', assetRegistrationId: '', approvalStatus: '', assessor: '', from: '', to: '' };
    this.historyAssetLabel = '';
    this.reload();
  }

  get levelOptions(): FilterSelectOption[] {
    return [...(this.options?.levels ?? []).map((l) => l.name), 'Unassessed'];
  }

  get statusOptions(): FilterSelectOption[] {
    return this.options?.approvalStatuses ?? [];
  }

  get historyStatusOptions(): FilterSelectOption[] {
    return ['Draft', 'Submitted', 'PendingApproval', 'Approved', 'Rejected', 'Cancelled'].map((s) => ({ value: s, label: STATUS_LABELS[s] }));
  }

  get siteOptions(): FilterSelectOption[] {
    return this.options?.sites ?? [];
  }

  sort(column: Column): void {
    if (!column.sort) return;
    if (this.sortBy === column.sort) this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    else {
      this.sortBy = column.sort;
      // Severity, score and dates read best highest / latest first.
      this.sortDir = ['level', 'score', 'assessmentDate', 'nextReviewDate'].includes(column.sort) ? 'desc' : 'asc';
    }
    this.reload();
  }

  // ===== Columns =====

  get visibleColumns(): Column[] {
    return COLUMNS.filter((c) => !this.hidden.has(c.key));
  }

  toggleColumn(key: string): void {
    if (this.hidden.has(key)) this.hidden.delete(key);
    else if (this.visibleColumns.length > 1) this.hidden.add(key);
    saveHiddenColumns(this.hidden);
  }

  resetColumns(): void {
    this.hidden = new Set(DEFAULT_HIDDEN);
    saveHiddenColumns(this.hidden);
  }

  // ===== Actions =====

  openAsset(row: CriticalityAssetRow, assess = false): void {
    const tag = row.assetTag || row.assetRegistrationId;
    this.router.navigate(['/assets/criticality/asset', tag], assess ? { queryParams: { assess: 1 } } : {});
  }

  showHistory(row: CriticalityAssetRow): void {
    this.history = { search: '', assetRegistrationId: row.assetRegistrationId, approvalStatus: '', assessor: '', from: '', to: '' };
    this.historyAssetLabel = row.assetTag || row.assetRegistrationId;
    this.selected = null;
    this.view = 'history';
    this.reload();
  }

  clearHistoryAsset(): void {
    this.history = { ...this.history, assetRegistrationId: '' };
    this.historyAssetLabel = '';
    this.reload();
  }

  openAssessment(a: CriticalityAssessmentRow): void {
    this.router.navigate(['/assets/criticality/asset', a.assetTag || a.assetRegistrationId]);
  }

  select(row: CriticalityAssetRow): void {
    this.selected = this.selected?.assetRegistrationId === row.assetRegistrationId ? null : row;
  }

  exportExcel(): void {
    if (this.exporting) return;
    this.exporting = true;
    const keys = this.visibleColumns.flatMap((c) => EXPORT_KEYS[c.key] ?? [c.key]);
    this.api.exportXlsx(this.filters, keys, this.sortBy, this.sortDir).subscribe({
      next: (blob) => {
        this.exporting = false;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `asset-criticality-${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      },
      error: () => {
        this.exporting = false;
        this.showToast('Export failed. Please try again.');
      },
    });
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }

  // ===== Display helpers =====

  levelColor(level: string | null | undefined): string {
    if (!level) return '#9ca3af';
    const fromMaster = this.options?.levels.find((l) => l.name.toLowerCase() === level.toLowerCase())?.color;
    return fromMaster || LEVEL_COLORS[level.toLowerCase()] || '#6b7280';
  }

  statusLabel(status: string | null | undefined): string {
    return (status && STATUS_LABELS[status]) || status || '-';
  }

  statusTone(status: string | null | undefined): string {
    switch (status) {
      case 'Approved': return 'tone-ok';
      case 'Rejected': return 'tone-bad';
      case 'Submitted':
      case 'PendingApproval': return 'tone-warn';
      case 'Draft': return 'tone-info';
      default: return 'tone-muted';
    }
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '-';
    const d = new Date(value);
    return isNaN(d.getTime()) ? '-' : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  /** Bar width in the distribution panel, relative to the largest bar. */
  barWidth(count: number, max: number): string {
    return max > 0 ? `${Math.max(2, Math.round((count / max) * 100))}%` : '0%';
  }

  get maxLevelCount(): number {
    return Math.max(0, ...(this.distribution?.byLevel ?? []).map((l) => l.count));
  }

  get topCategories(): CriticalityDistribution['byCategory'] {
    return (this.distribution?.byCategory ?? []).slice(0, 8);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / PAGE_SIZE));
  }

  get rangeLabel(): string {
    if (!this.total) return '';
    const from = (this.page - 1) * PAGE_SIZE + 1;
    return `Showing ${from}–${Math.min(this.page * PAGE_SIZE, this.total)} of ${this.total}`;
  }

  trackRow = (_: number, r: CriticalityAssetRow) => r.assetRegistrationId;
  trackAssessment = (_: number, a: CriticalityAssessmentRow) => a.id;
}

function buildKpis(k: CriticalityKpis): Kpi[] {
  const levels = [...(k.levels ?? [])].sort((a, b) => b.rank - a.rank);
  return [
    { key: 'total', label: 'Total Assets', value: k.total, icon: Layers, apply: {} },
    ...levels.map((l) => ({
      key: 'level:' + l.name,
      label: l.name,
      value: l.count,
      color: l.color || LEVEL_COLORS[l.name.toLowerCase()],
      icon: ShieldAlert,
      apply: { level: l.name },
    })),
    { key: 'unassessed', label: 'Unassessed', value: k.unassessed, icon: ClipboardList, color: '#9ca3af', apply: { level: 'Unassessed' } },
    { key: 'pending', label: 'Pending Approvals', value: k.pendingApprovals, icon: History, apply: { approvalStatus: 'Pending' } },
    { key: 'overdue', label: 'Reassessment Overdue', value: k.reassessmentOverdue, icon: TriangleAlert, color: '#dc2626', apply: { overdue: true } },
  ];
}

/** Column choice is a per-browser convenience; storage may be unavailable (private mode). */
function loadHiddenColumns(): Set<string> {
  try {
    const raw = localStorage.getItem(COLUMNS_STORAGE_KEY);
    if (raw) {
      const keys = (JSON.parse(raw) as unknown[]).filter((k): k is string => typeof k === 'string' && COLUMNS.some((c) => c.key === k));
      if (keys.length < COLUMNS.length) return new Set(keys);
    }
  } catch {
    /* ignore */
  }
  return new Set(DEFAULT_HIDDEN);
}

function saveHiddenColumns(hidden: Set<string>): void {
  try {
    localStorage.setItem(COLUMNS_STORAGE_KEY, JSON.stringify([...hidden]));
  } catch {
    /* ignore */
  }
}
