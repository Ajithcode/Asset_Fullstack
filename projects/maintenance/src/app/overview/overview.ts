import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { LucideAngularModule, ArrowDown, ArrowUp, CalendarDays, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Plus, Search, Settings } from 'lucide-angular';
import { BarChart } from '../shared/charts/bar-chart';
import { ChartCard } from '../shared/charts/chart-card';
import { C, colorFor } from '../shared/charts/chart-colors';
import { DonutChart } from '../shared/charts/donut-chart';
import { DateRangePicker } from '../shared/date-range-picker';
import {
  DateRange, RANGE_PRESETS, change, day, inRange, monthlySeries, pct, pdmIsLive, pmStatus, previousRange, riskOf,
  srDate, srStatus, woDate, woDue, woStatus
} from '../shared/derive';
import { MaintenanceApi } from '../shared/maintenance-api';
import {
  PM_TYPES, PRIORITIES, PredictiveMaintenance, PreventiveMaintenance, RISK_LEVELS, SR_CATEGORIES, ServiceRequest, WORK_TYPES, WorkOrder
} from '../shared/maintenance.models';
import { downloadCsv, fmtDate, pillClass } from '../shared/mnt-utils';
import { TableFooter } from '../shared/table-footer';
import { TableState, Toast } from '../shared/table-state';
import { WorkOrderDetail } from '../shared/work-order-detail';
import { WorkOrderForm } from '../shared/work-order-form';
import { duplicateSeed } from '../shared/work-order-factory';

type OvTab = 'wo' | 'pm' | 'pdm' | 'sr';

/** Work order statuses grouped the way the overview charts show them. */
function woBucket(w: WorkOrder): string | null {
  const s = woStatus(w);
  if (s === 'Completed' || s === 'In Progress' || s === 'Overdue') return s;
  if (s === 'Cancelled') return null;
  return 'Scheduled';
}

/** Maintenance home: module KPIs, status / trend / type charts and tabbed registers of every submodule. */
@Component({
  selector: 'app-mnt-overview',
  imports: [FormsModule, RouterLink, LucideAngularModule, DateRangePicker, ChartCard, DonutChart, BarChart, TableFooter, WorkOrderDetail, WorkOrderForm],
  templateUrl: './overview.html',
  styleUrls: ['../shared/mnt-base.css', '../shared/mnt-dash.css'],
  host: { '(document:click)': 'menuId.set(null)' }
})
export class OverviewPage {
  private readonly api = inject(MaintenanceApi);
  private readonly router = inject(Router);
  readonly icons = { ArrowDown, ArrowUp, CalendarDays, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Plus, Search, Settings };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly woStatus = woStatus;
  readonly srStatus = srStatus;
  readonly riskOf = riskOf;
  readonly priorities = PRIORITIES;
  readonly toast = new Toast();
  readonly wo = this.api.workOrders;

  readonly range = signal<DateRange>(RANGE_PRESETS[0]!.range());
  readonly tab = signal<OvTab>('wo');
  readonly search = signal('');
  readonly status = signal('');
  readonly kind = signal('');
  readonly priority = signal('');
  readonly advanced = signal(false);
  readonly assignee = signal('');
  readonly site = signal('');
  readonly menuId = signal<string | null>(null);
  readonly openId = signal<string | null>(null);
  readonly formOpen = signal(false);
  readonly editing = signal<WorkOrder | null>(null);
  readonly seed = signal<Partial<WorkOrder> | null>(null);

  readonly tabs: { key: OvTab; label: string }[] = [
    { key: 'wo', label: 'Work Orders' },
    { key: 'pm', label: 'Preventive Maintenance' },
    { key: 'pdm', label: 'Predictive Maintenance' },
    { key: 'sr', label: 'Service Requests' }
  ];

  constructor() {
    this.api.workOrders.load();
    this.api.preventive.load();
    this.api.predictive.load();
    this.api.serviceRequests.load();
  }

  private readonly periodWo = computed(() => this.wo.items().filter((w) => inRange(woDate(w), this.range())));

