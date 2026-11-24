import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule, Ban, Boxes, Check, CircleCheck, ClipboardCheck, ClipboardList, Download, Eye, History, Pencil,
  Plus, RefreshCw, Search, TriangleAlert, Wrench, X,
} from 'lucide-angular';
import { Subscription, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import {
  StatusConditionAsset, StatusConditionChange, StatusConditionLookups, StatusConditionOptions, StatusConditionService,
  StatusConditionSummary, conditionTone, formatDate, formatDateTime, stateTone, statusTone,
} from './status-condition.service';

type View = 'assets' | 'history';
/** Change History: every change, only status transitions, or only condition reassessments. */
type ChangeKind = '' | 'status' | 'condition';
/** Assessment due filter on the Asset Summary. */
type DueFilter = '' | 'overdue' | 'due' | 'scheduled' | 'none';

interface Kpi {
  label: string;
  value: number;
  icon: typeof Boxes;
  /** Clicking the card sets these filters. */
  status?: string;
  condition?: string;
  due?: DueFilter;
  pending?: boolean;
  hint?: string;
}

const PAGE_SIZE = 20;
const POOR_OR_CRITICAL = 'Poor,Critical';
const DAY = 86_400_000;

/**
 * Assets > Asset Status & Condition: KPIs, the asset summary (current operational status, condition, criticality,
 * health and assessment schedule of every registered asset) with a details panel, and the status / condition change
 * history with approvals. All data comes from /api/asset-status-condition; nothing is sample data.
 * Update opens /assets/asset-status-condition/update (optionally ?tag= / ?change=).
 */
@Component({
  selector: 'app-asset-status-condition-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-status-condition-overview.html',
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-status-condition-overview.css',
    './status-condition-panel.css',
  ],
})
export class AssetStatusConditionOverview implements OnInit, OnDestroy {
  readonly icons = {
    Ban, Boxes, Check, CircleCheck, ClipboardCheck, ClipboardList, Download, Eye, History, Pencil, Plus, RefreshCw, Search,
    TriangleAlert, Wrench, X,
  };
  readonly poorOrCritical = POOR_OR_CRITICAL;
  readonly statusTone = statusTone;
  readonly conditionTone = conditionTone;
  readonly stateTone = stateTone;
  readonly formatDate = formatDate;
  readonly formatDateTime = formatDateTime;

  private readonly api = inject(StatusConditionService);
  private readonly router = inject(Router);
  private subs: Subscription[] = [];

  view: View = 'assets';
  page = 1;
  readonly pageSize = PAGE_SIZE;

  // ===== Data (all from the API) =====
  options: StatusConditionOptions | null = null;
  summary: StatusConditionSummary | null = null;
  kpis: Kpi[] = [];
  allAssets: StatusConditionAsset[] = [];
  allChanges: StatusConditionChange[] = [];
  loading = false;
  loadError = '';

  // ===== Filters =====
  search = '';
  site = '';
  category = '';
  assetType = '';
  status = '';
  condition = '';
  criticality = '';
  source = '';
  due: DueFilter = '';
  /** Assets with a change waiting for approval. */
  pendingOnly = false;
  /** Change History only. */
  state = '';
  kind: ChangeKind = '';
  from = '';
  to = '';

  // ===== Details panel =====
  detail: StatusConditionAsset | null = null;
  detailLookups: StatusConditionLookups | null = null;
  detailLoading = false;

  // ===== Change dialog (details & approval) =====
  selected: StatusConditionChange | null = null;
  decision: 'approve' | 'reject' | null = null;
  comments = '';
  acting = false;
  dialogError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.subs.push(
      this.api.options().subscribe({
        next: (o) => (this.options = o),
        error: (err) => (this.loadError = apiErrorMessage(err, 'Could not load the status and condition options.')),
      }),
    );
    this.refresh();
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  refresh(): void {
    this.loading = true;
    this.loadError = '';
    this.subs.push(
      forkJoin({ summary: this.api.summary(), assets: this.api.allAssets(), changes: this.api.allChanges() }).subscribe({
        next: ({ summary, assets, changes }) => {
          this.summary = summary;
          this.allAssets = assets;
          this.allChanges = changes;
          this.kpis = this.buildKpis(summary);
          this.loading = false;
          if (this.detail) this.detail = assets.find((a) => a.assetRegistrationId === this.detail!.assetRegistrationId) ?? null;
        },
        error: (err) => {
          this.loading = false;
          this.loadError = apiErrorMessage(err, 'Could not load the asset status and condition data.');
        },
      }),
    );
  }

