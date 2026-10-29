import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  Clock,
  Ellipsis,
  EllipsisVertical,
  FileText,
  Filter,
  Link,
  Pencil,
  Plus,
  Search,
  X,
} from 'lucide-angular';
import { AssetRecord, TODAY, toIsoDate } from 'shared-ui';
import {
  DUE_SOON_DAYS,
  PM_CATEGORIES,
  PM_FREQUENCIES,
  PM_STATUSES,
  PM_STATUS_COLORS,
  PmCategory,
  PmSchedule,
  PmStatus,
  addDays,
  addMonths,
  daysUntil,
  frequencyMonths,
  pmStatus,
} from '../maintenance-pm';

/** The work-order fields this tab reads. */
export interface PmOrder {
  id: string;
  date: string;
  type: string;
  description: string;
  status: string;
  technician: string;
  labor: number;
  parts: number;
  service: number;
}

export interface PmWorkOrderRequest {
  description: string;
  date: string;
  technician: string;
}

interface PmForm {
  id: string | null;
  name: string;
  description: string;
  component: string;
  category: PmCategory;
  frequency: string;
  lastPerformed: string;
  nextDue: string;
  assignedTo: string;
  durationHours: number;
  estCost: number;
  template: string;
}

interface NextAction {
  key: 'wo' | 'parts' | 'notify';
  text: string;
  date: string;
  done: boolean;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const CW = 440;
const CH = 150;
const PAD = { l: 30, r: 8, t: 8, b: 22 };
/** Compliance series: categorical hues validated with the rest of the page; legend + tooltip carry identity. */
const SERIES = [
  { key: 'completed', label: 'Completed', color: '#16a34a' },
  { key: 'missed', label: 'Missed', color: '#dc2626' },
  { key: 'scheduled', label: 'Scheduled', color: '#2563eb' },
] as const;
type SeriesKey = (typeof SERIES)[number]['key'];
const PM_TYPES = new Set(['Preventive', 'Inspection']);

@Component({
  selector: 'app-pm-schedules',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './pm-schedules.html',
  // shares the page's base styles (cards, tables, buttons, dialogs) with the parent
  styleUrls: ['../asset-maintenance.css', '../asset-maintenance-charts.css', './pm-schedules.css'],
})
export class PmSchedules {
  @Input({ required: true }) asset!: AssetRecord;
  /** Owned by the parent page; this tab edits it in place. */
  @Input({ required: true }) schedules!: PmSchedule[];
  @Input({ required: true }) orders!: PmOrder[];
  @Input() templates: { code: string; name: string }[] = [];
  @Input() lastYearCount = 0;
  @Input() year = TODAY.getFullYear();
  @Output() createWorkOrder = new EventEmitter<PmWorkOrderRequest>();
  @Output() activity = new EventEmitter<string>();
  @Output() notify = new EventEmitter<string>();

  readonly icons = { ArrowDown, ArrowUp, CalendarDays, ChevronLeft, ChevronRight, CircleAlert, CircleCheck, Clock, Ellipsis, EllipsisVertical, FileText, Filter, Link, Pencil, Plus, Search, X };
  readonly categories = PM_CATEGORIES;
  readonly frequencies = PM_FREQUENCIES;
  readonly statuses = PM_STATUSES;
  readonly statusColors = PM_STATUS_COLORS;
  readonly series = SERIES;
  readonly cw = CW;
  readonly ch = CH;
  readonly pad = PAD;
  readonly pageSizes = [10, 25, 50];
  readonly Math = Math;
  private readonly today = toIsoDate(TODAY);

  search = '';
  statusFilter: PmStatus | '' = '';
  categoryFilter: PmCategory | '' = '';
  frequencyFilter = '';
  assigneeFilter = '';
  advOpen = false;
  pageSize = 10;
  page = 0;
  readonly checked = new Set<string>();
  selectedId: string | null = null;
  panelOpen = true;
  detailTab: 'details' | 'history' | 'tasks' | 'checklist' | 'documents' = 'details';
  menuOpen = '';
  hoverMonth: number | null = null;
  hoverStatus: PmStatus | null = null;
  form: PmForm | null = null;
  formError = '';
  linkForm: { scheduleId: string; template: string } | null = null;

  // ===== Status & KPIs =====

  status(s: PmSchedule): PmStatus {
    return pmStatus(s, this.today);
  }

  statusClass(s: PmStatus): string {
    return s === 'On Schedule' ? 'pm-st--ok' : s === 'Due Soon' ? 'pm-st--soon' : 'pm-st--late';
  }

