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
import { Subscription, catchError, forkJoin, from, of } from 'rxjs';
import { ASSET_TYPE_ICONS, AssetHeader, AssetTabLink, AuthService, assetTabs } from 'shared-ui';
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

type SubTab = 'details' | 'impact' | 'mitigation' | 'history' | 'related';
type ViewMode = 'list' | 'grid';

/** The asset's current assessment with its mitigation actions and earlier assessments. */
interface CriticalityView extends CriticalityAssessment {
  /** DEMO MODE: failure impact has no Asset API yet; from the asset's sample data file when there is one. */
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
  likelihood: number;
  consequence: number;
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
  status: MitigationStatus;
  reducesTo: CriticalityLevel;
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
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
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
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => this.load(params.get('tag')));
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
    const next = this.crit ? LEVELS[Math.min(LEVELS.length - 1, LEVELS.indexOf(this.crit.level) + 1)] : 'Low';
    this.mitigationForm = { action: '', owner: this.currentUser, dueDate: addMonths(this.today, 1), status: 'Open', reducesTo: next };
  }

  saveMitigation(): void {
    const f = this.mitigationForm;
    const crit = this.crit;
    if (!f || !crit || this.saving) return;
    if (!f.action.trim() || !f.owner.trim() || !f.dueDate) {
      this.formError = 'Enter the action, owner and due date.';
      return;
    }
    this.saving = true;
    this.formError = '';
    this.api.createMitigation(this.assetId, f).subscribe({
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
    this.router.navigate(['/assets/criticality'], { queryParams: { tag } }).then(() => {
      if (tag === this.asset.tag && assess && !this.loading) {
        this.assessOnLoad = null;
        this.openAssessment();
      }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  // ===== Re-assessment =====

  openAssessment(): void {
    this.closeMenus();
    if (!this.assetId || this.notFound) return;
    this.formError = '';
    const c = this.crit;
    // The first assessment of an asset starts from the standard criteria
    const criteria: CriticalityCriterion[] = c
      ? c.criteria.map((x) => ({ ...x }))
      : (this.options?.criteria ?? DEFAULT_CRITERIA).map((x) => ({
          key: x.name.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
          name: x.name,
          weight: Number(x.weight),
          score: 3,
          justification: '',
        }));
    this.assessForm = {
      date: this.today,
      assessedBy: this.currentUser,
      approvedBy: c?.approvedBy ?? '',
      likelihood: c?.likelihood ?? 3,
      consequence: c?.consequence ?? 3,
      criteria,
      remarks: '',
    };
  }

  get formScore(): number {
    return Math.round((this.assessForm?.criteria.reduce((s, c) => s + weighted(c), 0) ?? 0) * 10) / 10;
  }

  get formLevel(): CriticalityLevel {
    return criticalityLevel(this.formScore);
  }

  saveAssessment(): void {
    const f = this.assessForm;
    if (!f || this.saving) return;
    if (!f.date || !f.assessedBy.trim() || !f.approvedBy.trim()) {
      this.formError = 'Enter the assessment date, assessed by and approved by.';
      return;
    }
    if (f.criteria.some((c) => !c.justification.trim())) {
      this.formError = 'Give a justification for every criterion.';
      return;
    }
    const old = this.crit;
    const months = old?.reviewFrequency === 'Every 3 Months' ? 3 : old?.reviewFrequency === 'Every 12 Months' ? 12 : 6;
    this.saving = true;
    this.formError = '';
    this.api
      .create(this.assetId, {
        ...f,
        validUntil: addMonths(f.date, 12),
        nextReview: addMonths(f.date, months),
      })
      .subscribe({
        next: (saved) => {
          this.saving = false;
          // The previous assessment moves into the review history
          const { impact = null, mitigations = [], history = [], ...previous } = old ?? {};
          this.crit = {
            ...saved,
            impact,
            mitigations,
            history: old ? [{ ...(previous as CriticalityAssessment), isCurrent: false }, ...history] : [],
          };
          this.setRows([...this.rows.filter((r) => r.assetRegistrationId !== saved.assetRegistrationId), saved].sort((a, b) =>
            a.assetRegistrationId.localeCompare(b.assetRegistrationId),
          ));
          this.assessForm = null;
          this.showToast(
            !old ? `Assessment saved: ${saved.score} / 100 (${saved.level}).`
              : old.level === saved.level ? `Assessment saved: ${saved.score} / 100 (${saved.level}).`
              : `Criticality changed from ${old.level} to ${saved.level}.`,
          );
        },
        error: (err) => {
          this.saving = false;
          this.formError = apiErrorMessage(err, 'The assessment could not be saved.');
        },
      });
  }

  // ===== Additional information =====

  openInfoEdit(): void {
    this.closeMenus();
    if (!this.crit) return;
    this.formError = '';
    const { method, reviewFrequency, validUntil, nextReview, assessedBy, approvedBy, department, assetGroup, remarks } = this.crit;
    this.infoForm = { method, reviewFrequency, validUntil, nextReview, assessedBy, approvedBy, department, assetGroup, remarks };
  }

  saveInfo(): void {
    const f = this.infoForm;
    const crit = this.crit;
    if (!f || !crit || this.saving) return;
    if (!f.validUntil || !f.assessedBy.trim() || !f.approvedBy.trim()) {
      this.formError = 'Enter valid until, assessed by and approved by.';
      return;
    }
    this.saving = true;
    this.formError = '';
    this.api.updateDetails(crit.id, f).subscribe({
      next: (saved) => {
        this.saving = false;
        Object.assign(crit, saved);
        this.setRows(this.rows.map((r) => (r.id === saved.id ? saved : r)));
        this.infoForm = null;
        this.showToast('Additional information saved.');
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, 'The additional information could not be saved.');
      },
    });
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

  exportAssessment(): void {
    this.closeMenus();
    const c = this.crit;
    if (!c) return;
    const lines = [
      ['Criteria', 'Weight (%)', 'Score (1-5)', 'Weighted Score', 'Justification'],
      ...c.criteria.map((x) => [x.name, x.weight, x.score, weighted(x).toFixed(1), x.justification]),
      ['Total', this.totalWeight, '', c.score.toFixed(1), c.level],
    ];
    this.download(lines, `${this.asset.tag}_criticality.csv`);
  }

  exportList(): void {
    this.closeMenus();
    const rows = this.checked.size ? this.filtered.filter((r) => this.checked.has(r.tag)) : this.filtered;
    const lines = [
      ['Asset ID', 'Asset Name', 'Category', 'Location', 'Criticality Level', 'Criticality Score', 'Assessment Date', 'Next Review', 'Status'],
      ...rows.map((r) => [r.tag, r.name, r.category, r.location, r.level, r.score, r.assessmentDate, r.nextReview, r.assetStatus]),
    ];
    this.download(lines, 'asset_criticality_list.csv');
  }

  private download(lines: (string | number)[][], name: string): void {
    const csv = lines.map((l) => l.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
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
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
