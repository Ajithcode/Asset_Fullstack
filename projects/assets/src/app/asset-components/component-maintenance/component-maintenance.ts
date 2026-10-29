import { Component, HostListener, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  CalendarDays,
  CalendarClock,
  Check,
  CircleAlert,
  ClipboardList,
  EllipsisVertical,
  Gauge,
  HeartPulse,
  History,
  Inbox,
  Plus,
  SquarePen,
  Table2,
  Trash2,
  ChartColumn,
  X,
  Zap,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';

export type IntervalUnit = 'Hours' | 'Days' | 'Months';
export type MaintenanceKind = 'Preventive' | 'Inspection' | 'Replacement' | 'Calibration';
export type RecordStatus = 'Completed' | 'Cancelled';
type ScheduleStatus = 'Overdue' | 'Upcoming' | 'Scheduled';

export interface MaintenanceSchedule {
  id: number;
  task: string;
  kind: MaintenanceKind;
  intervalValue: number;
  intervalUnit: IntervalUnit;
  /** ISO date (yyyy-mm-dd). */
  nextDue: string;
}

export interface MaintenanceRecord {
  id: number;
  date: string;
  task: string;
  /** Component running hours when the work was done; null when not applicable. */
  runningHours: number | null;
  status: RecordStatus;
  technician: string;
  notes: string;
}

export interface MaintenanceData {
  /** Average running hours per day, used to turn hour-based intervals into due dates. */
  hoursPerDay: number;
  runningHours: number;
  schedules: MaintenanceSchedule[];
  history: MaintenanceRecord[];
}

interface Occurrence {
  schedule: MaintenanceSchedule;
  due: string;
  status: ScheduleStatus;
}

interface MonthBar {
  label: string;
  key: string;
  completed: number;
  pending: number;
  overdue: number;
}

const DAY_MS = 86_400_000;
/** Due within this many days counts as "Upcoming" rather than "Scheduled". */
const UPCOMING_WINDOW_DAYS = 90;
const LOOKAHEAD_MONTHS = 6;

let nextId = 1;

function today(): Date {
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

function toIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function fromIso(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function addDays(d: Date, days: number): Date {
  const out = new Date(d);
  out.setDate(out.getDate() + days);
  return out;
}

function addMonths(d: Date, months: number): Date {
  const out = new Date(d);
  out.setMonth(out.getMonth() + months);
  return out;
}

type Template = [task: string, kind: MaintenanceKind, value: number, unit: IntervalUnit];

/** Schedule templates per component icon, so each kind of part gets plausible tasks. */
function templatesFor(c: AssetComponent): Template[] {
  switch (c.icon) {
    case 'engine':
      return [
        ['500 Hours Service', 'Preventive', 500, 'Hours'],
        ['Oil Filter Replacement', 'Replacement', 500, 'Hours'],
        ['Air Filter Replacement', 'Replacement', 1000, 'Hours'],
        ['Fuel Filter Replacement', 'Replacement', 1000, 'Hours'],
        ['Turbo Charger Inspection', 'Inspection', 2000, 'Hours'],
      ];
    case 'filter':
    case 'fuel':
    case 'oil':
      return [
        [`${c.name} Replacement`, 'Replacement', 500, 'Hours'],
        ['Visual Inspection', 'Inspection', 1, 'Months'],
        ['Differential Pressure Check', 'Inspection', 14, 'Days'],
        ['Housing Cleaning', 'Preventive', 250, 'Hours'],
        ['Seal & Gasket Check', 'Inspection', 3, 'Months'],
      ];
    case 'battery':
      return [
        ['Voltage & Terminal Check', 'Inspection', 1, 'Months'],
        ['Load Test', 'Preventive', 6, 'Months'],
        ['Terminal Cleaning', 'Preventive', 3, 'Months'],
        ['Capacity Test', 'Inspection', 6, 'Months'],
        ['Charger Output Calibration', 'Calibration', 12, 'Months'],
      ];
    case 'panel':
    case 'cpu':
    case 'board':
    case 'control':
      return [
        ['Firmware & Fault Log Review', 'Inspection', 3, 'Months'],
        ['Sensor Calibration', 'Calibration', 12, 'Months'],
        ['Connection Tightness Check', 'Inspection', 6, 'Months'],
        ['Dust Cleaning', 'Preventive', 3, 'Months'],
        ['Configuration Backup', 'Preventive', 1, 'Months'],
      ];
    default:
      return [
        ['General Inspection', 'Inspection', 3, 'Months'],
        ['Cleaning & Lubrication', 'Preventive', 500, 'Hours'],
        ['Vibration Analysis', 'Inspection', 6, 'Months'],
        ['Bearing Lubrication', 'Preventive', 1000, 'Hours'],
        ['Alignment Check', 'Calibration', 12, 'Months'],
      ];
  }
}

const TECHNICIANS = ['Rashid Ali', 'Hamad Lateef', 'Ramesh Kumar', 'Faisal Ahmed'];

/** Deterministic sample data for a component, laid out relative to today. */
export function buildMockMaintenance(c: AssetComponent): MaintenanceData {
  const now = today();
  const runningHours = c.lifeUnit === 'hrs' && c.lifeUsed ? c.lifeUsed : 1250;
  const schedules: MaintenanceSchedule[] = templatesFor(c).map(([task, kind, value, unit], i) => ({
    id: nextId++,
    task,
    kind,
    intervalValue: value,
    intervalUnit: unit,
    // Spread due dates from a couple of weeks overdue to ~8 months out.
    nextDue: toIso(addDays(now, ((i * 53 + c.id * 17) % 250) - 15)),
  }));
  const history: MaintenanceRecord[] = Array.from({ length: 6 }, (_, k) => ({
    id: nextId++,
    date: toIso(addDays(now, -(12 + k * 38 + (c.id % 7) * 3))),
    task: schedules[k % schedules.length].task,
    runningHours: Math.max(0, runningHours - (k + 1) * 150),
    status: k === 3 ? 'Cancelled' : 'Completed',
    technician: TECHNICIANS[(k + c.id) % TECHNICIANS.length],
    notes: '',
  }));
  return { hoursPerDay: 4, runningHours, schedules, history };
}

type ScheduleForm = Omit<MaintenanceSchedule, 'id'>;
type RecordForm = Omit<MaintenanceRecord, 'id'>;

@Component({
  selector: 'app-component-maintenance',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './component-maintenance.html',
  // Reuses the page's card, table, pill, menu and modal styles.
  styleUrls: ['../asset-components.css', './component-maintenance.css'],
})
export class ComponentMaintenance {
  readonly icons = {
    CalendarDays, CalendarClock, Check, ChartColumn, CircleAlert, ClipboardList, EllipsisVertical, Gauge, HeartPulse,
    History, Inbox, Plus, SquarePen, Table2, Trash2, X, Zap,
  };

  readonly kinds: MaintenanceKind[] = ['Preventive', 'Inspection', 'Replacement', 'Calibration'];
  readonly units: IntervalUnit[] = ['Hours', 'Days', 'Months'];
  readonly technicians = TECHNICIANS;

  private comp!: AssetComponent;

  /** Sample data is created on first view and kept on the component, so it survives tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    c.maintenance ??= buildMockMaintenance(c);
    this.comp = c;
    this.openMenu = null;
    this.checkedSchedules.clear();
    this.checkedHistory.clear();
  }

  get component(): AssetComponent {
    return this.comp;
  }

  get data(): MaintenanceData {
    return this.comp.maintenance!;
  }

  openMenu: string | null = null;
  checkedSchedules = new Set<number>();
  checkedHistory = new Set<number>();
  chartAsTable = false;
  hoveredMonth: string | null = null;

  scheduleFormOpen = false;
  editingSchedule: MaintenanceSchedule | null = null;
  scheduleForm: ScheduleForm = this.emptySchedule();
  scheduleSubmitted = false;

  recordFormOpen = false;
  editingRecord: MaintenanceRecord | null = null;
  recordForm: RecordForm = this.emptyRecord();
  recordSubmitted = false;

  confirmTarget: { kind: 'schedule'; item: MaintenanceSchedule } | { kind: 'record'; item: MaintenanceRecord } | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  // ===== Date helpers =====

  daysUntil(iso: string): number {
    return Math.round((fromIso(iso).getTime() - today().getTime()) / DAY_MS);
  }

  private statusFor(iso: string): ScheduleStatus {
    const days = this.daysUntil(iso);
    if (days < 0) return 'Overdue';
    return days <= UPCOMING_WINDOW_DAYS ? 'Upcoming' : 'Scheduled';
  }

  scheduleStatus(s: MaintenanceSchedule): ScheduleStatus {
    return this.statusFor(s.nextDue);
  }

  /** Next due date after `from`, converting running-hour intervals to days at the component's usage rate. */
  private advance(from: Date, s: Pick<MaintenanceSchedule, 'intervalValue' | 'intervalUnit'>): Date {
    switch (s.intervalUnit) {
      case 'Hours':
        return addDays(from, Math.max(1, Math.ceil(s.intervalValue / this.data.hoursPerDay)));
      case 'Days':
        return addDays(from, s.intervalValue);
      case 'Months':
        return addMonths(from, s.intervalValue);
    }
  }

  intervalLabel(s: Pick<MaintenanceSchedule, 'intervalValue' | 'intervalUnit'>): string {
    const unit = s.intervalValue === 1 ? s.intervalUnit.replace(/s$/, '') : s.intervalUnit;
    return `${s.intervalValue.toLocaleString()} ${unit}`;
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  // ===== KPIs =====

  get sortedSchedules(): MaintenanceSchedule[] {
    return [...this.data.schedules].sort((a, b) => a.nextDue.localeCompare(b.nextDue));
  }

  get nextSchedule(): MaintenanceSchedule | null {
    return this.sortedSchedules[0] ?? null;
  }

  /** The recurring service the component is mainly maintained on: the first preventive task, else the first task. */
  get primarySchedule(): MaintenanceSchedule | null {
    return this.data.schedules.find((s) => s.kind === 'Preventive') ?? this.data.schedules[0] ?? null;
  }

  get sortedHistory(): MaintenanceRecord[] {
    return [...this.data.history].sort((a, b) => b.date.localeCompare(a.date));
  }

  get lastCompleted(): MaintenanceRecord | null {
    return this.sortedHistory.find((r) => r.status === 'Completed') ?? null;
  }

  /** Progress toward the next hour-based service, measured from the last completed record with an hour reading. */
  get hourProgress(): { since: number; interval: number; percent: number } | null {
    const hourly = this.data.schedules.filter((s) => s.intervalUnit === 'Hours');
    if (!hourly.length) return null;
    const interval = Math.min(...hourly.map((s) => s.intervalValue));
    const last = this.sortedHistory.find((r) => r.status === 'Completed' && r.runningHours !== null);
    const since = Math.max(0, this.data.runningHours - (last?.runningHours ?? 0));
    return { since, interval, percent: Math.min(100, Math.round((since / interval) * 100)) };
  }

  get overdueCount(): number {
    return this.data.schedules.filter((s) => this.scheduleStatus(s) === 'Overdue').length;
  }

  get dueSoonCount(): number {
    return this.data.schedules.filter((s) => {
      const d = this.daysUntil(s.nextDue);
      return d >= 0 && d <= 14;
    }).length;
  }

  get health(): { label: 'Good' | 'Attention' | 'Critical'; note: string } {
    const c = this.comp;
    if (this.overdueCount || c.status === 'Needs Replacement') {
      const parts = [];
      if (this.overdueCount) parts.push(`${this.overdueCount} overdue task${this.overdueCount === 1 ? '' : 's'}`);
      if (c.status === 'Needs Replacement') parts.push('needs replacement');
      return { label: 'Critical', note: parts.join(', ') };
    }
    if (this.dueSoonCount || c.status === 'Under Maintenance' || c.status === 'Inactive') {
      return {
        label: 'Attention',
        note: this.dueSoonCount ? `${this.dueSoonCount} task${this.dueSoonCount === 1 ? '' : 's'} due within 14 days` : c.status,
      };
    }
    return { label: 'Good', note: 'No critical issues' };
  }

  // ===== Upcoming (next 6 months) =====

  /** Every occurrence of every schedule up to `until`, starting from its next due date. */
  private occurrences(until: Date): Occurrence[] {
    const out: Occurrence[] = [];
    for (const s of this.data.schedules) {
      let due = fromIso(s.nextDue);
      for (let i = 0; i < 24 && due <= until; i++) {
        const iso = toIso(due);
        out.push({ schedule: s, due: iso, status: this.statusFor(iso) });
        // An overdue task only counts once; the following occurrences are reckoned from completion.
        if (i === 0 && this.statusFor(iso) === 'Overdue') due = this.advance(today(), s);
        else due = this.advance(due, s);
      }
    }
    return out.sort((a, b) => a.due.localeCompare(b.due));
  }

  get upcoming(): Occurrence[] {
    return this.occurrences(addMonths(today(), LOOKAHEAD_MONTHS));
  }

  // ===== Metrics chart =====

  /** Six months back through five months ahead, so both completed work and the forward plan show. */
  get monthBars(): MonthBar[] {
    const start = new Date(today().getFullYear(), today().getMonth() - 6, 1);
    const bars: MonthBar[] = Array.from({ length: 12 }, (_, i) => {
      const d = new Date(start.getFullYear(), start.getMonth() + i, 1);
      return {
        key: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`,
        label: d.toLocaleString('en', { month: 'short' }),
        completed: 0,
        pending: 0,
        overdue: 0,
      };
    });
    const byKey = new Map(bars.map((b) => [b.key, b]));
    const end = new Date(start.getFullYear(), start.getMonth() + 12, 0);

    for (const r of this.data.history) {
      if (r.status === 'Completed') {
        const bar = byKey.get(r.date.slice(0, 7));
        if (bar) bar.completed++;
      }
    }
    for (const o of this.occurrences(end)) {
      const bar = byKey.get(o.due.slice(0, 7));
      if (!bar) continue;
      if (o.status === 'Overdue') bar.overdue++;
      else bar.pending++;
    }
    return bars;
  }

  get chartMax(): number {
    const peak = Math.max(1, ...this.monthBars.map((b) => b.completed + b.pending + b.overdue));
    return peak <= 4 ? 4 : Math.ceil(peak / 2) * 2;
  }

  get chartTicks(): number[] {
    const max = this.chartMax;
    const step = max <= 4 ? 1 : max / 4;
    return Array.from({ length: Math.round(max / step) + 1 }, (_, i) => Math.round(i * step)).reverse();
  }

  barTotal(b: MonthBar): number {
    return b.completed + b.pending + b.overdue;
  }

  isCurrentMonth(b: MonthBar): boolean {
    return b.key === toIso(today()).slice(0, 7);
  }

  // ===== Selection & menus =====

  toggle(set: Set<number>, id: number): void {
    if (set.has(id)) set.delete(id);
    else set.add(id);
  }

  toggleAll(set: Set<number>, ids: number[]): void {
    const all = ids.length > 0 && ids.every((id) => set.has(id));
    ids.forEach((id) => (all ? set.delete(id) : set.add(id)));
  }

  allChecked(set: Set<number>, ids: number[]): boolean {
    return ids.length > 0 && ids.every((id) => set.has(id));
  }

  someChecked(set: Set<number>, ids: number[]): boolean {
    return !this.allChecked(set, ids) && ids.some((id) => set.has(id));
  }

  get scheduleIds(): number[] {
    return this.sortedSchedules.map((s) => s.id);
  }

  get historyIds(): number[] {
    return this.sortedHistory.map((r) => r.id);
  }

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.scheduleFormOpen = false;
    this.recordFormOpen = false;
    this.confirmTarget = null;
  }

  // ===== Schedule actions =====

  private emptySchedule(): ScheduleForm {
    return { task: '', kind: 'Preventive', intervalValue: 500, intervalUnit: 'Hours', nextDue: '' };
  }

  openScheduleForm(s?: MaintenanceSchedule): void {
    this.openMenu = null;
    this.editingSchedule = s ?? null;
    if (s) {
      this.scheduleForm = { task: s.task, kind: s.kind, intervalValue: s.intervalValue, intervalUnit: s.intervalUnit, nextDue: s.nextDue };
    } else {
      this.scheduleForm = this.emptySchedule();
      this.scheduleForm.nextDue = toIso(this.advance(today(), this.scheduleForm));
    }
    this.scheduleSubmitted = false;
    this.scheduleFormOpen = true;
  }

  /** Re-suggest the due date when the interval changes on a new schedule. */
  onIntervalChange(): void {
    if (this.editingSchedule || !(this.scheduleForm.intervalValue > 0)) return;
    this.scheduleForm.nextDue = toIso(this.advance(today(), this.scheduleForm));
  }

  get scheduleFormValid(): boolean {
    const f = this.scheduleForm;
    return !!f.task.trim() && f.intervalValue > 0 && Number.isFinite(f.intervalValue) && !!f.nextDue;
  }

  saveSchedule(): void {
    this.scheduleSubmitted = true;
    if (!this.scheduleFormValid) return;
    const values = { ...this.scheduleForm, task: this.scheduleForm.task.trim(), intervalValue: Number(this.scheduleForm.intervalValue) };
    if (this.editingSchedule) {
      Object.assign(this.editingSchedule, values);
      this.showToast(`${values.task} updated.`);
    } else {
      this.data.schedules.push({ id: nextId++, ...values });
      this.showToast(`${values.task} scheduled.`);
    }
    this.scheduleFormOpen = false;
  }

  /** Logs a completed record at today's running hours and rolls the schedule forward one interval. */
  markDone(s: MaintenanceSchedule): void {
    this.openMenu = null;
    this.data.history.push({
      id: nextId++,
      date: toIso(today()),
      task: s.task,
      runningHours: this.data.runningHours,
      status: 'Completed',
      technician: '',
      notes: '',
    });
    s.nextDue = toIso(this.advance(today(), s));
    this.showToast(`${s.task} completed. Next due ${fromIso(s.nextDue).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}.`);
  }

  markCheckedDone(): void {
    this.data.schedules.filter((s) => this.checkedSchedules.has(s.id)).forEach((s) => this.markDone(s));
    const n = this.checkedSchedules.size;
    this.checkedSchedules.clear();
    this.showToast(`${n} task${n === 1 ? '' : 's'} marked done.`);
  }

  // ===== History actions =====

  private emptyRecord(): RecordForm {
    return { date: toIso(today()), task: '', runningHours: null, status: 'Completed', technician: '', notes: '' };
  }

  openRecordForm(r?: MaintenanceRecord): void {
    this.openMenu = null;
    this.editingRecord = r ?? null;
    this.recordForm = r ? { ...r } : { ...this.emptyRecord(), runningHours: this.data.runningHours };
    this.recordSubmitted = false;
    this.recordFormOpen = true;
  }

  get taskSuggestions(): string[] {
    return [...new Set([...this.data.schedules.map((s) => s.task), ...this.data.history.map((r) => r.task)])];
  }

  get recordDateInFuture(): boolean {
    return !!this.recordForm.date && this.daysUntil(this.recordForm.date) > 0;
  }

  get recordHoursInvalid(): boolean {
    const h = this.recordForm.runningHours;
    return h !== null && (h < 0 || h > this.data.runningHours);
  }

  saveRecord(): void {
    this.recordSubmitted = true;
    const f = this.recordForm;
    if (!f.task.trim() || !f.date || this.recordDateInFuture || this.recordHoursInvalid) return;
    const runningHours = f.runningHours === null || (f.runningHours as unknown) === '' ? null : Number(f.runningHours);
    const values = { ...f, task: f.task.trim(), technician: f.technician.trim(), notes: f.notes.trim(), runningHours };
    if (this.editingRecord) {
      Object.assign(this.editingRecord, values);
      this.showToast('Maintenance record updated.');
    } else {
      this.data.history.push({ id: nextId++, ...values });
      this.showToast(`${values.task} recorded.`);
    }
    this.recordFormOpen = false;
  }

  // ===== Delete =====

  askDelete(target: NonNullable<ComponentMaintenance['confirmTarget']>): void {
    this.openMenu = null;
    this.confirmTarget = target;
  }

  confirmDelete(): void {
    const t = this.confirmTarget;
    if (!t) return;
    if (t.kind === 'schedule') {
      this.data.schedules = this.data.schedules.filter((s) => s !== t.item);
      this.checkedSchedules.delete(t.item.id);
      this.showToast(`${t.item.task} schedule removed.`);
    } else {
      this.data.history = this.data.history.filter((r) => r !== t.item);
      this.checkedHistory.delete(t.item.id);
      this.showToast('Maintenance record deleted.');
    }
    this.confirmTarget = null;
  }

  trackById(_: number, row: { id: number }): number {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