  private get pmOrders(): PmOrder[] {
    return this.orders.filter((o) => PM_TYPES.has(o.type));
  }

  get kpis(): { total: number; totalChange: number; completed: number; compliance: number; overdue: number; overduePct: number; dueSoon: number; dueSoonPct: number } {
    const total = this.schedules.length;
    const y = String(this.year);
    const thisYear = this.pmOrders.filter((o) => o.date.startsWith(y));
    const completed = thisYear.filter((o) => o.status === 'Completed').length;
    const overdue = this.schedules.filter((s) => this.status(s) === 'Overdue').length;
    const missed = thisYear.filter((o) => o.status !== 'Completed' && o.date < this.today).length + overdue;
    const dueSoon = this.schedules.filter((s) => this.status(s) === 'Due Soon').length;
    const pct = (n: number) => (total ? Math.round((n / total) * 100) : 0);
    return {
      total,
      totalChange: this.lastYearCount ? Math.round(((total - this.lastYearCount) / this.lastYearCount) * 100) : 0,
      completed,
      compliance: completed + missed ? Math.round((completed / (completed + missed)) * 100) : 100,
      overdue,
      overduePct: pct(overdue),
      dueSoon,
      dueSoonPct: pct(dueSoon),
    };
  }

  // ===== Table =====

  get assignees(): string[] {
    return [...new Set(this.schedules.map((s) => s.assignedTo))].sort();
  }

  get rows(): PmSchedule[] {
    const q = this.search.trim().toLowerCase();
    return this.schedules
      .filter((s) => (!this.statusFilter || this.status(s) === this.statusFilter) && (!this.categoryFilter || s.category === this.categoryFilter))
      .filter((s) => (!this.frequencyFilter || s.frequency === this.frequencyFilter) && (!this.assigneeFilter || s.assignedTo === this.assigneeFilter))
      .filter((s) => !q || `${s.name} ${s.description} ${s.component} ${s.template}`.toLowerCase().includes(q));
  }

  get pageRows(): PmSchedule[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showing(): string {
    const n = this.rows.length;
    return n ? `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records` : 'No records';
  }

  onFilter(): void {
    this.page = 0;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((s) => this.checked.has(s.id));
  }

  toggleAll(on: boolean): void {
    for (const s of this.pageRows) on ? this.checked.add(s.id) : this.checked.delete(s.id);
  }

  toggleCheck(id: string, on: boolean): void {
    on ? this.checked.add(id) : this.checked.delete(id);
  }

  fmtDate(iso: string): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '—';
  }

  // ===== Side panel =====

  select(s: PmSchedule): void {
    this.selectedId = s.id;
    this.panelOpen = true;
    this.detailTab = 'details';
    this.menuOpen = '';
  }

  get selected(): PmSchedule | null {
    if (!this.panelOpen) return null;
    return this.schedules.find((s) => s.id === this.selectedId) ?? this.rows[0] ?? null;
  }

  historyOf(s: PmSchedule): PmOrder[] {
    const words = s.match.map((m) => m.toLowerCase());
    return this.orders
      .filter((o) => o.status === 'Completed' && words.some((m) => o.description.toLowerCase().includes(m)))
      .sort((a, b) => b.date.localeCompare(a.date));
  }

  private openOrderFor(s: PmSchedule): PmOrder | undefined {
    return this.orders.find((o) => o.status !== 'Completed' && o.description === s.name);
  }

  nextActions(s: PmSchedule): NextAction[] {
    const done = new Set(s.actionsDone ?? []);
    // lead times before the due date; never in the past
    const by = (days: number) => {
      const d = addDays(s.nextDue, -days);
      return d < this.today ? this.today : d;
    };
    const list: NextAction[] = [{ key: 'wo', text: 'Schedule work order', date: by(0), done: done.has('wo') || !!this.openOrderFor(s) }];
    if (s.parts && s.parts !== 'none') list.push({ key: 'parts', text: `Reserve spare parts (${s.parts})`, date: by(3), done: done.has('parts') });
    list.push({ key: 'notify', text: 'Notify assigned technician', date: by(3), done: done.has('notify') });
    return list;
  }

  toggleAction(s: PmSchedule, a: NextAction): void {
    if (a.key === 'wo' && this.openOrderFor(s)) return;
    const done = new Set(s.actionsDone ?? []);
    done.has(a.key) ? done.delete(a.key) : done.add(a.key);
    s.actionsDone = [...done];
  }