  readonly kpis = computed(() => {
    const rows = this.periodWo();
    const total = rows.length;
    const prev = this.wo.items().filter((w) => inRange(woDate(w), previousRange(this.range()))).length;
    const n = (b: string) => rows.filter((w) => woBucket(w) === b).length;
    return { total, trend: change(total, prev), completed: n('Completed'), progress: n('In Progress'), overdue: n('Overdue'), scheduled: n('Scheduled'), pct: (v: number) => pct(v, total) };
  });

  readonly statusDonut = computed(() => {
    const rows = this.periodWo();
    const segments = ['Completed', 'In Progress', 'Overdue', 'Scheduled'].map((label) => ({ label, value: rows.filter((w) => woBucket(w) === label).length, color: label === 'Scheduled' ? C.lavender : colorFor(label) }));
    const cancelled = rows.filter((w) => woStatus(w) === 'Cancelled').length;
    return cancelled ? [...segments, { label: 'Cancelled', value: cancelled, color: '#94a3b8' }] : segments;
  });

  readonly trend = computed(() => {
    const names = ['Completed', 'In Progress', 'Overdue', 'Scheduled'];
    const m = monthlySeries(this.wo.items(), this.range(), woDate, woBucket, names);
    return { labels: m.labels, series: names.map((name) => ({ name, color: name === 'Scheduled' ? C.purple : name === 'Completed' ? C.greenLight : colorFor(name), values: m.values[name]! })) };
  });

  readonly typeDonut = computed(() =>
    ['Preventive', 'Corrective', 'Predictive', 'Inspection', 'Service']
      .map((label) => ({ label, value: this.periodWo().filter((w) => w.workType === label).length, color: colorFor(label) }))
      .filter((s) => s.value > 0 || s.label !== 'Service')
  );

  // ---------------------------------------------------------------- tab filters

  readonly statusOptions = computed(() => {
    switch (this.tab()) {
      case 'wo': return ['Open', 'Scheduled', 'In Progress', 'On Hold', 'Overdue', 'Completed', 'Cancelled'];
      case 'pm': return ['On Schedule', 'In Progress', 'Overdue', 'Paused', 'Inactive'];
      case 'pdm': return ['Monitoring', 'Alert', 'Work Order Raised', 'Resolved'];
      default: return ['Open', 'In Progress', 'Overdue', 'Resolved', 'Closed', 'Rejected'];
    }
  });

  readonly kindOptions = computed(() => {
    switch (this.tab()) {
      case 'wo': return { label: 'All Maintenance Type', values: WORK_TYPES };
      case 'pm': return { label: 'All Maintenance Type', values: PM_TYPES };
      case 'pdm': return { label: 'All Risk Levels', values: RISK_LEVELS };
      default: return { label: 'All Categories', values: SR_CATEGORIES };
    }
  });

  readonly sites = computed(() =>
    [...new Set([...this.wo.items().map((w) => w.site), ...this.api.preventive.items().map((p) => p.location), ...this.api.serviceRequests.items().map((r) => r.location), ...this.api.predictive.items().map((p) => p.location)].filter((s): s is string => !!s))].sort()
  );

  private match(values: (string | null | undefined)[]): boolean {
    const q = this.search().trim().toLowerCase();
    return !q || values.some((v) => (v ?? '').toLowerCase().includes(q));
  }

  private matchPeople(name: string | null | undefined, site: string | null | undefined): boolean {
    if (this.assignee() && !(name ?? '').toLowerCase().includes(this.assignee().toLowerCase())) return false;
    return !this.site() || site === this.site();
  }

  readonly woRows = computed(() =>
    this.periodWo()
      .filter((w) => (!this.status() || woStatus(w) === this.status()) && (!this.kind() || w.workType === this.kind()) && (!this.priority() || w.priority === this.priority()))
      .filter((w) => this.matchPeople(w.assignedTechnician, w.site) && this.match([w.workOrderId, w.assetName, w.assetId, w.title, w.description]))
      .sort((a, b) => woDue(b).localeCompare(woDue(a)))
  );

