import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  LucideAngularModule, ArrowDown, ArrowUp, CalendarDays, ChevronLeft, ChevronRight, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Plus, Search, X
} from 'lucide-angular';
import { AssetPicker } from '../shared/asset-picker';
import { CatalogAsset } from '../shared/asset-catalog';
import { BarChart } from '../shared/charts/bar-chart';
import { ChartCard } from '../shared/charts/chart-card';
import { C, colorFor } from '../shared/charts/chart-colors';
import { DonutChart } from '../shared/charts/donut-chart';
import { DateRangePicker } from '../shared/date-range-picker';
import { DateRange, RANGE_PRESETS, change, day, inRange, monthlySeries, nextDueFrom, pct, pmStatus, previousRange, today, woDate, woDue, woIsOpen, woStatus } from '../shared/derive';
import { MaintenanceApi, apiErrorMessage, nextCode } from '../shared/maintenance-api';
import { PM_FREQUENCIES, PM_STATUSES, PM_TYPES, PRIORITIES, PreventiveMaintenance, WorkOrder } from '../shared/maintenance.models';
import { downloadCsv, fmtDate, isoDay, pillClass } from '../shared/mnt-utils';
import { TableFooter } from '../shared/table-footer';
import { TableState, Toast } from '../shared/table-state';
import { WorkOrderDetail } from '../shared/work-order-detail';
import { WorkOrderForm } from '../shared/work-order-form';
import { duplicateSeed, raiseWorkOrder } from '../shared/work-order-factory';

type PmTab = 'schedules' | 'generated' | 'calendar' | 'plan';

interface PmForm {
  pmScheduleId: string;
  scheduleName: string;
  assetId: string;
  assetName: string;
  assetType: string;
  location: string;
  triggerType: string;
  frequency: string;
  lastMaintenanceDate: string;
  nextDueDate: string;
  priority: string;
  assignedTo: string;
  estimatedHours: number | null;
  autoCreateWorkOrder: boolean;
  status: string;
  description: string;
  checklist: string;
}

interface CalendarCell {
  date: string;
  dayNum: number;
  inMonth: boolean;
  items: { label: string; kind: 'pm' | 'wo'; status: string; code: string }[];
}

/** Chart bucket for PM work orders (open ones not yet started count as Scheduled). */
function pmWoBucket(w: WorkOrder): string | null {
  const s = woStatus(w);
  if (s === 'Completed' || s === 'In Progress' || s === 'Overdue') return s;
  return s === 'Cancelled' ? null : 'Scheduled';
}

/** Preventive Maintenance: PM schedules, generated work orders, calendar and per-asset plan. */
@Component({
  selector: 'app-mnt-preventive',
  imports: [FormsModule, RouterLink, LucideAngularModule, AssetPicker, DateRangePicker, ChartCard, DonutChart, BarChart, TableFooter, WorkOrderDetail, WorkOrderForm],
  templateUrl: './preventive.html',
  styleUrls: ['../shared/mnt-base.css', '../shared/mnt-dash.css', './preventive.css'],
  host: { '(document:click)': 'table.menuId.set(null)' }
})
export class PreventivePage {
  private readonly api = inject(MaintenanceApi);
  private readonly router = inject(Router);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly icons = { ArrowDown, ArrowUp, CalendarDays, ChevronLeft, ChevronRight, CircleAlert, CircleCheck, Clock, EllipsisVertical, FileText, Filter, Plus, Search, X };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly woStatus = woStatus;
  readonly types = PM_TYPES;
  readonly frequencies = PM_FREQUENCIES;
  readonly scheduleStatuses = PM_STATUSES;
  readonly priorities = PRIORITIES;
  readonly displayStatuses = ['On Schedule', 'In Progress', 'Overdue', 'Paused', 'Inactive'];
  readonly store = this.api.preventive;
  readonly wo = this.api.workOrders;
  readonly toast = new Toast();