  requestWorkOrder(s: PmSchedule): void {
    this.menuOpen = '';
    const date = s.nextDue < this.today ? this.today : s.nextDue;
    this.createWorkOrder.emit({ description: s.name, date, technician: s.assignedTo });
  }

  markPerformed(s: PmSchedule): void {
    this.menuOpen = '';
    s.lastPerformed = this.today;
    s.nextDue = addMonths(this.today, frequencyMonths(s.frequency));
    s.actionsDone = [];
    this.activity.emit(`PM performed (${s.name})`);
    this.notify.emit(`${s.name} recorded as performed today. Next due ${this.fmtDate(s.nextDue)}.`);
  }

  remove(s: PmSchedule): void {
    this.menuOpen = '';
    if (!confirm(`Delete the PM schedule "${s.name}"?`)) return;
    const i = this.schedules.indexOf(s);
    if (i >= 0) this.schedules.splice(i, 1);
    this.checked.delete(s.id);
    if (this.selectedId === s.id) this.selectedId = null;
    this.activity.emit(`PM schedule removed (${s.name})`);
  }

  // ===== Add / edit =====

  openForm(s?: PmSchedule): void {
    this.menuOpen = '';
    this.formError = '';
    this.form = s
      ? { id: s.id, name: s.name, description: s.description, component: s.component, category: s.category, frequency: s.frequency, lastPerformed: s.lastPerformed, nextDue: s.nextDue, assignedTo: s.assignedTo, durationHours: s.durationHours, estCost: s.estCost, template: s.template }
      : { id: null, name: '', description: '', component: '', category: 'Routine', frequency: 'Monthly', lastPerformed: '', nextDue: '', assignedTo: this.assignees[0] ?? '', durationHours: 1, estCost: 0, template: '' };
  }

  /** Changing the last date or the frequency proposes the next due date. */
  onFormDates(): void {
    const f = this.form;
    if (f?.lastPerformed) f.nextDue = addMonths(f.lastPerformed, frequencyMonths(f.frequency));
  }

  saveForm(): void {
    const f = this.form;
    if (!f) return;
    if (!f.name.trim()) return void (this.formError = 'Enter the PM name.');
    if (!f.nextDue) return void (this.formError = 'Enter the next due date (or the last performed date).');
    if (f.lastPerformed && f.nextDue <= f.lastPerformed) return void (this.formError = 'Next due date must be after the last performed date.');
    const fields = {
      name: f.name.trim(), description: f.description.trim(), component: f.component.trim() || 'Main unit', category: f.category, frequency: f.frequency,
      lastPerformed: f.lastPerformed, nextDue: f.nextDue, assignedTo: f.assignedTo.trim() || 'Maintenance Team', durationHours: Number(f.durationHours) || 0,
      estCost: Number(f.estCost) || 0, template: f.template,
    };
    const existing = f.id ? this.schedules.find((s) => s.id === f.id) : undefined;
    if (existing) {
      if (existing.nextDue !== fields.nextDue) existing.actionsDone = [];
      Object.assign(existing, fields);
      this.notify.emit(`${existing.name} updated.`);
    } else {
      const n = Math.max(0, ...this.schedules.map((s) => Number(s.id.split('-')[1]) || 0)) + 1;
      const s: PmSchedule = {
        id: `PM-${String(n).padStart(2, '0')}`, ...fields, parts: 'as per checklist', match: [fields.name.split(/[ ,]/)[0].toLowerCase()],
        tasks: ['Isolate the unit', fields.name, 'Test run', 'Record readings'], checklist: ['Permit / lock-out in place', 'Work done as per procedure', 'PM record signed'], documents: [],
      };
      this.schedules.push(s);
      this.selectedId = s.id;
      this.panelOpen = true;
      this.activity.emit(`PM schedule added (${s.name})`);
      this.notify.emit(`${s.name} added. Next due ${this.fmtDate(s.nextDue)}.`);
    }
    this.form = null;
  }

  // ===== Link template =====

  openLink(): void {
    this.menuOpen = '';
    const s = this.selected ?? this.schedules[0];
    if (!s) return;
    this.linkForm = { scheduleId: s.id, template: s.template || this.templates[0]?.code || '' };
  }

  saveLink(): void {
    const f = this.linkForm;
    const s = f && this.schedules.find((x) => x.id === f.scheduleId);
    if (!f || !s || !f.template) return;
    s.template = f.template;
    this.linkForm = null;
    this.notify.emit(`${s.name} linked to template ${f.template}.`);
  }

  templateName(code: string): string {
    return this.templates.find((t) => t.code === code)?.name ?? '';
  }

  // ===== Charts =====

