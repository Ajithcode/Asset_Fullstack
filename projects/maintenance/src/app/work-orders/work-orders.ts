import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { LucideAngularModule, CircleAlert, CircleCheck, ClipboardList, Clock, Download, EllipsisVertical, Plus, Search, ArrowUp, ArrowDown, Trash2 } from 'lucide-angular';
import { DateRangePicker } from '../shared/date-range-picker';
import { DateRange, RANGE_PRESETS, change, inRange, pct, previousRange, woDate, woDue, woIsOpen, woStatus } from '../shared/derive';
import { MaintenanceApi, apiErrorMessage } from '../shared/maintenance-api';
import { PRIORITIES, WORK_TYPES, WO_STATUSES, WorkOrder } from '../shared/maintenance.models';
import { downloadCsv, fmtDate, pillClass } from '../shared/mnt-utils';
import { TableFooter } from '../shared/table-footer';
import { TableState, Toast } from '../shared/table-state';
import { WorkOrderDetail } from '../shared/work-order-detail';
import { WorkOrderForm } from '../shared/work-order-form';
import { duplicateSeed } from '../shared/work-order-factory';

type WoTab = 'all' | 'mine' | 'open' | 'progress' | 'overdue' | 'completed' | 'cancelled';

/** Work Orders: KPIs, tabbed register with bulk actions, and the slide-over work order panel. */
@Component({
  selector: 'app-mnt-work-orders',
  imports: [FormsModule, RouterLink, LucideAngularModule, DateRangePicker, TableFooter, WorkOrderDetail, WorkOrderForm],
  templateUrl: './work-orders.html',
  styleUrls: ['../shared/mnt-base.css', '../shared/mnt-dash.css'],
  host: { '(document:click)': 'table.menuId.set(null)' }
})
export class WorkOrdersPage {
  private readonly api = inject(MaintenanceApi);
  private readonly router = inject(Router);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly icons = { CircleAlert, CircleCheck, ClipboardList, Clock, Download, EllipsisVertical, Plus, Search, ArrowUp, ArrowDown, Trash2 };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly woStatus = woStatus;
  readonly types = WORK_TYPES;
  readonly priorities = PRIORITIES;
  readonly statuses = WO_STATUSES;
  readonly store = this.api.workOrders;
  readonly toast = new Toast();

  readonly range = signal<DateRange>(RANGE_PRESETS[0]!.range());
  readonly tab = signal<WoTab>('all');
  readonly search = signal('');
  readonly type = signal('');
  readonly priority = signal('');
  readonly openId = signal<string | null>(null);
  readonly formOpen = signal(false);
  readonly editing = signal<WorkOrder | null>(null);
  readonly seed = signal<Partial<WorkOrder> | null>(null);
  readonly bulkStatus = signal('');

  readonly tabs: { key: WoTab; label: string }[] = [
    { key: 'all', label: 'All Work Orders' },
    { key: 'mine', label: 'My Work Orders' },
    { key: 'open', label: 'Open' },
    { key: 'progress', label: 'In Progress' },
    { key: 'overdue', label: 'Overdue' },
    { key: 'completed', label: 'Completed' },
    { key: 'cancelled', label: 'Cancelled' }
  ];

  /** Work orders in the selected period. */
  private readonly inPeriod = computed(() => this.store.items().filter((w) => inRange(woDate(w), this.range())));