  readonly range = signal<DateRange>(RANGE_PRESETS[0]!.range());
  readonly tab = signal<PmTab>('schedules');
  readonly search = signal('');
  readonly status = signal('');
  readonly type = signal('');
  readonly assetType = signal('');
  readonly location = signal('');
  readonly advanced = signal(false);
  readonly assignee = signal('');
  readonly frequency = signal('');
  readonly busyId = signal<string | null>(null);
  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly woOpenId = signal<string | null>(null);
  readonly woEditing = signal<WorkOrder | null>(null);
  readonly woSeed = signal<Partial<WorkOrder> | null>(null);
  readonly woFormOpen = signal(false);
  readonly month = signal(today().slice(0, 7));
  form!: PmForm;

  readonly tabs: { key: PmTab; label: string }[] = [
    { key: 'schedules', label: 'PM Schedules' },
    { key: 'generated', label: 'Generated Work Orders' },
    { key: 'calendar', label: 'Calendar View' },
    { key: 'plan', label: 'Asset PM Plan' }
  ];

  constructor() {
    this.store.load();
    this.wo.load();
    effect(() => {
      const code = this.query().get('code');
      const items = this.store.items();
      if (!code || !items.length) return;
      const p = items.find((x) => x.pmScheduleId.toLowerCase() === code.toLowerCase());
      if (p) this.edit(p);
      queueMicrotask(() => this.router.navigate([], { queryParams: {}, replaceUrl: true }));
    });
  }

  /** Work orders raised from PM schedules. */
  readonly pmWos = computed(() => this.wo.items().filter((w) => w.source === 'PM Schedule' || (!!w.sourceReference && w.sourceReference.startsWith('PM-'))));
  private readonly pmWosInPeriod = computed(() => this.pmWos().filter((w) => inRange(woDate(w), this.range())));

  readonly kpis = computed(() => {
    const schedules = this.store.items();
    const gen = this.pmWosInPeriod();
    const prevGen = this.pmWos().filter((w) => inRange(woDate(w), previousRange(this.range()))).length;
    const prevSchedules = schedules.filter((p) => day(p.createdAt) <= previousRange(this.range()).to).length;
    const n = (b: string) => gen.filter((w) => pmWoBucket(w) === b).length;
    return {
      schedules: schedules.length,
      schedulesTrend: change(schedules.length, prevSchedules),
      generated: gen.length,
      generatedTrend: change(gen.length, prevGen),
      completed: n('Completed'),
      progress: n('In Progress'),
      overdue: n('Overdue'),
      pct: (v: number) => pct(v, gen.length)
    };
  });

  readonly compliance = computed(() =>
    ['Completed', 'In Progress', 'Overdue', 'Scheduled']
      .map((label) => ({ label, value: this.pmWosInPeriod().filter((w) => pmWoBucket(w) === label).length, color: label === 'Scheduled' ? C.lavender : colorFor(label) }))
      .filter((s) => s.label !== 'Scheduled' || s.value > 0)
  );

  readonly trend = computed(() => {
    const names = ['Completed', 'In Progress', 'Overdue', 'Scheduled'];
    const m = monthlySeries(this.pmWos(), this.range(), woDate, pmWoBucket, names);
    return { labels: m.labels, series: names.map((name) => ({ name, color: name === 'Completed' ? C.greenLight : name === 'Scheduled' ? C.purple : colorFor(name), values: m.values[name]! })).filter((s) => s.name !== 'Scheduled' || s.values.some((v) => v > 0)) };
  });

  readonly typeDonut = computed(() => this.types.map((label) => ({ label, value: this.store.items().filter((p) => p.triggerType === label).length, color: colorFor(label) })));

  readonly assetTypes = computed(() => [...new Set(this.store.items().map((p) => p.assetType).filter((t): t is string => !!t))].sort());
  readonly locations = computed(() => [...new Set(this.store.items().map((p) => p.location).filter((t): t is string => !!t))].sort());
  readonly people = computed(() => [...new Set([...this.store.items().map((p) => p.assignedTo), ...this.wo.items().map((w) => w.assignedTechnician)].filter((t): t is string => !!t))].sort());

