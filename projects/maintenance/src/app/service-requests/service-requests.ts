import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { LucideAngularModule, ArrowDown, ArrowUp, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Headset, Plus, Search, UserRound, X } from 'lucide-angular';
import { AuthService } from 'shared-ui';
import { AssetPicker } from '../shared/asset-picker';
import { CatalogAsset } from '../shared/asset-catalog';
import { BarChart } from '../shared/charts/bar-chart';
import { ChartCard } from '../shared/charts/chart-card';
import { C, colorFor } from '../shared/charts/chart-colors';
import { DonutChart } from '../shared/charts/donut-chart';
import { DateRangePicker } from '../shared/date-range-picker';
import { DateRange, RANGE_PRESETS, change, day, inRange, monthlySeries, pct, previousRange, srDate, srIsOpen, srStatus, today } from '../shared/derive';
import { MaintenanceApi, apiErrorMessage, currentUserName, nextCode } from '../shared/maintenance-api';
import { PRIORITIES, SR_CATEGORIES, SR_STATUSES, SR_TYPES, ServiceRequest } from '../shared/maintenance.models';
import { downloadCsv, fmtDate, pillClass } from '../shared/mnt-utils';
import { TableFooter } from '../shared/table-footer';
import { TableState, Toast } from '../shared/table-state';
import { raiseWorkOrder } from '../shared/work-order-factory';

type SrTab = 'all' | 'mine' | 'open' | 'progress' | 'overdue' | 'resolved';

interface SrForm {
  issueId: string;
  issueType: string;
  category: string;
  assetId: string;
  assetName: string;
  location: string;
  severity: string;
  status: string;
  reportedBy: string;
  requestedDate: string;
  targetDate: string;
  assignedTo: string;
  description: string;
  resolutionNotes: string;
}

/** Display bucket for charts: Resolved covers Closed; Rejected is left out. */
function srBucket(r: ServiceRequest): string | null {
  const s = srStatus(r);
  if (s === 'Closed') return 'Resolved';
  return ['Resolved', 'In Progress', 'Overdue', 'Open'].includes(s) ? s : null;
}

