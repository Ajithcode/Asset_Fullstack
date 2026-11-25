import { Component, DestroyRef, ElementRef, HostListener, OnDestroy, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  ChartPie,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Download,
  EllipsisVertical,
  Eye,
  Grid3x3,
  LayoutGrid,
  List,
  Pencil,
  Plus,
  Printer,
  RefreshCw,
  Search,
  ShieldAlert,
  TriangleAlert,
  X,
} from 'lucide-angular';
import { Subscription, catchError, combineLatest, forkJoin, from, of } from 'rxjs';
import { ASSET_TYPE_ICONS, AssetHeader, AssetTabLink, AuthService, assetTabs, pushToast } from 'shared-ui';
import { CriticalityCriterion, CriticalityImpact, CriticalityLevel, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';
import { apiErrorMessage } from '../services/asset-api';
import { AssetLookupService, EMPTY_HEADER } from '../asset-status-condition/asset-lookup.service';
import {
  AssetCriticalityService,
  CriticalityAssessment,
  CriticalityOptions,
  MITIGATION_STATUSES,
  MitigationAction,
  MitigationStatus,
  RISK_SCALE,
  RelatedAssetLink,
} from './asset-criticality.service';
import { ExportMenu, ExportRequest } from 'shared-ui';

type SubTab = 'details' | 'impact' | 'mitigation' | 'history' | 'related';
type ViewMode = 'list' | 'grid';

/** The asset's current assessment with its mitigation actions and earlier assessments. */
interface CriticalityView extends CriticalityAssessment {
  /** Failure impact has no Asset API yet; empty until one serves it. */
  impact: CriticalityImpact | null;
  mitigations: MitigationAction[];
  /** Earlier assessments, newest first. */
  history: CriticalityAssessment[];
}

/** One row of the Asset Criticality List: an asset's current assessment. */
type ListRow = CriticalityAssessment;

interface RelatedRow extends RelatedAssetLink {
  crit: CriticalityAssessment | null;
}

interface AssessmentForm {
  date: string;
  assessedBy: string;
  approvedBy: string;
  likelihood: number | null;
  consequence: number | null;
  criteria: CriticalityCriterion[];
  remarks: string;
}

type InfoForm = Pick<
  CriticalityAssessment,
  'method' | 'reviewFrequency' | 'validUntil' | 'nextReview' | 'assessedBy' | 'approvedBy' | 'department' | 'assetGroup' | 'remarks'
>;

interface MitigationForm {
  action: string;
  owner: string;
  dueDate: string;
  status: MitigationStatus | null;
  reducesTo: CriticalityLevel | null;
}

const LEVELS: CriticalityLevel[] = ['Critical', 'High', 'Medium', 'Low'];
/** Matrix axis labels, index 0 = 1 (very low). */
const SCALE = RISK_SCALE;
/** Criteria the first assessment of an asset starts with, until GET /api/asset-criticality/options answers. */
const DEFAULT_CRITERIA = [
  { name: 'Operational Impact', weight: 25 },
  { name: 'Safety Impact', weight: 20 },
  { name: 'Financial Impact', weight: 20 },
  { name: 'Compliance / Regulatory', weight: 15 },
  { name: 'Environmental Impact', weight: 10 },
  { name: 'Reputation Impact', weight: 10 },
];

/** Radar geometry in SVG user units. */
const RADAR = { cx: 210, cy: 120, r: 78, labelR: 104 };

export function criticalityLevel(score: number): CriticalityLevel {
  return score >= 80 ? 'Critical' : score >= 60 ? 'High' : score >= 40 ? 'Medium' : 'Low';
}

function weighted(c: CriticalityCriterion): number {
  return (c.weight * c.score) / 5;
}

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function addMonths(iso: string, months: number): string {
  const [y, m, d] = iso.split('-').map(Number);
  return toIsoDate(new Date(y, m - 1 + months, d));
}

@Component({
  selector: 'app-asset-criticality',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, ExportMenu],
  templateUrl: './asset-criticality.html',
  // Shares the Status & Condition page's header, tab, table and modal styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-criticality.css',
  ],
})
export class AssetCriticality implements OnDestroy {
  readonly icons = {
    ChartPie, ChevronDown, ChevronLeft, ChevronRight, ClipboardList, Download, EllipsisVertical, Eye, Grid3x3, LayoutGrid, List,
    Pencil, Plus, Printer, RefreshCw, Search, ShieldAlert, TriangleAlert, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly levels = LEVELS;
  readonly scale = SCALE;
  readonly radar = RADAR;
  readonly scoreSteps = [1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5];
  readonly mitigationStatuses: MitigationStatus[] = MITIGATION_STATUSES;
  readonly assessmentMethods = ['Risk Matrix (Weighted)', 'Risk Matrix (Simple)', 'FMECA', 'Expert Judgement'];
  readonly reviewFrequencies = ['Every 3 Months', 'Every 6 Months', 'Every 12 Months'];

  @ViewChild('subtabsEl') private subtabsEl?: ElementRef<HTMLElement>;

  private readonly router = inject(Router);
  private readonly lookup = inject(AssetLookupService);
  private readonly api = inject(AssetCriticalityService);
  private readonly auth = inject(AuthService);
  private loadSub?: Subscription;
  private options: CriticalityOptions | null = null;

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = EMPTY_HEADER;
  /** assetRegistrationId of the asset, the key of the Asset API. */
  assetId = '';
  notFound = false;
  /** Set when the asset's criticality could not be read from the Asset API. */
  loadError = '';
  /** True while a dialog is being saved. */
  saving = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Criticality');

  loading = false;
  crit: CriticalityView | null = null;
  related: RelatedRow[] = [];
  subTab: SubTab = 'details';
  readonly subTabs: { key: SubTab; label: string }[] = [
    { key: 'details', label: 'Assessment Details' },
    { key: 'impact', label: 'Impact Analysis' },
    { key: 'mitigation', label: 'Risk Mitigation' },
    { key: 'history', label: 'Review History' },
    { key: 'related', label: 'Related Assets' },
  ];

  // ===== Asset Criticality List =====
  listLoading = true;
  listError = '';
  rows: ListRow[] = [];
  search = '';
  categoryFilter = '';
  siteFilter = '';
  levelFilter: CriticalityLevel | '' = '';
  view: ViewMode = 'list';
  readonly pageSizes = [10, 20, 50];
  pageSize = 10;
  page = 1;
  readonly checked = new Set<string>();
  categories: string[] = [];
  sites: string[] = [];

  // ===== Menus & modals =====
  actionsOpen = false;
  rowMenuTag: string | null = null;
  assessForm: AssessmentForm | null = null;
  infoForm: InfoForm | null = null;
  mitigationForm: MitigationForm | null = null;
  formError = '';
  /** Asset whose re-assessment dialog opens once its page has loaded. */
  private assessOnLoad: string | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  readonly today = toIsoDate(new Date());

  constructor() {
    // /assets/criticality/asset/:tag (old ?tag= links are redirected here); ?assess=1 opens the Re-assess dialog.
    const route = inject(ActivatedRoute);
    combineLatest([route.paramMap, route.queryParamMap])
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(([params, query]) => {
        const tag = params.get('tag') ?? query.get('tag');
        if (query.get('assess') === '1' && tag) this.assessOnLoad = tag;
        this.load(tag);
      });
    this.loadList();
    this.api.getOptions().subscribe({ next: (o) => (this.options = o), error: () => (this.options = null) });
  }

  ngOnDestroy(): void {
    this.loadSub?.unsubscribe();
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  private load(tag: string | null): void {
    this.requestedTag = tag ?? '';
    this.notFound = false;
    this.loadError = '';
    this.crit = null;
    this.related = [];
    this.subTab = 'details';
    this.closeMenus();
    this.assessForm = this.infoForm = this.mitigationForm = null;
    this.loading = true;
    this.loadSub?.unsubscribe();
    this.loadSub = this.lookup.resolve(tag).subscribe({
      next: (found) => {
        if (!found) {
          this.loading = false;
          this.notFound = true;
          return;
        }
        this.asset = found.header;
        this.assetId = found.id;
        this.loadSub = forkJoin({
          history: this.api.getHistory(found.id),
          mitigations: this.api.getMitigations(found.id),
          related: this.api.getRelated(found.id).pipe(catchError(() => of([] as RelatedAssetLink[]))),
          detail: from(loadAssetDetail(found.header.tag)),
        }).subscribe({
          next: ({ history, mitigations, related, detail }) => {
            this.loading = false;
            const current = history.find((h) => h.isCurrent) ?? history[0];
            this.crit = current
              ? {
                  ...current,
                  impact: detail ? cloneDetail(detail.criticality.impact) : null,
                  mitigations,
                  history: history.filter((h) => h.id !== current.id),
                }
              : null;
            this.related = related.map((r) => ({ ...r, crit: this.rowFor(r.assetRegistrationId) }));
            if (this.assessOnLoad === this.asset.tag) {
              this.assessOnLoad = null;
              this.openAssessment();
            }
          },
          error: (err) => this.failLoad(err),
        });
      },
      error: (err) => this.failLoad(err),
    });
  }

  private failLoad(err: unknown): void {
    this.loading = false;
    this.loadError = apiErrorMessage(err, 'Could not load the criticality assessment.');
    this.showToast(this.loadError);
  }

  /** Current assessment of an asset from the list, if it has one. */
  private rowFor(assetRegistrationId: string | null): CriticalityAssessment | null {
    return assetRegistrationId ? this.rows.find((r) => r.assetRegistrationId === assetRegistrationId) ?? null : null;
  }

  /** Loads the current assessment of every asset for the list. */
  private loadList(): void {
    this.listLoading = true;
    this.listError = '';
    this.api.getList().subscribe({
      next: (rows) => {
        this.setRows(rows);
        this.listLoading = false;
      },
      error: (err) => {
        this.listLoading = false;
        this.listError = apiErrorMessage(err, 'Could not load the asset criticality list.');
      },
    });
  }

  private setRows(rows: ListRow[]): void {
    this.rows = rows;
    this.categories = [...new Set(rows.map((r) => r.category).filter(Boolean))].sort();
    this.sites = [...new Set(rows.map((r) => r.location).filter(Boolean))].sort();
    for (const r of this.related) r.crit = this.rowFor(r.assetRegistrationId);
  }

  private get currentUser(): string {
    return this.auth.getUserEmail() ?? '';
  }

  // ===== Header helpers =====

  get headerLevel(): CriticalityLevel {
    return this.crit?.level ?? this.asset.criticality;
  }

  levelTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  statusTone(status: string): string {
    return status === 'Active' || status === 'Checked Out' ? 'good' : status === 'Disposed' ? 'critical' : 'warn';
  }

  conditionTone(condition: string): string {
    return condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : 'critical';
  }

  /** Colour of a 1-5 criterion score. */
  scoreTone(score: number): string {
    return score >= 4.5 ? 'critical' : score >= 3.5 ? 'serious' : score >= 2.5 ? 'warn' : 'good';
  }

  isOverdue(date: string): boolean {
    return !!date && date < this.today;
  }

  // ===== Assessment details =====

  weighted(c: CriticalityCriterion): number {
    return weighted(c);
  }

  get totalWeight(): number {
    return this.crit?.criteria.reduce((s, c) => s + c.weight, 0) ?? 0;
  }

  // ===== Radar (score breakdown) =====

  private point(i: number, fraction: number): { x: number; y: number } {
    const n = this.crit?.criteria.length || 6;
    const angle = -Math.PI / 2 + (i * 2 * Math.PI) / n;
    return { x: RADAR.cx + Math.cos(angle) * RADAR.r * fraction, y: RADAR.cy + Math.sin(angle) * RADAR.r * fraction };
  }

  /** Grid rings at 20 % steps. */
  get radarRings(): string[] {
    const n = this.crit?.criteria.length ?? 0;
    return [0.2, 0.4, 0.6, 0.8, 1].map((f) =>
      Array.from({ length: n }, (_, i) => this.point(i, f)).map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '),
    );
  }

  get radarAxes(): { x: number; y: number }[] {
    return (this.crit?.criteria ?? []).map((_, i) => this.point(i, 1));
  }

  /** The score polygon; each criterion is drawn on a 0-10 scale (score × 2). */
  get radarPoints(): { x: number; y: number; value: number }[] {
    return (this.crit?.criteria ?? []).map((c, i) => ({ ...this.point(i, c.score / 5), value: c.score * 2 }));
  }

  get radarPolygon(): string {
    return this.radarPoints.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  }

  get radarLabels(): { x: number; y: number; name: string; value: number; anchor: string }[] {
    const n = this.crit?.criteria.length ?? 0;
    return (this.crit?.criteria ?? []).map((c, i) => {
      const angle = -Math.PI / 2 + (i * 2 * Math.PI) / n;
      const x = RADAR.cx + Math.cos(angle) * RADAR.labelR;
      const y = RADAR.cy + Math.sin(angle) * RADAR.labelR;
      const cos = Math.cos(angle);
      return { x, y, name: c.name, value: c.score * 2, anchor: Math.abs(cos) < 0.2 ? 'middle' : cos > 0 ? 'start' : 'end' };
    });
  }

  showDetails(): void {
    this.subTab = 'details';
    this.subtabsEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ===== Matrix =====

  /** Rows from very high consequence (5) down to very low (1). */
  readonly matrixRows = [5, 4, 3, 2, 1];
  readonly matrixCols = [1, 2, 3, 4, 5];

  heatTone(likelihood: number, consequence: number): string {
    const risk = likelihood * consequence;
    return risk >= 16 ? 'heat-6' : risk >= 12 ? 'heat-5' : risk >= 8 ? 'heat-4' : risk >= 5 ? 'heat-3' : risk >= 3 ? 'heat-2' : 'heat-1';
  }

  // ===== Mitigation / history / related =====

  get openMitigations(): number {
    return this.crit?.mitigations.filter((m) => m.status !== 'Completed' && m.status !== 'Cancelled').length ?? 0;
  }

  mitigationTone(status: MitigationStatus): string {
    return status === 'Completed' ? 'good' : status === 'Cancelled' ? 'muted' : status === 'In Progress' ? 'warn' : 'serious';
  }

  setMitigationStatus(m: MitigationAction, status: MitigationStatus): void {
    const previous = m.status;
    m.status = status;
    this.api.setMitigationStatus(m.id, status).subscribe({
      next: (saved) => {
        Object.assign(m, saved);
        this.showToast(`"${m.action}" marked ${status}.`);
      },
      error: (err) => {
        m.status = previous;
        this.showToast(apiErrorMessage(err, 'The status could not be changed.'));
      },
    });
  }

  openMitigation(): void {
    this.closeMenus();
    this.formError = '';
    this.mitigationForm = { action: '', owner: this.currentUser, dueDate: addMonths(this.today, 1), status: null, reducesTo: null };
  }

  saveMitigation(): void {
    const f = this.mitigationForm;
    const crit = this.crit;
    if (!f || !crit || this.saving) return;
    if (!f.action.trim() || !f.owner.trim() || !f.dueDate) {
      this.formError = 'Enter the action, owner and due date.';
      return;
    }
    if (!f.status || !f.reducesTo) {
      this.formError = 'Select the status and expected criticality.';
      return;
    }
    this.saving = true;
    this.formError = '';
    this.api.createMitigation(this.assetId, { ...f, status: f.status, reducesTo: f.reducesTo }).subscribe({
      next: (action) => {
        this.saving = false;
        crit.mitigations = [...crit.mitigations, action];
        this.mitigationForm = null;
        this.showToast('Mitigation action added.');
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, 'The mitigation action could not be added.');
      },
    });
  }

  viewAsset(tag: string, assess = false): void {
    this.closeMenus();
    if (assess) this.assessOnLoad = tag;
    this.router.navigate(['/assets/criticality/asset', tag]).then(() => {
      if (tag === this.asset.tag && assess && !this.loading) {
        this.assessOnLoad = null;
        this.openAssessment();
      }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  // ===== Assessment =====

  /** Assess / Re-assess opens the assessment form (Draft -> Submit -> Approve); nothing is saved on this page. */
  openAssessment(): void {
    this.closeMenus();
    if (!this.assetId || this.notFound) return;
    this.router.navigate(['/assets/criticality/assess', this.assetId]);
  }

  // ===== Asset Criticality List =====

  get filtered(): ListRow[] {
    const q = this.search.trim().toLowerCase();
    return this.rows.filter(
      (r) =>
        (!this.categoryFilter || r.category === this.categoryFilter) &&
        (!this.siteFilter || r.location === this.siteFilter) &&
        (!this.levelFilter || r.level === this.levelFilter) &&
        (!q || [r.tag, r.name, r.category, r.location].some((v) => v.toLowerCase().includes(q))),
    );
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get safePage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get pageRows(): ListRow[] {
    const start = (this.safePage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeLabel(): string {
    const total = this.filtered.length;
    if (!total) return 'No assets';
    const start = (this.safePage - 1) * this.pageSize + 1;
    return `Showing ${start} - ${Math.min(start + this.pageSize - 1, total)} of ${total} assets`;
  }

  rowNumber(index: number): number {
    return (this.safePage - 1) * this.pageSize + index + 1;
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.pageCount);
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.tag));
  }

  toggleAll(on: boolean): void {
    this.pageRows.forEach((r) => (on ? this.checked.add(r.tag) : this.checked.delete(r.tag)));
  }

  toggleRow(tag: string, on: boolean): void {
    if (on) this.checked.add(tag);
    else this.checked.delete(tag);
  }

  onFilterChange(): void {
    this.page = 1;
  }

  toggleRowMenu(event: Event, tag: string): void {
    event.stopPropagation();
    this.rowMenuTag = this.rowMenuTag === tag ? null : tag;
    this.actionsOpen = false;
  }

  // ===== Export / print =====

  /** The criticality list: selected rows if any, else every row matching the filters. */
  exportListRequest = (): ExportRequest<ListRow> => ({
    title: 'Asset Criticality List',
    fileBase: 'asset-criticality-list',
    columns: [
      { label: 'Asset ID', key: 'tag' }, { label: 'Asset Name', key: 'name', width: 2 }, { label: 'Category', key: 'category' }, { label: 'Location', key: 'location' },
      { label: 'Criticality Level', key: 'level' }, { label: 'Criticality Score', key: 'score', align: 'right' }, { label: 'Assessment Date', key: 'assessmentDate' },
      { label: 'Next Review', key: 'nextReview' }, { label: 'Status', key: 'assetStatus' },
    ],
    filters: this.checked.size
      ? { Selection: `${this.checked.size} selected` }
      : { Search: this.search, Category: this.categoryFilter, Location: this.siteFilter, Level: this.levelFilter },
    rows: () => (this.checked.size ? this.filtered.filter((r) => this.checked.has(r.tag)) : this.filtered),
  });

  /** The selected asset's assessment (criteria table). */
  exportRequest = (): ExportRequest => {
    return {
      title: `Criticality Assessment - ${this.asset.tag}`,
      fileBase: `${this.asset.tag}-criticality`,
      columns: [
        { label: 'Criteria', key: 'name', width: 2 }, { label: 'Weight (%)', key: 'weight', align: 'right' }, { label: 'Score (1-5)', key: 'score', align: 'right' },
        { label: 'Weighted Score', key: 'weighted', align: 'right' }, { label: 'Justification', key: 'justification', width: 3 },
      ],
      filters: { Asset: `${this.asset.name} (${this.asset.tag})` },
      rows: () => {
        const c = this.crit;
        if (!c) return [];
        return [
          ...c.criteria.map((x) => ({ name: x.name, weight: x.weight, score: x.score, weighted: weighted(x).toFixed(1), justification: x.justification })),
          { name: 'Total', weight: this.totalWeight, score: '', weighted: c.score.toFixed(1), justification: c.level },
        ];
      },
    };
  };

  print(): void {
    this.closeMenus();
    window.print();
  }

  // ===== Menus =====

  toggleActions(event: Event): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
    this.rowMenuTag = null;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = false;
    this.rowMenuTag = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.assessForm = this.infoForm = this.mitigationForm = null;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  private showToast(message: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(message);
  }
}
