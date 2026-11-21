import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Boxes,
  CircleCheck,
  ClipboardCheck,
  ClipboardList,
  Ban,
  Check,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  TriangleAlert,
  Wrench,
  X,
} from 'lucide-angular';
import { Subject, Subscription, debounceTime } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import {
  OPERATIONAL_STATUSES,
  PHYSICAL_CONDITIONS,
  StatusConditionAsset,
  StatusConditionChange,
  StatusConditionService,
  StatusConditionSummary,
  conditionTone,
  formatDate,
  formatDateTime,
  stateTone,
  statusTone,
} from './status-condition.service';

type View = 'assets' | 'history';

interface Kpi {
  label: string;
  value: number;
  icon: typeof Boxes;
  /** Sets the filters to list these assets when clicked. */
  status?: string;
  condition?: string;
  hint?: string;
}

const PAGE_SIZE = 20;
/** Condition filter value for the Poor / Critical card (the API takes a comma-separated list). */
const POOR_OR_CRITICAL = 'Poor,Critical';

/**
 * Assets > Asset Status & Condition (menu page): summary cards, the asset list with current status and condition,
 * and the history of status & condition changes with their approvals.
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
  ],
})
export class AssetStatusConditionOverview implements OnInit, OnDestroy {
  readonly icons = { Boxes, CircleCheck, ClipboardCheck, ClipboardList, Ban, Check, Pencil, Plus, RefreshCw, Search, TriangleAlert, Wrench, X };
  readonly statuses = OPERATIONAL_STATUSES;
  readonly conditions = PHYSICAL_CONDITIONS;
  readonly poorOrCritical = POOR_OR_CRITICAL;
  readonly statusTone = statusTone;
  readonly conditionTone = conditionTone;
  readonly stateTone = stateTone;
  readonly formatDate = formatDate;
  readonly formatDateTime = formatDateTime;

  private readonly api = inject(StatusConditionService);
  private readonly router = inject(Router);
  private readonly search$ = new Subject<void>();
  private subs: Subscription[] = [];
  private listSub?: Subscription;

  view: View = 'assets';
  search = '';
  status = '';
  condition = '';
  /** Change History only: Draft, Pending Approval, Approved or Rejected. */
  state = '';
  page = 1;
  readonly pageSize = PAGE_SIZE;

  summary: StatusConditionSummary | null = null;
  kpis: Kpi[] = [];
  assets: StatusConditionAsset[] = [];
  changes: StatusConditionChange[] = [];
  total = 0;
  loading = false;
  loadError = '';

  /** Change opened in the details / approval dialog. */
  selected: StatusConditionChange | null = null;
  decision: 'approve' | 'reject' | null = null;
  comments = '';
  acting = false;
  dialogError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.subs.push(this.search$.pipe(debounceTime(300)).subscribe(() => this.reload()));
    this.loadSummary();
    this.reload();
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    this.listSub?.unsubscribe();
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  loadSummary(): void {
    this.subs.push(
      this.api.summary().subscribe({
        next: (s) => {
          this.summary = s;
          this.kpis = [
            { label: 'Total Assets', value: s.totalAssets, icon: Boxes },
            { label: 'Operational Assets', value: s.operationalAssets, icon: CircleCheck, status: 'Active' },
            { label: 'Under Maintenance', value: s.underMaintenance, icon: Wrench, status: 'Under Maintenance' },
            { label: 'Poor / Critical Condition', value: s.poorOrCritical, icon: TriangleAlert, condition: POOR_OR_CRITICAL },
            { label: 'Out of Service', value: s.outOfService, icon: Ban, status: 'Out of Service' },
            { label: 'Condition Assessments Due', value: s.assessmentsDue, icon: ClipboardCheck, hint: `Due within ${s.assessmentDueWithinDays} days or overdue` },
          ];
        },
        error: (err) => (this.loadError = apiErrorMessage(err, 'Could not load the status summary.')),
      }),
    );
  }

  reload(page = 1): void {
    this.page = page;
    this.loading = true;
    this.loadError = '';
    this.listSub?.unsubscribe();
    const filters = { search: this.search, status: this.status, condition: this.condition, page, pageSize: PAGE_SIZE };
    if (this.view === 'assets') {
      this.listSub = this.api.assets(filters).subscribe({
        next: (res) => {
          this.assets = res.items;
          this.total = res.totalCount;
          this.loading = false;
        },
        error: (err) => this.fail(err),
      });
    } else {
      this.listSub = this.api.changes({ ...filters, state: this.state }).subscribe({
        next: (res) => {
          this.changes = res.items;
          this.total = res.totalCount;
          this.loading = false;
        },
        error: (err) => this.fail(err),
      });
    }
  }

  private fail(err: unknown): void {
    this.loading = false;
    this.loadError = apiErrorMessage(err, 'Could not load the list.');
  }

  refresh(): void {
    this.loadSummary();
    this.reload(this.page);
  }

  // ===== Filters =====

  onSearch(): void {
    this.search$.next();
  }

  setView(view: View): void {
    if (this.view === view) return;
    this.view = view;
    this.reload();
  }

  pickKpi(k: Kpi): void {
    this.status = k.status ?? '';
    this.condition = k.condition ?? '';
    this.reload();
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / PAGE_SIZE));
  }

  get rangeLabel(): string {
    if (!this.total) return '0 items';
    const from = (this.page - 1) * PAGE_SIZE + 1;
    const to = Math.min(this.total, this.page * PAGE_SIZE);
    return `${from}–${to} of ${this.total}`;
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
    if (!a.pendingChangeId) return;
    this.subs.push(
      this.api.change(a.pendingChangeId).subscribe({
        next: (c) => this.openChange(c),
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

  /** "Source: Inspection · Operational status: Active" line of a history row. */
  sourceLine(c: StatusConditionChange): string {
    return [c.assessmentSource ? `Source: ${c.assessmentSource}` : '', c.newStatus ? `Operational status: ${c.newStatus}` : '']
      .filter(Boolean)
      .join(' · ');
  }

  showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }

  trackAsset = (_: number, a: StatusConditionAsset) => a.assetRegistrationId;
  trackChange = (_: number, c: StatusConditionChange) => c.id;
}