  /** Last 12 months (ending this month): completed, missed and scheduled PM jobs. */
  get compliance(): { bars: { label: string; x: number; rects: { d: string; color: string }[]; values: { label: string; color: string; value: number }[]; total: number }[]; ticks: { y: number; label: string }[]; barW: number } {
    const end = new Date(TODAY.getFullYear(), TODAY.getMonth(), 1);
    const months = Array.from({ length: 12 }, (_, i) => new Date(end.getFullYear(), end.getMonth() - 11 + i, 1));
    const key = (iso: string) => iso.slice(0, 7);
    const pm = this.pmOrders;
    const counts = months.map((m) => {
      const k = `${m.getFullYear()}-${String(m.getMonth() + 1).padStart(2, '0')}`;
      const c: Record<SeriesKey, number> = { completed: 0, missed: 0, scheduled: 0 };
      for (const o of pm) {
        if (key(o.date) !== k) continue;
        if (o.status === 'Completed') c.completed++;
        else if (o.date < this.today) c.missed++;
        else c.scheduled++;
      }
      for (const s of this.schedules) {
        if (key(s.nextDue) !== k || this.openOrderFor(s)) continue;
        if (s.nextDue < this.today) c.missed++;
        else c.scheduled++;
      }
      return c;
    });
    const max = Math.max(4, Math.ceil(Math.max(...counts.map((c) => c.completed + c.missed + c.scheduled)) / 2) * 2);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / 12;
    const barW = Math.min(16, slot * 0.55);
    const bars = counts.map((c, i) => {
      const x = PAD.l + slot * i + (slot - barW) / 2;
      const present = SERIES.filter((s) => c[s.key] > 0);
      let y = CH - PAD.b;
      const rects = present.map((s, k) => {
        const h = (c[s.key] / max) * plotH;
        y -= h;
        const rh = h - (k ? 2 : 0);
        const rr = k === present.length - 1 ? Math.min(4, rh) : 0;
        return { color: s.color, d: `M${x},${y + rh}V${y + rr}${rr ? `Q${x},${y} ${x + rr},${y}` : ''}H${x + barW - rr}${rr ? `Q${x + barW},${y} ${x + barW},${y + rr}` : ''}V${y + rh}Z` };
      });
      const m = months[i];
      return {
        label: MONTHS[m.getMonth()], x, rects, total: c.completed + c.missed + c.scheduled,
        values: SERIES.map((s) => ({ label: s.label, color: s.color, value: c[s.key] })),
        year: m.getFullYear(),
      };
    });
    const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => ({ y: CH - PAD.b - f * plotH, label: String(Math.round(max * f)) }));
    return { bars, ticks, barW };
  }

  get distribution(): { total: number; rows: { status: PmStatus; count: number; pct: number; color: string; dash: string; offset: number }[] } {
    const total = this.schedules.length;
    const C = 2 * Math.PI * 52;
    let acc = 0;
    const rows = PM_STATUSES.map((status) => {
      const count = this.schedules.filter((s) => this.status(s) === status).length;
      const len = total ? (count / total) * C : 0;
      const row = { status, count, pct: total ? Math.round((count / total) * 100) : 0, color: PM_STATUS_COLORS[status], dash: `${Math.max(0, len - 2)} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  get hoverCount(): number | null {
    return this.hoverStatus ? (this.distribution.rows.find((r) => r.status === this.hoverStatus)?.count ?? 0) : null;
  }

  dueLabel(s: PmSchedule): string {
    const d = daysUntil(s.nextDue, this.today);
    return d < 0 ? `${-d} days overdue` : d === 0 ? 'due today' : d <= DUE_SOON_DAYS ? `in ${d} days` : '';
  }

  // ===== Export & menus =====

  export(): void {
    this.menuOpen = '';
    const rows = this.rows
      .filter((s) => !this.checked.size || this.checked.has(s.id))
      .map((s, i) => ({
        '#': i + 1, 'PM Name': s.name, 'Asset Component': s.component, Category: s.category, Frequency: s.frequency, 'Next Due Date': s.nextDue,
        'Last Performed': s.lastPerformed, Status: this.status(s), 'Assigned To': s.assignedTo, 'Est. Duration (h)': s.durationHours, 'Est. Cost (AED)': s.estCost, Template: s.template,
      }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'PM Schedules');
    XLSX.writeFile(book, `${this.asset.tag}_PM_Schedules.xlsx`);
  }

  print(): void {
    this.menuOpen = '';
    window.print();
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen = '';
    this.form = null;
    this.linkForm = null;
  }
}