  private buildKpis(s: StatusConditionSummary): Kpi[] {
    return [
      { label: 'Total Assets', value: s.totalAssets, icon: Boxes },
      { label: 'Operational Assets', value: s.operationalAssets, icon: CircleCheck, status: 'Active' },
      { label: 'Under Maintenance', value: s.underMaintenance, icon: Wrench, status: 'Under Maintenance' },
      { label: 'Poor / Critical Condition', value: s.poorOrCritical, icon: TriangleAlert, condition: POOR_OR_CRITICAL },
      { label: 'Out of Service', value: s.outOfService, icon: Ban, status: 'Out of Service' },
      { label: 'Condition Assessments Due', value: s.assessmentsDue, icon: ClipboardCheck, due: 'due', hint: `Due within ${s.assessmentDueWithinDays} days or overdue` },
      { label: 'Pending Approvals', value: s.pendingApprovals, icon: ClipboardList, pending: true, hint: 'Changes waiting for approval' },
    ];
  }

  // ===== Filter choices (from the API options and the loaded assets) =====

  get statuses(): string[] {
    return this.options?.statuses ?? [];
  }

  get conditions(): string[] {
    return this.options?.conditions ?? [];
  }

  get sources(): string[] {
    return this.options?.assessmentSources ?? [];
  }

  get states(): string[] {
    return this.options?.states ?? [];
  }

  private distinct(pick: (a: StatusConditionAsset) => string | null): string[] {
    return [...new Set(this.allAssets.map(pick).filter((v): v is string => !!v))].sort();
  }

  get sites(): string[] {
    return this.distinct((a) => a.siteName);
  }

  get categories(): string[] {
    return this.distinct((a) => a.category);
  }

  get assetTypes(): string[] {
    return this.distinct((a) => (!this.category || a.category === this.category ? a.assetType : null));
  }

  get criticalities(): string[] {
    return this.distinct((a) => a.criticality);
  }

  // ===== Asset Summary =====

  /** Days until the next assessment (negative = overdue); null when none is scheduled. */
  dueIn(a: StatusConditionAsset): number | null {
    if (!a.nextAssessmentDue) return null;
    const t = new Date(a.nextAssessmentDue).getTime();
    return isNaN(t) ? null : Math.floor((t - Date.now()) / DAY);
  }

  dueState(a: StatusConditionAsset): DueFilter {
    const d = this.dueIn(a);
    const window = this.summary?.assessmentDueWithinDays ?? 30;
    return d === null ? 'none' : d < 0 ? 'overdue' : d <= window ? 'due' : 'scheduled';
  }

  private matchesSearch(...values: (string | null)[]): boolean {
    const q = this.search.trim().toLowerCase();
    return !q || values.some((v) => (v ?? '').toLowerCase().includes(q));
  }

  get filteredAssets(): StatusConditionAsset[] {
    const conditions = this.condition ? this.condition.split(',') : [];
    return this.allAssets.filter(
      (a) =>
        this.matchesSearch(a.assetRegistrationId, a.assetTag, a.assetName, a.siteName, a.category, a.assetType) &&
        (!this.site || a.siteName === this.site) &&
        (!this.category || a.category === this.category) &&
        (!this.assetType || a.assetType === this.assetType) &&
        (!this.status || a.status === this.status) &&
        (!conditions.length || conditions.includes(a.condition ?? '')) &&
        (!this.criticality || a.criticality === this.criticality) &&
        (!this.source || a.lastAssessmentSource === this.source) &&
        (!this.due || (this.due === 'due' ? ['due', 'overdue'].includes(this.dueState(a)) : this.dueState(a) === this.due)) &&
        (!this.pendingOnly || !!a.pendingChangeId),
    );
  }

  /** Who last changed the asset's status or condition (latest applied change), from the change history. */
  lastUpdatedBy(a: StatusConditionAsset): string {
    const latest = this.changesOf(a).find((c) => c.state === 'Approved');
    return latest ? latest.updatedBy || latest.submittedBy || latest.createdBy || '-' : '-';
  }

  // ===== Change History =====

  isStatusChange(c: StatusConditionChange): boolean {
    return !!c.newStatus && c.newStatus !== c.previousStatus;
  }

  isConditionChange(c: StatusConditionChange): boolean {
    return !!c.newCondition && c.newCondition !== c.previousCondition;
  }

  private changeTime(c: StatusConditionChange): string {
    return c.appliedAt || c.submittedAt || c.updatedAt || c.createdAt || '';
  }