  readonly pmRows = computed(() => {
    const wos = this.wo.items();
    return this.api.preventive.items()
      .filter((p) => (!this.status() || pmStatus(p, wos) === this.status()) && (!this.kind() || p.triggerType === this.kind()) && (!this.priority() || p.priority === this.priority()))
      .filter((p) => this.matchPeople(p.assignedTo, p.location) && this.match([p.pmScheduleId, p.scheduleName, p.assetName, p.assetId]))
      .sort((a, b) => day(a.nextDueDate).localeCompare(day(b.nextDueDate)));
  });

  readonly pdmRows = computed(() =>
    this.api.predictive.items()
      .filter((p) => pdmIsLive(p) || this.status() === 'Resolved')
      .filter((p) => (!this.status() || p.status === this.status()) && (!this.kind() || riskOf(p) === this.kind()))
      .filter((p) => this.matchPeople('', p.location) && this.match([p.assetName, p.assetId, p.predictedIssue, p.assetType]))
      .sort((a, b) => (a.healthScore ?? 100) - (b.healthScore ?? 100))
  );

  readonly srRows = computed(() =>
    this.api.serviceRequests.items()
      .filter((r) => inRange(srDate(r), this.range()))
      .filter((r) => (!this.status() || srStatus(r) === this.status()) && (!this.kind() || r.category === this.kind()) && (!this.priority() || r.severity === this.priority()))
      .filter((r) => this.matchPeople(r.assignedTo, r.location) && this.match([r.issueId, r.assetName, r.assetId, r.description, r.reportedBy]))
      .sort((a, b) => srDate(b).localeCompare(srDate(a)))
  );

  readonly woTable = new TableState<WorkOrder>(this.woRows);
  readonly pmTable = new TableState<PreventiveMaintenance>(this.pmRows);
  readonly pdmTable = new TableState<PredictiveMaintenance>(this.pdmRows);
  readonly srTable = new TableState<ServiceRequest>(this.srRows);

  readonly opened = computed(() => this.wo.byId(this.openId()) ?? null);

  pmStatusOf(p: PreventiveMaintenance): string {
    return pmStatus(p, this.wo.items());
  }

  setTab(t: OvTab): void {
    this.tab.set(t);
    this.status.set('');
    this.kind.set('');
    this.priority.set('');
    this.resetPages();
  }

  setFilter(kind: 'search' | 'status' | 'kind' | 'priority' | 'assignee' | 'site', value: string): void {
    this[kind].set(value);
    this.resetPages();
  }

  setRange(r: DateRange): void {
    this.range.set(r);
    this.resetPages();
  }

  private resetPages(): void {
    [this.woTable, this.pmTable, this.pdmTable, this.srTable].forEach((t) => t.reset());
  }

  toggleMenu(id: string, ev: Event): void {
    ev.stopPropagation();
    this.menuId.update((cur) => (cur === id ? null : id));
  }

  go(path: string, code: string): void {
    this.router.navigate(['/maintenance', path], { queryParams: { code } });
  }

  editWo(w: WorkOrder): void {
    this.menuId.set(null);
    this.editing.set(w);
    this.seed.set(null);
    this.formOpen.set(true);
  }

  createWo(): void {
    this.editing.set(null);
    this.seed.set(null);
    this.formOpen.set(true);
  }

  duplicateWo(w: WorkOrder): void {
    this.editing.set(null);
    this.seed.set(duplicateSeed(w));
    this.formOpen.set(true);
  }

  onSaved(w: WorkOrder): void {
    this.formOpen.set(false);
    this.openId.set(w.id);
    this.toast.show(`Work order ${w.workOrderId} saved.`);
  }

  exportStatus(): void {
    downloadCsv('work-order-status.csv', this.statusDonut().map((s) => ({ Status: s.label, Count: s.value })));
  }

  exportTrend(): void {
    const t = this.trend();
    downloadCsv('maintenance-trend.csv', t.labels.map((label, i) => ({ Month: label, ...Object.fromEntries(t.series.map((s) => [s.name, s.values[i]])) })));
  }

  exportType(): void {
    downloadCsv('maintenance-by-type.csv', this.typeDonut().map((s) => ({ Type: s.label, Count: s.value })));
  }
}