/** Service Requests: KPIs, status / trend / category charts, tabbed register and convert-to-work-order. */
@Component({
  selector: 'app-mnt-service-requests',
  imports: [FormsModule, RouterLink, LucideAngularModule, AssetPicker, DateRangePicker, ChartCard, DonutChart, BarChart, TableFooter],
  templateUrl: './service-requests.html',
  styleUrls: ['../shared/mnt-base.css', '../shared/mnt-dash.css'],
  host: { '(document:click)': 'table.menuId.set(null)' }
})
export class ServiceRequestsPage {
  private readonly api = inject(MaintenanceApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly icons = { ArrowDown, ArrowUp, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Headset, Plus, Search, UserRound, X };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly srStatus = srStatus;
  readonly types = SR_TYPES;
  readonly categories = SR_CATEGORIES;
  readonly priorities = PRIORITIES;
  readonly statuses = SR_STATUSES;
  readonly store = this.api.serviceRequests;
  readonly toast = new Toast();

  readonly range = signal<DateRange>(RANGE_PRESETS[0]!.range());
  readonly tab = signal<SrTab>('all');
  readonly search = signal('');
  readonly status = signal('');
  readonly category = signal('');
  readonly priority = signal('');
  readonly location = signal('');
  readonly advanced = signal(false);
  readonly reqType = signal('');
  readonly assignee = signal('');
  readonly viewId = signal<string | null>(null);
  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  form!: SrForm;

  readonly tabs: { key: SrTab; label: string }[] = [
    { key: 'all', label: 'All Requests' },
    { key: 'mine', label: 'My Requests' },
    { key: 'open', label: 'Open' },
    { key: 'progress', label: 'In Progress' },
    { key: 'overdue', label: 'Overdue' },
    { key: 'resolved', label: 'Resolved' }
  ];

  private readonly inPeriod = computed(() => this.store.items().filter((r) => inRange(srDate(r), this.range())));

  readonly kpis = computed(() => {
    const rows = this.inPeriod();
    const total = rows.length;
    const prev = this.store.items().filter((r) => inRange(srDate(r), previousRange(this.range()))).length;
    const n = (b: string) => rows.filter((r) => srBucket(r) === b).length;
    return { total, trend: change(total, prev), resolved: n('Resolved'), progress: n('In Progress'), overdue: n('Overdue'), open: n('Open'), pct: (v: number) => pct(v, total) };
  });

  readonly statusDonut = computed(() =>
    ['Resolved', 'In Progress', 'Overdue', 'Open'].map((label) => ({ label, value: this.inPeriod().filter((r) => srBucket(r) === label).length, color: label === 'Open' ? C.purple : colorFor(label) }))
  );

  readonly trend = computed(() => {
    const names = ['Resolved', 'In Progress', 'Overdue', 'Open'];
    const m = monthlySeries(this.store.items(), this.range(), srDate, srBucket, names);
    return { labels: m.labels, series: names.map((name) => ({ name, color: name === 'Open' ? C.purple : name === 'Resolved' ? C.greenLight : colorFor(name), values: m.values[name]! })) };
  });

  readonly categoryDonut = computed(() =>
    this.categories.map((label) => ({ label, value: this.inPeriod().filter((r) => (r.category || 'Other') === label).length, color: colorFor(label) }))
  );

  readonly locations = computed(() => [...new Set(this.store.items().map((r) => r.location).filter((l): l is string => !!l))].sort());
  readonly people = computed(() => [...new Set(this.store.items().flatMap((r) => [r.assignedTo, r.reportedBy]).filter((p): p is string => !!p))].sort());

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const tab = this.tab();
    return this.inPeriod()
      .filter((r) => {
        const st = srStatus(r);
        if (tab === 'mine' && !this.api.isMe(r.reportedBy) && !this.api.isMe(r.assignedTo) && !this.api.isMe(r.createdBy)) return false;
        if (tab === 'open' && st !== 'Open') return false;
        if (tab === 'progress' && st !== 'In Progress') return false;
        if (tab === 'overdue' && st !== 'Overdue') return false;
        if (tab === 'resolved' && !['Resolved', 'Closed'].includes(st)) return false;
        if (this.status() && st !== this.status()) return false;
        if (this.category() && r.category !== this.category()) return false;
        if (this.priority() && r.severity !== this.priority()) return false;
        if (this.location() && r.location !== this.location()) return false;
        if (this.reqType() && r.issueType !== this.reqType()) return false;
        if (this.assignee() && !(r.assignedTo ?? '').toLowerCase().includes(this.assignee().toLowerCase())) return false;
        if (!q) return true;
        return [r.issueId, r.assetName, r.assetId, r.description, r.reportedBy, r.issueType].some((v) => (v ?? '').toLowerCase().includes(q));
      })
      .sort((a, b) => srDate(b).localeCompare(srDate(a)) || b.issueId.localeCompare(a.issueId));
  });

  readonly table = new TableState(this.filtered);
  readonly viewing = computed(() => this.store.byId(this.viewId()) ?? null);
  readonly linkedWo = computed(() => {
    const r = this.viewing();
    if (!r) return null;
    return this.api.workOrders.items().find((w) => (r.workOrderId && w.workOrderId === r.workOrderId) || w.sourceReference === r.issueId) ?? null;
  });

  constructor() {
    this.store.load();
    this.api.workOrders.load();
    effect(() => {
      const code = this.query().get('code');
      const items = this.store.items();
      if (!code || !items.length) return;
      const r = items.find((x) => x.issueId.toLowerCase() === code.toLowerCase());
      if (r) {
        this.viewId.set(r.id);
        const d = srDate(r);
        if (!inRange(d, this.range())) this.range.set({ from: `${d.slice(0, 4)}-01-01`, to: `${d.slice(0, 4)}-12-31` });
      }
      queueMicrotask(() => this.router.navigate([], { queryParams: {}, replaceUrl: true }));
    });
  }

  set<K extends 'search' | 'status' | 'category' | 'priority' | 'location' | 'reqType' | 'assignee'>(kind: K, value: string): void {
    this[kind].set(value);
    this.table.reset();
  }

  setTab(t: SrTab): void {
    this.tab.set(t);
    this.table.reset();
  }

  setRange(r: DateRange): void {
    this.range.set(r);
    this.table.reset();
  }

  // ---------------------------------------------------------------- form

  openNew(): void {
    this.editingId.set(null);
    const t = today();
    const target = new Date(Date.now() + 3 * 86_400_000).toISOString().slice(0, 10);
    this.form = {
      issueId: nextCode('SR', this.store.items().map((r) => r.issueId)),
      issueType: 'Repair Request', category: 'Equipment Issue', assetId: '', assetName: '', location: '',
      severity: 'Medium', status: 'Open', reportedBy: currentUserName(this.auth), requestedDate: t, targetDate: target,
      assignedTo: '', description: '', resolutionNotes: ''
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  edit(r: ServiceRequest): void {
    this.table.menuId.set(null);
    this.editingId.set(r.id);
    this.form = {
      issueId: r.issueId, issueType: r.issueType, category: r.category ?? 'Other', assetId: r.assetId, assetName: r.assetName,
      location: r.location ?? '', severity: r.severity, status: r.status ?? 'Open', reportedBy: r.reportedBy ?? '',
      requestedDate: day(r.requestedDate), targetDate: day(r.targetDate), assignedTo: r.assignedTo ?? '',
      description: r.description ?? '', resolutionNotes: r.resolutionNotes ?? ''
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  onAsset(a: CatalogAsset): void {
    this.form.location = a.site;
  }

  save(): void {
    const f = this.form;
    const problem = !f.issueId.trim() ? 'Request ID is required.'
      : !f.assetId.trim() ? 'Asset tag is required.'
      : f.targetDate && f.requestedDate && f.targetDate < f.requestedDate ? 'Target date cannot be before the requested date.'
      : null;
    if (problem) return this.formError.set(problem);
    const existing = this.store.byId(this.editingId());
    const body: Partial<ServiceRequest> = {
      ...(existing ?? {}),
      issueId: f.issueId.trim(), issueType: f.issueType, category: f.category, assetId: f.assetId.trim(), assetName: f.assetName.trim(),
      location: f.location.trim() || null, severity: f.severity, status: f.status, reportedBy: f.reportedBy.trim() || null,
      requestedDate: f.requestedDate ? `${f.requestedDate}T00:00:00Z` : null, targetDate: f.targetDate ? `${f.targetDate}T00:00:00Z` : null,
      assignedTo: f.assignedTo.trim() || null, description: f.description.trim() || null, resolutionNotes: f.resolutionNotes.trim() || null
    };
    this.saving.set(true);
    this.formError.set(null);
    this.store.save(body).subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.toast.show(`Service request ${saved.issueId} saved.`);
      },
      error: (e) => {
        this.saving.set(false);
        this.formError.set(apiErrorMessage(e));
      }
    });
  }

  // ---------------------------------------------------------------- actions

  setStatus(r: ServiceRequest, status: string): void {
    this.table.menuId.set(null);
    this.store.save({ ...r, status }).subscribe({
      next: () => this.toast.show(`${r.issueId} set to ${status}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  canConvert(r: ServiceRequest): boolean {
    return srIsOpen(r) && !r.workOrderId;
  }

  /** Raises a work order from the request and links it (request moves to In Progress). */
  convert(r: ServiceRequest): void {
    this.table.menuId.set(null);
    if (this.busyId()) return;
    this.busyId.set(r.id);
    raiseWorkOrder(this.api, {
      assetId: r.assetId,
      assetName: r.assetName,
      workType: r.issueType === 'Inspection Request' ? 'Inspection' : r.issueType === 'Service Request' ? 'Service' : 'Corrective',
      category: r.issueType === 'Repair Request' ? 'Breakdown' : 'Routine',
      title: `${r.assetName || r.assetId} - ${r.issueType}`,
      description: r.description,
      priority: r.severity,
      dueDate: r.targetDate,
      scheduledDate: r.targetDate,
      assignedTechnician: r.assignedTo,
      source: 'Service Request',
      sourceReference: r.issueId
    }).subscribe({
      next: (wo) => {
        this.store.save({ ...r, workOrderId: wo.workOrderId, status: 'In Progress' }).subscribe({
          next: () => { this.busyId.set(null); this.toast.show(`Work order ${wo.workOrderId} created from ${r.issueId}.`); },
          error: (e) => { this.busyId.set(null); this.toast.show(`Work order ${wo.workOrderId} created, but the request was not updated: ${apiErrorMessage(e)}`); }
        });
      },
      error: (e) => { this.busyId.set(null); this.toast.show(apiErrorMessage(e)); }
    });
  }

  remove(r: ServiceRequest): void {
    this.table.confirmDeleteId.set(null);
    if (this.viewId() === r.id) this.viewId.set(null);
    this.store.remove(r.id).subscribe({
      next: () => this.toast.show(`${r.issueId} deleted.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  export(): void {
    const rows = this.filtered();
    if (!rows.length) return this.toast.show('No service requests to export.');
    downloadCsv(`service-requests-${this.range().from}-to-${this.range().to}.csv`, rows.map((r) => ({
      'Request ID': r.issueId, 'Request Type': r.issueType, 'Asset Name': r.assetName, 'Asset Tag': r.assetId, Category: r.category ?? '',
      Priority: r.severity, Status: srStatus(r), 'Requested By': r.reportedBy ?? '', 'Requested Date': srDate(r), 'Target Date': day(r.targetDate),
      Location: r.location ?? '', 'Assigned To': r.assignedTo ?? '', 'Work Order': r.workOrderId ?? ''
    })));
  }

  exportDonut(rows: { label: string; value: number }[], file: string): void {
    downloadCsv(file, rows.map((s) => ({ Label: s.label, Count: s.value })));
  }

  exportTrend(): void {
    const t = this.trend();
    downloadCsv('requests-trend.csv', t.labels.map((label, i) => ({ Month: label, ...Object.fromEntries(t.series.map((s) => [s.name, s.values[i]])) })));
  }
}
