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
import {
  ASSET_RECORDS,
  ASSET_TYPE_ICONS,
  AssetHeader,
  AssetRecord,
  AssetTabLink,
  assetCondition,
  assetHeader,
  assetLocation,
  assetTabs,
  findAsset,
} from '../asset-data';
import {
  CriticalityCriterion,
  CriticalityData,
  CriticalityLevel,
  Mitigation,
  MitigationStatus,
  cloneDetail,
  loadAssetDetail,
} from '../asset-details/asset-details';

type SubTab = 'details' | 'impact' | 'mitigation' | 'history' | 'related';
type ViewMode = 'list' | 'grid';

/** A register asset with its criticality, for the list at the bottom of the page. */
interface ListRow {
  asset: AssetRecord;
  crit: CriticalityData;
}

interface RelatedRow {
  asset: AssetRecord;
  relation: string;
  crit: CriticalityData | null;
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
  CriticalityData,
  'method' | 'reviewFrequency' | 'validUntil' | 'nextReview' | 'assessedBy' | 'approvedBy' | 'department' | 'assetGroup' | 'remarks'
>;

interface MitigationForm {
  action: string;
  owner: string;
  dueDate: string;
  status: MitigationStatus;
  reducesTo: CriticalityLevel;
}

const CURRENT_USER = 'Rex Kumar';
const LEVELS: CriticalityLevel[] = ['Critical', 'High', 'Medium', 'Low'];
/** Matrix axis labels, index 0 = 1 (very low). */
const SCALE = ['Very Low', 'Low', 'Medium', 'High', 'Very High'];

/** Radar geometry in SVG user units. */
const RADAR = { cx: 210, cy: 120, r: 78, labelR: 104 };

/** Criticality of each asset, kept in memory so edits survive moving between tabs (DEMO MODE). */
const CRITICALITY = new Map<string, CriticalityData>();

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
  readonly mitigationStatuses: MitigationStatus[] = ['Open', 'In Progress', 'Completed'];
  readonly assessmentMethods = ['Risk Matrix (Weighted)', 'Risk Matrix (Simple)', 'FMECA', 'Expert Judgement'];
  readonly reviewFrequencies = ['Every 3 Months', 'Every 6 Months', 'Every 12 Months'];

  @ViewChild('subtabsEl') private subtabsEl?: ElementRef<HTMLElement>;

  private readonly router = inject(Router);

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  record: AssetRecord = ASSET_RECORDS[0];
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Criticality');