  get filteredChanges(): StatusConditionChange[] {
    const conditions = this.condition ? this.condition.split(',') : [];
    const from = this.from ? new Date(`${this.from}T00:00:00`).getTime() : null;
    const to = this.to ? new Date(`${this.to}T23:59:59`).getTime() : null;
    return this.allChanges
      .filter((c) => {
        const t = new Date(this.changeTime(c)).getTime();
        return (
          this.matchesSearch(c.assetRegistrationId, c.assetTag, c.assetName, c.siteName, c.changeNumber) &&
          (!this.site || c.siteName === this.site) &&
          (!this.status || c.newStatus === this.status) &&
          (!conditions.length || conditions.includes(c.newCondition)) &&
          (!this.source || c.assessmentSource === this.source) &&
          (!this.state || c.state === this.state) &&
          (!this.kind || (this.kind === 'status' ? this.isStatusChange(c) : this.isConditionChange(c))) &&
          (from === null || t >= from) &&
          (to === null || t <= to)
        );
      })
      .sort((a, b) => this.changeTime(b).localeCompare(this.changeTime(a)));
  }

  // ===== Paging (client side, over the filtered list) =====

  get total(): number {
    return this.view === 'assets' ? this.filteredAssets.length : this.filteredChanges.length;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / PAGE_SIZE));
  }

  get pageAssets(): StatusConditionAsset[] {
    const p = Math.min(this.page, this.pageCount);
    return this.filteredAssets.slice((p - 1) * PAGE_SIZE, p * PAGE_SIZE);
  }

  get pageChanges(): StatusConditionChange[] {
    const p = Math.min(this.page, this.pageCount);
    return this.filteredChanges.slice((p - 1) * PAGE_SIZE, p * PAGE_SIZE);
  }

  get rangeLabel(): string {
    if (!this.total) return '0 items';
    const from = (Math.min(this.page, this.pageCount) - 1) * PAGE_SIZE + 1;
    return `${from}–${Math.min(this.total, from + PAGE_SIZE - 1)} of ${this.total}`;
  }

  setPage(p: number): void {
    this.page = Math.min(Math.max(1, p), this.pageCount);
  }

  onFilter(): void {
    this.page = 1;
  }

  get hasFilters(): boolean {
    return !!(this.search || this.site || this.category || this.assetType || this.status || this.condition || this.criticality ||
      this.source || this.due || this.pendingOnly || this.state || this.kind || this.from || this.to);
  }

  clearFilters(): void {
    this.search = this.site = this.category = this.assetType = this.status = this.condition = this.criticality = this.source = '';
    this.due = '';
    this.pendingOnly = false;
    this.state = '';
    this.kind = '';
    this.from = this.to = '';
    this.page = 1;
  }

  setView(view: View): void {
    this.view = view;
    this.page = 1;
  }

  pickKpi(k: Kpi): void {
    this.clearFilters();
    if (k.pending) {
      this.view = 'history';
      this.state = 'Pending Approval';
      return;
    }
    this.view = 'assets';
    this.status = k.status ?? '';
    this.condition = k.condition ?? '';
    this.due = k.due ?? '';
  }

  isKpiActive(k: Kpi): boolean {
    if (k.pending) return this.view === 'history' && this.state === 'Pending Approval';
    return (
      this.view === 'assets' &&
      (k.status ?? '') === this.status &&
      (k.condition ?? '') === this.condition &&
      (k.due ?? '') === this.due &&
      !this.site && !this.category && !this.assetType && !this.criticality && !this.source && !this.pendingOnly
    );
  }

  // ===== Details panel =====

  openDetail(a: StatusConditionAsset): void {
    this.detail = a;
    this.detailLookups = null;
    this.detailLoading = true;
    this.subs.push(
      this.api.lookups(a.assetRegistrationId).subscribe({
        next: (l) => {
          if (this.detail?.assetRegistrationId !== a.assetRegistrationId) return;
          this.detailLookups = l;
          this.detailLoading = false;
        },
        error: () => (this.detailLoading = false),
      }),
    );
  }

  closeDetail(): void {
    this.detail = null;
  }

  /** The asset's changes, newest first. */
  changesOf(a: StatusConditionAsset): StatusConditionChange[] {
    return this.allChanges
      .filter((c) => c.assetRegistrationId === a.assetRegistrationId)
      .sort((x, y) => this.changeTime(y).localeCompare(this.changeTime(x)));
  }

  /** Work orders of the asset that are not closed. */
  openWorkOrders(l: StatusConditionLookups): StatusConditionLookups['workOrders'] {
    return l.workOrders.filter((w) => !/^(completed|closed|cancelled|canceled)$/i.test(w.status ?? ''));
  }

  historyFor(a: StatusConditionAsset): void {
    this.clearFilters();
    this.search = a.assetTag || a.assetRegistrationId;
    this.view = 'history';
    this.detail = null;
  }

  // ===== Navigation =====

  openUpdate(asset?: StatusConditionAsset): void {
    const queryParams = asset ? { tag: asset.assetTag || asset.assetRegistrationId } : {};
    this.router.navigate(['/assets/asset-status-condition/update'], { queryParams });
  }

  editChange(c: StatusConditionChange): void {
    this.router.navigate(['/assets/asset-status-condition/update'], { queryParams: { change: c.id } });
  }

  openPending(a: StatusConditionAsset): void {
    const c = a.pendingChangeId ? this.allChanges.find((x) => x.id === a.pendingChangeId) : undefined;
    if (c) return this.openChange(c);
    if (!a.pendingChangeId) return;
    this.subs.push(
      this.api.change(a.pendingChangeId).subscribe({
        next: (change) => this.openChange(change),
        error: (err) => this.showToast(apiErrorMessage(err, 'Could not load the change.')),
      }),
    );
  }

  // ===== Change details / approval =====

  openChange(c: StatusConditionChange): void {
    this.selected = c;
    this.decision = null;
    this.comments = '';
    this.dialogError = '';
  }

  closeChange(): void {
    if (this.acting) return;
    this.selected = null;
  }

  currentLevel(c: StatusConditionChange) {
    return c.state === 'Pending Approval' ? c.approvals.find((a) => a.status === 'Pending') ?? null : null;
  }

  startDecision(kind: 'approve' | 'reject'): void {
    this.decision = kind;
    this.dialogError = '';
  }

  confirmDecision(): void {
    const c = this.selected;
    if (!c || !this.decision) return;
    if (this.decision === 'reject' && !this.comments.trim()) {
      this.dialogError = 'Enter a reason for rejecting the change.';
      return;
    }
    this.acting = true;
    const call = this.decision === 'approve' ? this.api.approve(c.id, this.comments.trim()) : this.api.reject(c.id, this.comments.trim());
    this.subs.push(
      call.subscribe({
        next: (updated) => {
          this.acting = false;
          this.selected = updated;
          this.decision = null;
          this.comments = '';
          this.showToast(
            updated.state === 'Approved'
              ? `${updated.changeNumber} approved. ${updated.assetTag} is now ${updated.newStatus} / ${updated.newCondition}.`
              : updated.state === 'Rejected'
                ? `${updated.changeNumber} rejected.`
                : `${updated.changeNumber} approved at this level; waiting for the next approver.`,
          );
          this.refresh();
        },
        error: (err) => {
          this.acting = false;
          this.dialogError = apiErrorMessage(err, 'Could not save the decision.');
        },
      }),
    );
  }

  deleteDraft(c: StatusConditionChange): void {
    this.subs.push(
      this.api.deleteDraft(c.id).subscribe({
        next: () => {
          this.selected = null;
          this.showToast(`Draft ${c.changeNumber} deleted.`);
          this.refresh();
        },
        error: (err) => (this.dialogError = apiErrorMessage(err, 'Could not delete the draft.')),
      }),
    );
  }

  /** "Status change · Condition reassessment" tags of a history row. */
  kindLabel(c: StatusConditionChange): string {
    return [this.isStatusChange(c) ? 'Status change' : '', this.isConditionChange(c) ? 'Condition reassessment' : ''].filter(Boolean).join(' · ') || 'No change';
  }

  // ===== Export =====

  exportCsv(): void {
    const cell = (v: unknown) => `"${`${v ?? ''}`.replace(/"/g, '""')}"`;
    const rows =
      this.view === 'assets'
        ? [
            ['Asset ID', 'Asset Tag', 'Asset Name', 'Category', 'Type', 'Site', 'Operational Status', 'Condition', 'Criticality', 'Health Score', 'Last Assessment', 'Assessment Source', 'Next Assessment Due', 'Last Updated By'],
            ...this.filteredAssets.map((a) => [a.assetRegistrationId, a.assetTag, a.assetName, a.category, a.assetType, a.siteName, a.status, a.condition, a.criticality, a.healthScore, a.lastAssessmentDate, a.lastAssessmentSource, a.nextAssessmentDue, this.lastUpdatedBy(a)]),
          ]
        : [
            ['Change', 'Asset Tag', 'Asset Name', 'Site', 'Previous Status', 'New Status', 'Previous Condition', 'New Condition', 'Reason', 'Source', 'Assessed By', 'State', 'Submitted By', 'Submitted At', 'Applied At'],
            ...this.filteredChanges.map((c) => [c.changeNumber, c.assetTag, c.assetName, c.siteName, c.previousStatus, c.newStatus, c.previousCondition, c.newCondition, c.reasonCode, c.assessmentSource, c.assessedBy, c.state, c.submittedBy, c.submittedAt, c.appliedAt]),
          ];
    const blob = new Blob([rows.map((r) => r.map(cell).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = this.view === 'assets' ? 'asset-status-condition.csv' : 'asset-status-condition-history.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.selected) this.closeChange();
    else this.detail = null;
  }

  showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }

  trackAsset = (_: number, a: StatusConditionAsset) => a.assetRegistrationId;
  trackChange = (_: number, c: StatusConditionChange) => c.id;
}