  readonly kpis = computed(() => {
    const rows = this.inPeriod();
    const prev = this.store.items().filter((w) => inRange(woDate(w), previousRange(this.range()))).length;
    const total = rows.length;
    const count = (s: string) => rows.filter((w) => woStatus(w) === s).length;
    return {
      total,
      trend: change(total, prev),
      completed: count('Completed'),
      progress: count('In Progress'),
      overdue: count('Overdue'),
      pct: (n: number) => pct(n, total)
    };
  });

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const tab = this.tab();
    return this.inPeriod()
      .filter((w) => {
        const st = woStatus(w);
        if (tab === 'mine' && !this.api.isMe(w.assignedTechnician)) return false;
        if (tab === 'open' && !['Open', 'Scheduled'].includes(st)) return false;
        if (tab === 'progress' && st !== 'In Progress') return false;
        if (tab === 'overdue' && st !== 'Overdue') return false;
        if (tab === 'completed' && st !== 'Completed') return false;
        if (tab === 'cancelled' && st !== 'Cancelled') return false;
        if (this.type() && w.workType !== this.type()) return false;
        if (this.priority() && w.priority !== this.priority()) return false;
        if (!q) return true;
        return [w.workOrderId, w.title, w.assetName, w.assetId, w.description, w.assignedTechnician].some((v) => (v ?? '').toLowerCase().includes(q));
      })
      .sort((a, b) => (woDue(b) || '').localeCompare(woDue(a) || '') || b.workOrderId.localeCompare(a.workOrderId));
  });

  readonly table = new TableState(this.filtered);
  readonly opened = computed(() => this.store.byId(this.openId()) ?? null);

  constructor() {
    this.store.load();
    // Deep links: ?code=WO-2026-101 opens that work order, ?new=1 opens the create form.
    effect(() => {
      const q = this.query();
      const code = q.get('code');
      const items = this.store.items();
      if (code && items.length) {
        const w = items.find((x) => x.workOrderId.toLowerCase() === code.toLowerCase());
        if (w) {
          this.openId.set(w.id);
          if (!inRange(woDate(w), this.range())) this.range.set({ from: woDate(w).slice(0, 4) + '-01-01', to: woDate(w).slice(0, 4) + '-12-31' });
        }
        this.clearQuery();
      } else if (q.get('new')) {
        this.openNew();
        this.clearQuery();
      }
    });
  }

  private clearQuery(): void {
    queueMicrotask(() => this.router.navigate([], { queryParams: {}, replaceUrl: true }));
  }

  setFilter(kind: 'search' | 'type' | 'priority', value: string): void {
    this[kind].set(value);
    this.table.reset();
  }

  setTab(t: WoTab): void {
    this.tab.set(t);
    this.table.reset();
  }

  setRange(r: DateRange): void {
    this.range.set(r);
    this.table.reset();
  }

  openNew(seed: Partial<WorkOrder> | null = null): void {
    this.editing.set(null);
    this.seed.set(seed);
    this.formOpen.set(true);
  }

  edit(w: WorkOrder): void {
    this.table.menuId.set(null);
    this.editing.set(w);
    this.seed.set(null);
    this.formOpen.set(true);
  }

  duplicate(w: WorkOrder): void {
    this.openNew(duplicateSeed(w));
  }

  onSaved(w: WorkOrder): void {
    this.formOpen.set(false);
    this.openId.set(w.id);
    this.toast.show(`Work order ${w.workOrderId} saved.`);
  }

  setStatus(w: WorkOrder, status: string): void {
    this.table.menuId.set(null);
    this.store.save({ ...w, status }).subscribe({
      next: () => this.toast.show(`${w.workOrderId} set to ${status}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  remove(w: WorkOrder): void {
    this.table.confirmDeleteId.set(null);
    this.store.remove(w.id).subscribe({
      next: () => this.toast.show(`${w.workOrderId} deleted.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  /** Applies the bulk status to every selected work order. */
  applyBulkStatus(): void {
    const status = this.bulkStatus();
    const rows = this.table.selectedRows();
    if (!status || !rows.length) return;
    let done = 0;
    let failed = 0;
    const finish = () => {
      if (done + failed < rows.length) return;
      this.toast.show(failed ? `${done} updated, ${failed} failed.` : `${done} work orders set to ${status}.`);
      this.table.clearSelection();
      this.bulkStatus.set('');
    };
    rows.forEach((w) =>
      this.store.save({ ...w, status }).subscribe({
        next: () => { done++; finish(); },
        error: () => { failed++; finish(); }
      })
    );
  }

  export(rows: WorkOrder[] = this.filtered()): void {
    if (!rows.length) return this.toast.show('No work orders to export.');
    downloadCsv(`work-orders-${this.range().from}-to-${this.range().to}.csv`, rows.map((w) => ({
      'Work Order #': w.workOrderId,
      Title: w.title,
      'Asset Name': w.assetName,
      'Asset Tag': w.assetId,
      Type: w.workType,
      Category: w.category ?? '',
      Priority: w.priority,
      Status: woStatus(w),
      'Due Date': woDue(w),
      'Assigned To': w.assignedTechnician ?? '',
      Progress: `${w.progress}%`,
      'Total Cost': w.totalCost
    })));
  }

  isOverdue(w: WorkOrder): boolean {
    return woIsOpen(w) && woStatus(w) === 'Overdue';
  }
}