  loading = false;
  crit: CriticalityData | null = null;
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
  readonly categories = [...new Set(ASSET_RECORDS.map((a) => a.category))].sort();
  readonly sites = [...new Set(ASSET_RECORDS.map((a) => a.site))].sort();

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
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  private load(tag: string | null): void {
    tag ??= ASSET_RECORDS[0].tag; // opened from the menu without an asset: show the first one
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.record = record;
    this.asset = assetHeader(record);
    this.crit = CRITICALITY.get(record.tag) ?? null;
    this.related = [];
    this.subTab = 'details';
    this.closeMenus();
    this.assessForm = this.infoForm = this.mitigationForm = null;
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      if (detail) {
        this.crit = this.stateFor(record.tag, detail.criticality);
        this.related = detail.relationships.links
          .filter((l) => findAsset(l.tag))
          .map((l) => ({ asset: findAsset(l.tag)!, relation: `${l.type} (${l.direction})`, crit: CRITICALITY.get(l.tag) ?? null }));
      }
      if (this.assessOnLoad === record.tag) {
        this.assessOnLoad = null;
        this.openAssessment();
      }
    });
  }

  /** The in-memory criticality of an asset, created from its data file the first time. */
  private stateFor(tag: string, fromFile: CriticalityData): CriticalityData {
    let state = CRITICALITY.get(tag);
    if (!state) {
      state = cloneDetail(fromFile);
      CRITICALITY.set(tag, state);
    }
    return state;
  }

  /** Loads every asset's criticality for the list (each file is fetched once and cached). */
  private loadList(): void {
    Promise.all(
      ASSET_RECORDS.map((asset) =>
        loadAssetDetail(asset.tag).then((d) => (d ? { asset, crit: this.stateFor(asset.tag, d.criticality) } : null)),
      ),
    ).then((rows) => {
      this.rows = rows.filter((r): r is ListRow => !!r);
      this.listLoading = false;
      for (const r of this.related) r.crit = CRITICALITY.get(r.asset.tag) ?? null;
    });
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
    return this.crit?.mitigations.filter((m) => m.status !== 'Completed').length ?? 0;
  }

  mitigationTone(status: MitigationStatus): string {
    return status === 'Completed' ? 'good' : status === 'In Progress' ? 'warn' : 'serious';
  }

  setMitigationStatus(m: Mitigation, status: MitigationStatus): void {
    m.status = status;
    this.showToast(`"${m.action}" marked ${status}.`);
  }

  openMitigation(): void {
    this.closeMenus();
    this.formError = '';
    const next = this.crit ? LEVELS[Math.min(LEVELS.length - 1, LEVELS.indexOf(this.crit.level) + 1)] : 'Low';
    this.mitigationForm = { action: '', owner: CURRENT_USER, dueDate: addMonths(this.today, 1), status: 'Open', reducesTo: next };
  }

  saveMitigation(): void {
    const f = this.mitigationForm;
    if (!f || !this.crit) return;
    if (!f.action.trim() || !f.owner.trim() || !f.dueDate) {
      this.formError = 'Enter the action, owner and due date.';
      return;
    }
    const id = Math.max(0, ...this.crit.mitigations.map((m) => m.id)) + 1;
    this.crit.mitigations = [...this.crit.mitigations, { ...f, id, action: f.action.trim(), owner: f.owner.trim() }];
    this.mitigationForm = null;
    this.showToast('Mitigation action added.');
  }

  viewAsset(tag: string, assess = false): void {
    this.closeMenus();
    if (assess) this.assessOnLoad = tag;
    this.router.navigate(['/assets/criticality'], { queryParams: { tag } }).then(() => {
      if (tag === this.record.tag && assess) {
        this.assessOnLoad = null;
        this.openAssessment();
      }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  // ===== Re-assessment =====

  openAssessment(): void {
    this.closeMenus();
    if (!this.crit) return;
    this.formError = '';
    this.assessForm = {
      date: this.today,
      assessedBy: CURRENT_USER,
      approvedBy: this.crit.approvedBy,
      likelihood: this.crit.likelihood,
      consequence: this.crit.consequence,
      criteria: this.crit.criteria.map((c) => ({ ...c })),
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
    const crit = this.crit;
    if (!f || !crit) return;
    if (!f.date || !f.assessedBy.trim() || !f.approvedBy.trim()) {
      this.formError = 'Enter the assessment date, assessed by and approved by.';
      return;
    }
    if (f.criteria.some((c) => !c.justification.trim())) {
      this.formError = 'Give a justification for every criterion.';
      return;
    }
    // The current assessment moves into the review history
    crit.history = [
      {
        date: crit.assessmentDate, score: crit.score, level: crit.level, likelihood: crit.likelihood, consequence: crit.consequence,
        assessedBy: crit.assessedBy, approvedBy: crit.approvedBy, remarks: crit.remarks,
      },
      ...crit.history,
    ];
    const previous = crit.level;
    const months = crit.reviewFrequency === 'Every 3 Months' ? 3 : crit.reviewFrequency === 'Every 12 Months' ? 12 : 6;
    Object.assign(crit, {
      score: this.formScore,
      level: this.formLevel,
      assessmentDate: f.date,
      validUntil: addMonths(f.date, 12),
      nextReview: addMonths(f.date, months),
      assessedBy: f.assessedBy.trim(),
      approvedBy: f.approvedBy.trim(),
      likelihood: Number(f.likelihood),
      consequence: Number(f.consequence),
      criteria: f.criteria.map((c) => ({ ...c, score: Number(c.score), justification: c.justification.trim() })),
      remarks: f.remarks.trim() || crit.remarks,
    });
    this.assessForm = null;
    this.showToast(previous === crit.level ? `Assessment saved: ${crit.score} / 100 (${crit.level}).` : `Criticality changed from ${previous} to ${crit.level}.`);
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
    if (!f || !this.crit) return;
    if (!f.validUntil || !f.assessedBy.trim() || !f.approvedBy.trim()) {
      this.formError = 'Enter valid until, assessed by and approved by.';
      return;
    }
    Object.assign(this.crit, { ...f, assessedBy: f.assessedBy.trim(), approvedBy: f.approvedBy.trim(), remarks: f.remarks.trim() });
    this.infoForm = null;
    this.showToast('Additional information saved.');
  }

  // ===== Asset Criticality List =====

  get filtered(): ListRow[] {
    const q = this.search.trim().toLowerCase();
    return this.rows.filter(
      (r) =>
        (!this.categoryFilter || r.asset.category === this.categoryFilter) &&
        (!this.siteFilter || r.asset.site === this.siteFilter) &&
        (!this.levelFilter || r.crit.level === this.levelFilter) &&
        (!q || [r.asset.tag, r.asset.name, r.asset.category, assetLocation(r.asset)].some((v) => v.toLowerCase().includes(q))),
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

  location(a: AssetRecord): string {
    return assetLocation(a);
  }

  condition(a: AssetRecord): string {
    return assetCondition(a);
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.asset.tag));
  }

  toggleAll(on: boolean): void {
    this.pageRows.forEach((r) => (on ? this.checked.add(r.asset.tag) : this.checked.delete(r.asset.tag)));
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
    const rows = this.checked.size ? this.filtered.filter((r) => this.checked.has(r.asset.tag)) : this.filtered;
    const lines = [
      ['Asset ID', 'Asset Name', 'Category', 'Location', 'Criticality Level', 'Criticality Score', 'Assessment Date', 'Next Review', 'Status'],
      ...rows.map((r) => [r.asset.tag, r.asset.name, r.asset.category, assetLocation(r.asset), r.crit.level, r.crit.score, r.crit.assessmentDate, r.crit.nextReview, r.asset.status]),
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