  statusOf(p: PreventiveMaintenance): string {
    return pmStatus(p, this.wo.items());
  }

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const wos = this.wo.items();
    return this.store.items()
      .filter((p) => {
        if (this.status() && pmStatus(p, wos) !== this.status()) return false;
        if (this.type() && p.triggerType !== this.type()) return false;
        if (this.assetType() && p.assetType !== this.assetType()) return false;
        if (this.location() && p.location !== this.location()) return false;
        if (this.frequency() && p.frequency !== this.frequency()) return false;
        if (this.assignee() && !(p.assignedTo ?? '').toLowerCase().includes(this.assignee().toLowerCase())) return false;
        if (!q) return true;
        return [p.pmScheduleId, p.scheduleName, p.assetName, p.assetId, p.assetType, p.description].some((v) => (v ?? '').toLowerCase().includes(q));
      })
      .sort((a, b) => a.pmScheduleId.localeCompare(b.pmScheduleId));
  });

  readonly generatedRows = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.pmWosInPeriod()
      .filter((w) => !this.status() || woStatus(w) === this.status())
      .filter((w) => !q || [w.workOrderId, w.title, w.assetName, w.assetId, w.sourceReference].some((v) => (v ?? '').toLowerCase().includes(q)))
      .sort((a, b) => woDue(b).localeCompare(woDue(a)));
  });

  readonly table = new TableState(this.filtered);
  readonly genTable = new TableState(this.generatedRows);

  /** Due PM schedules with auto work order on and no open work order yet. */
  readonly autoDue = computed(() => {
    const horizon = isoDay(new Date(Date.now() + 7 * 86_400_000));
    const open = new Set(this.wo.items().filter(woIsOpen).map((w) => w.sourceReference));
    return this.store.items().filter((p) => (p.status ?? 'Active') === 'Active' && p.autoCreateWorkOrder && !!p.nextDueDate && day(p.nextDueDate) <= horizon && !open.has(p.pmScheduleId));
  });

  // ---------------------------------------------------------------- calendar

  readonly monthLabel = computed(() => {
    const [y, m] = this.month().split('-').map(Number);
    return new Date(y!, m! - 1, 1).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
  });

  readonly calendar = computed<CalendarCell[]>(() => {
    const [y, m] = this.month().split('-').map(Number);
    const first = new Date(y!, m! - 1, 1);
    const start = new Date(first);
    start.setDate(1 - ((first.getDay() + 6) % 7)); // weeks start on Monday
    const items = new Map<string, CalendarCell['items']>();
    const add = (d: string, item: CalendarCell['items'][number]) => items.set(d, [...(items.get(d) ?? []), item]);
    this.filtered().forEach((p) => p.nextDueDate && add(day(p.nextDueDate), { label: p.scheduleName || p.assetName, kind: 'pm', status: this.statusOf(p), code: p.pmScheduleId }));
    this.pmWos().forEach((w) => woDue(w) && add(woDue(w), { label: `${w.workOrderId} · ${w.assetName}`, kind: 'wo', status: woStatus(w), code: w.id }));
    return Array.from({ length: 42 }, (_, i) => {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      const key = isoDay(d);
      return { date: key, dayNum: d.getDate(), inMonth: d.getMonth() === first.getMonth(), items: items.get(key) ?? [] };
    });
  });

  shiftMonth(delta: number): void {
    const [y, m] = this.month().split('-').map(Number);
    const d = new Date(y!, m! - 1 + delta, 1);
    this.month.set(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`);
  }

  calendarClick(item: CalendarCell['items'][number]): void {
    if (item.kind === 'wo') this.woOpenId.set(item.code);
    else {
      const p = this.store.items().find((x) => x.pmScheduleId === item.code);
      if (p) this.edit(p);
    }
  }

  readonly isToday = (d: string) => d === today();

  // ---------------------------------------------------------------- asset plan

  readonly plan = computed(() => {
    const groups = new Map<string, { assetId: string; assetName: string; assetType: string; location: string; schedules: PreventiveMaintenance[] }>();
    for (const p of this.filtered()) {
      const g = groups.get(p.assetId) ?? { assetId: p.assetId, assetName: p.assetName, assetType: p.assetType ?? '', location: p.location ?? '', schedules: [] };
      g.schedules.push(p);
      groups.set(p.assetId, g);
    }
    return [...groups.values()].sort((a, b) => a.assetName.localeCompare(b.assetName));
  });

  lastWoFor(p: PreventiveMaintenance): WorkOrder | undefined {
    return this.pmWos().filter((w) => w.sourceReference === p.pmScheduleId).sort((a, b) => woDue(b).localeCompare(woDue(a)))[0];
  }

  readonly woOpened = computed(() => this.wo.byId(this.woOpenId()) ?? null);

  // ---------------------------------------------------------------- filters

  set<K extends 'search' | 'status' | 'type' | 'assetType' | 'location' | 'assignee' | 'frequency'>(kind: K, value: string): void {
    this[kind].set(value);
    this.table.reset();
    this.genTable.reset();
  }

  setTab(t: PmTab): void {
    this.tab.set(t);
    this.status.set('');
    this.table.reset();
    this.genTable.reset();
  }

  setRange(r: DateRange): void {
    this.range.set(r);
    this.genTable.reset();
  }

  // ---------------------------------------------------------------- actions

  generate(p: PreventiveMaintenance, quiet = false): void {
    this.table.menuId.set(null);
    this.busyId.set(p.id);
    raiseWorkOrder(this.api, {
      assetId: p.assetId,
      assetName: p.assetName,
      workType: 'Preventive',
      category: 'Routine',
      title: `${p.assetName} - ${p.frequency} ${p.scheduleName || 'Preventive Maintenance'}`,
      description: p.description,
      priority: p.priority || 'Medium',
      dueDate: p.nextDueDate,
      scheduledDate: p.nextDueDate,
      assignedTechnician: p.assignedTo,
      estimatedHours: p.estimatedHours,
      source: 'PM Schedule',
      sourceReference: p.pmScheduleId,
      tasks: p.checklist.map((title) => ({ title, isDone: false, assignedTo: p.assignedTo }))
    }).subscribe({
      next: (w) => { this.busyId.set(null); if (!quiet) this.toast.show(`Work order ${w.workOrderId} generated for ${p.pmScheduleId}.`); },
      error: (e) => { this.busyId.set(null); this.toast.show(apiErrorMessage(e)); }
    });
  }

  generateAllDue(): void {
    const due = this.autoDue();
    due.forEach((p) => this.generate(p, true));
    this.toast.show(`Generating ${due.length} work order${due.length === 1 ? '' : 's'}...`);
  }

  /** Records a service today: last = today, next = today + one interval. */
  markCompleted(p: PreventiveMaintenance): void {
    this.table.menuId.set(null);
    const next = nextDueFrom(today(), p.frequency);
    this.store.save({ ...p, lastMaintenanceDate: `${today()}T00:00:00Z`, nextDueDate: `${next}T00:00:00Z` }).subscribe({
      next: () => this.toast.show(`${p.pmScheduleId} completed today; next due ${fmtDate(next)}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  setScheduleStatus(p: PreventiveMaintenance, status: string): void {
    this.table.menuId.set(null);
    this.store.save({ ...p, status }).subscribe({
      next: () => this.toast.show(`${p.pmScheduleId} ${status === 'Active' ? 'resumed' : status.toLowerCase()}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  remove(p: PreventiveMaintenance): void {
    this.table.confirmDeleteId.set(null);
    this.store.remove(p.id).subscribe({
      next: () => this.toast.show(`${p.pmScheduleId} deleted.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  // ---------------------------------------------------------------- form

  openNew(): void {
    this.editingId.set(null);
    this.form = {
      pmScheduleId: nextCode('PM', this.store.items().map((p) => p.pmScheduleId)),
      scheduleName: '', assetId: '', assetName: '', assetType: '', location: '', triggerType: 'Time Based', frequency: 'Monthly',
      lastMaintenanceDate: '', nextDueDate: nextDueFrom(today(), 'Monthly'), priority: 'Medium', assignedTo: '', estimatedHours: null,
      autoCreateWorkOrder: true, status: 'Active', description: '', checklist: ''
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  edit(p: PreventiveMaintenance): void {
    this.table.menuId.set(null);
    this.editingId.set(p.id);
    this.form = {
      pmScheduleId: p.pmScheduleId, scheduleName: p.scheduleName ?? '', assetId: p.assetId, assetName: p.assetName, assetType: p.assetType ?? '',
      location: p.location ?? '', triggerType: p.triggerType, frequency: p.frequency, lastMaintenanceDate: day(p.lastMaintenanceDate),
      nextDueDate: day(p.nextDueDate), priority: p.priority ?? 'Medium', assignedTo: p.assignedTo ?? '', estimatedHours: p.estimatedHours,
      autoCreateWorkOrder: p.autoCreateWorkOrder, status: p.status ?? 'Active', description: p.description ?? '', checklist: p.checklist.join('\n')
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  onAsset(a: CatalogAsset): void {
    this.form.assetType = a.type;
    this.form.location = a.site;
    if (!this.form.scheduleName.trim()) this.form.scheduleName = `${a.type} PM`;
  }

  calcNextDue(): void {
    this.form.nextDueDate = nextDueFrom(this.form.lastMaintenanceDate || today(), this.form.frequency);
  }

  save(): void {
    const f = this.form;
    const problem = !f.pmScheduleId.trim() ? 'Schedule ID is required.'
      : !f.scheduleName.trim() ? 'Schedule name is required.'
      : !f.assetId.trim() ? 'Asset tag is required.'
      : f.lastMaintenanceDate && f.nextDueDate && f.nextDueDate < f.lastMaintenanceDate ? 'Next due date cannot be before the last completed date.'
      : null;
    if (problem) return this.formError.set(problem);
    const existing = this.store.byId(this.editingId());
    const body: Partial<PreventiveMaintenance> = {
      ...(existing ?? {}),
      pmScheduleId: f.pmScheduleId.trim(), scheduleName: f.scheduleName.trim(), assetId: f.assetId.trim(), assetName: f.assetName.trim(),
      assetType: f.assetType.trim() || null, location: f.location.trim() || null, triggerType: f.triggerType, frequency: f.frequency,
      lastMaintenanceDate: f.lastMaintenanceDate ? `${f.lastMaintenanceDate}T00:00:00Z` : null,
      nextDueDate: f.nextDueDate ? `${f.nextDueDate}T00:00:00Z` : null, priority: f.priority, assignedTo: f.assignedTo.trim() || null,
      estimatedHours: f.estimatedHours === null || (f.estimatedHours as unknown) === '' ? null : Number(f.estimatedHours),
      autoCreateWorkOrder: f.autoCreateWorkOrder, status: f.status, description: f.description.trim() || null,
      checklist: f.checklist.split('\n').map((s) => s.trim()).filter(Boolean)
    };
    this.saving.set(true);
    this.formError.set(null);
    this.store.save(body).subscribe({
      next: (saved) => { this.saving.set(false); this.formOpen.set(false); this.toast.show(`PM schedule ${saved.pmScheduleId} saved.`); },
      error: (e) => { this.saving.set(false); this.formError.set(apiErrorMessage(e)); }
    });
  }

  // ---------------------------------------------------------------- export

  export(): void {
    const rows = this.filtered();
    if (!rows.length) return this.toast.show('No PM schedules to export.');
    downloadCsv(`pm-schedules-${today()}.csv`, rows.map((p) => ({
      'Schedule ID': p.pmScheduleId, 'Schedule Name': p.scheduleName ?? '', 'Asset Tag': p.assetId, 'Asset Name': p.assetName, 'Asset Type': p.assetType ?? '',
      'Maintenance Type': p.triggerType, Frequency: p.frequency, 'Next Due Date': day(p.nextDueDate), 'Last Completed': day(p.lastMaintenanceDate),
      Status: this.statusOf(p), 'Assigned To': p.assignedTo ?? '', 'Auto Work Order': p.autoCreateWorkOrder ? 'Yes' : 'No'
    })));
  }

  exportDonut(rows: { label: string; value: number }[], file: string): void {
    downloadCsv(file, rows.map((s) => ({ Label: s.label, Count: s.value })));
  }

  exportTrend(): void {
    const t = this.trend();
    downloadCsv('pm-work-orders-trend.csv', t.labels.map((label, i) => ({ Month: label, ...Object.fromEntries(t.series.map((s) => [s.name, s.values[i]])) })));
  }

  editWo(w: WorkOrder): void {
    this.woEditing.set(w);
    this.woSeed.set(null);
    this.woFormOpen.set(true);
  }

  duplicateWo(w: WorkOrder): void {
    this.woEditing.set(null);
    this.woSeed.set(duplicateSeed(w));
    this.woFormOpen.set(true);
  }
}
