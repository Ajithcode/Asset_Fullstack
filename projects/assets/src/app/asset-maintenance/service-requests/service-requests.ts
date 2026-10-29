import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  Building2,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleUser,
  Ellipsis,
  EllipsisVertical,
  FileText,
  Flag,
  Folder,
  Hash,
  MapPin,
  Paperclip,
  Pencil,
  Plus,
  Search,
  Type,
  User,
  Wrench,
  X,
  Activity,
  List,
  Package,
} from 'lucide-angular';
import { AssetRecord, TODAY, toIsoDate } from 'shared-ui';
import {
  SR_CATEGORIES,
  SR_CATEGORY_COLORS,
  SR_PRIORITIES,
  SR_SLA,
  SR_STATUSES,
  SR_STATUS_COLORS,
  ServiceRequest,
  SrCategory,
  SrPriority,
  SrStatus,
  isOpen,
  slaOf,
} from '../maintenance-sr';

export interface SrWorkOrderRequest {
  requestId: string;
  description: string;
  technician: string;
}

interface SrForm {
  id: string | null;
  title: string;
  description: string;
  category: SrCategory;
  priority: SrPriority;
  requestedBy: string;
  requesterDept: string;
  assignedTo: string;
  targetDate: string;
}

type Period = '30' | '180' | 'year' | 'all';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const CW = 300;
const CH = 150;
const PAD = { l: 26, r: 8, t: 14, b: 22 };
const CURRENT_USER = 'Rex Kumar';
const TECHNICIANS = ['Ahmed Khan', 'Ravi Patel', 'Nisha Patel', 'Ramesh Kumar', 'Salim Ahmed', 'Khalid Mansoori', 'Priya Nair'];

@Component({
  selector: 'app-service-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './service-requests.html',
  // shares the page's base styles (cards, tables, buttons, dialogs) with the parent
  styleUrls: ['../asset-maintenance.css', '../asset-maintenance-charts.css', './service-requests.css'],
})
export class ServiceRequests {
  @Input({ required: true }) asset!: AssetRecord;
  @Input({ required: true }) location = '';
  /** Owned by the parent page; this tab edits it in place. */
  @Input({ required: true }) requests!: ServiceRequest[];
  @Output() convert = new EventEmitter<SrWorkOrderRequest>();
  @Output() activity = new EventEmitter<string>();
  @Output() notify = new EventEmitter<string>();
  @Output() openWorkOrder = new EventEmitter<string>();

  readonly icons = { Activity, List, Building2, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleUser, Ellipsis, EllipsisVertical, FileText, Flag, Folder, Hash, MapPin, Package, Paperclip, Pencil, Plus, Search, Type, User, Wrench, X };
  readonly statuses = SR_STATUSES;
  readonly priorities = SR_PRIORITIES;
  readonly categories = SR_CATEGORIES;
  readonly statusColors = SR_STATUS_COLORS;
  readonly sla = SR_SLA;
  readonly technicians = TECHNICIANS;
  readonly cw = CW;
  readonly ch = CH;
  readonly pad = PAD;
  readonly pageSizes = [10, 25, 50];

  period: Period = '180';
  search = '';
  statusFilter: SrStatus | '' = '';
  categoryFilter: SrCategory | '' = '';
  priorityFilter: SrPriority | '' = '';
  pageSize = 10;
  page = 0;
  readonly checked = new Set<string>();
  selectedId: string | null = null;
  panelOpen = true;
  detailTab: 'details' | 'updates' | 'attachments' | 'related' = 'details';
  menuOpen = '';
  hoverStatus: SrStatus | null = null;
  hoverCat: number | null = null;
  hoverTrend: number | null = null;
  form: SrForm | null = null;
  formError = '';
  /** Resolve / close dialog. */
  closing: { id: string; to: 'Resolved' | 'Closed'; note: string; rating: number } | null = null;
  slaOpen = false;

  // ===== Rows =====

  private get since(): string {
    if (this.period === 'all') return '';
    if (this.period === 'year') return `${TODAY.getFullYear()}-01-01`;
    const d = new Date(TODAY);
    d.setDate(d.getDate() - Number(this.period));
    return toIsoDate(d);
  }

  /** Requests in the selected period (the basis for the table and charts). */
  get inPeriod(): ServiceRequest[] {
    const since = this.since;
    return this.requests.filter((r) => !since || r.date >= since || isOpen(r));
  }

  get rows(): ServiceRequest[] {
    const q = this.search.trim().toLowerCase();
    return this.inPeriod
      .filter((r) => (!this.statusFilter || r.status === this.statusFilter) && (!this.categoryFilter || r.category === this.categoryFilter) && (!this.priorityFilter || r.priority === this.priorityFilter))
      .filter((r) => !q || `${r.id} ${r.title} ${r.category} ${r.status} ${r.requestedBy} ${r.assignedTo}`.toLowerCase().includes(q))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  get pageRows(): ServiceRequest[] {
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
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.id));
  }

  toggleAll(on: boolean): void {
    for (const r of this.pageRows) on ? this.checked.add(r.id) : this.checked.delete(r.id);
  }

  toggleCheck(id: string, on: boolean): void {
    on ? this.checked.add(id) : this.checked.delete(id);
  }

  fmtDate(iso: string): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '—';
  }

  fmtDateTime(v: string): string {
    return v ? `${this.fmtDate(v.slice(0, 10))} ${v.slice(11, 16)}` : '—';
  }

  statusClass(s: SrStatus): string {
    return 'sr-st--' + s.toLowerCase().replace(' ', '-');
  }

  prioClass(p: SrPriority): string {
    return 'prio--' + p.toLowerCase();
  }

  // ===== Side panel =====

  select(r: ServiceRequest): void {
    this.selectedId = r.id;
    this.panelOpen = true;
    this.detailTab = 'details';
    this.menuOpen = '';
  }

  get selected(): ServiceRequest | null {
    if (!this.panelOpen) return null;
    return this.requests.find((r) => r.id === this.selectedId) ?? this.rows[0] ?? null;
  }

  slaFor(r: ServiceRequest) {
    return slaOf(r);
  }

  get latestUpdate(): ServiceRequest['updates'][number] | null {
    const s = this.selected;
    return s?.updates.length ? s.updates[s.updates.length - 1] : null;
  }

  /** Related items: the linked work order and the asset itself. */
  relatedOf(r: ServiceRequest): { label: string; value: string; wo?: string }[] {
    return [...(r.workOrder ? [{ label: 'Work Order', value: r.workOrder, wo: r.workOrder }] : []), { label: 'Asset', value: `${this.asset.tag} (${this.asset.name})` }];
  }

  private log(r: ServiceRequest, text: string): void {
    r.updates.push({ at: this.nowStamp(), by: CURRENT_USER, text });
  }

  private nowStamp(): string {
    const d = new Date();
    const p = (n: number) => String(n).padStart(2, '0');
    return `${toIsoDate(d)}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  // ===== Actions =====

  openForm(r?: ServiceRequest): void {
    this.menuOpen = '';
    this.formError = '';
    this.form = r
      ? { id: r.id, title: r.title, description: r.description, category: r.category, priority: r.priority, requestedBy: r.requestedBy, requesterDept: r.requesterDept, assignedTo: r.assignedTo, targetDate: r.targetDate }
      : { id: null, title: '', description: '', category: 'Mechanical', priority: 'Medium', requestedBy: CURRENT_USER, requesterDept: `${this.asset.department} Department`, assignedTo: '', targetDate: this.targetFor('Medium') };
  }

  private targetFor(p: SrPriority): string {
    const d = new Date(TODAY);
    d.setDate(d.getDate() + SR_SLA[p].resolutionDays);
    return toIsoDate(d);
  }

  /** A new request's target follows its priority's resolution SLA. */
  onFormPriority(): void {
    if (this.form && !this.form.id) this.form.targetDate = this.targetFor(this.form.priority);
  }

  saveForm(): void {
    const f = this.form;
    if (!f) return;
    if (!f.title.trim()) return void (this.formError = 'Enter the subject.');
    if (!f.targetDate) return void (this.formError = 'Enter the target date.');
    const existing = f.id ? this.requests.find((r) => r.id === f.id) : undefined;
    if (existing) {
      const assigned = !existing.assignedTo && f.assignedTo;
      Object.assign(existing, { title: f.title.trim(), description: f.description.trim(), category: f.category, priority: f.priority, requestedBy: f.requestedBy.trim(), requesterDept: f.requesterDept.trim(), assignedTo: f.assignedTo, targetDate: f.targetDate });
      if (assigned) this.assignTo(existing, f.assignedTo, false);
      this.log(existing, 'Request details updated.');
      this.notify.emit(`${existing.id} updated.`);
    } else {
      const year = TODAY.getFullYear();
      const n = Math.max(0, ...this.requests.map((r) => Number(r.id.split('-')[2]) || 0)) + 1;
      const at = this.nowStamp();
      const r: ServiceRequest = {
        id: `SR-${year}-${String(n).padStart(3, '0')}`, date: at.slice(0, 10), title: f.title.trim(), description: f.description.trim(), category: f.category, priority: f.priority,
        status: 'Open', createdAt: at, requestedBy: f.requestedBy.trim() || CURRENT_USER, requesterDept: f.requesterDept.trim(), assignedTo: '', respondedAt: '', resolvedAt: '',
        targetDate: f.targetDate, workOrder: '', rating: 0, updates: [{ at, by: f.requestedBy.trim() || CURRENT_USER, text: 'Service request created.' }], attachments: [],
      };
      this.requests.unshift(r);
      if (f.assignedTo) this.assignTo(r, f.assignedTo, false);
      this.selectedId = r.id;
      this.panelOpen = true;
      this.activity.emit(`Service request ${r.id} created`);
      this.notify.emit(`${r.id} created for ${this.asset.tag}.`);
    }
    this.form = null;
  }

  private assignTo(r: ServiceRequest, who: string, announce = true): void {
    r.assignedTo = who;
    if (!r.respondedAt) r.respondedAt = this.nowStamp();
    if (r.status === 'Open') r.status = 'In Progress';
    this.log(r, `Assigned to ${who}.`);
    if (announce) this.notify.emit(`${r.id} assigned to ${who}.`);
  }

  assign(r: ServiceRequest, who: string): void {
    this.menuOpen = '';
    if (who && who !== r.assignedTo) this.assignTo(r, who);
  }

  convertToWorkOrder(r: ServiceRequest): void {
    this.menuOpen = '';
    if (r.workOrder) return void this.openWorkOrder.emit(r.workOrder);
    this.convert.emit({ requestId: r.id, description: r.title, technician: r.assignedTo || TECHNICIANS[0] });
  }

  /** Called by the page once the work order is saved. */
  linkWorkOrder(requestId: string, workOrder: string, technician: string): void {
    const r = this.requests.find((x) => x.id === requestId);
    if (!r) return;
    r.workOrder = workOrder;
    if (!r.assignedTo) r.assignedTo = technician;
    if (!r.respondedAt) r.respondedAt = this.nowStamp();
    if (r.status === 'Open') r.status = 'In Progress';
    this.log(r, `Converted to ${workOrder}.`);
  }

  startClose(r: ServiceRequest, to: 'Resolved' | 'Closed'): void {
    this.menuOpen = '';
    this.closing = { id: r.id, to, note: '', rating: r.rating || 0 };
  }

  confirmClose(): void {
    const c = this.closing;
    const r = c && this.requests.find((x) => x.id === c.id);
    if (!c || !r) return;
    const at = this.nowStamp();
    if (!r.respondedAt) r.respondedAt = at;
    if (!r.resolvedAt) r.resolvedAt = at;
    r.status = c.to;
    if (c.rating) r.rating = c.rating;
    this.log(r, `${c.to}.${c.note.trim() ? ' ' + c.note.trim() : ''}`);
    this.activity.emit(`Service request ${r.id} ${c.to.toLowerCase()}`);
    this.notify.emit(`${r.id} marked ${c.to.toLowerCase()}.`);
    this.closing = null;
  }

  reopen(r: ServiceRequest): void {
    this.menuOpen = '';
    r.status = r.assignedTo ? 'In Progress' : 'Open';
    r.resolvedAt = '';
    this.log(r, 'Reopened.');
    this.notify.emit(`${r.id} reopened.`);
  }

  remove(r: ServiceRequest): void {
    this.menuOpen = '';
    if (!confirm(`Delete service request ${r.id}?`)) return;
    const i = this.requests.indexOf(r);
    if (i >= 0) this.requests.splice(i, 1);
    this.checked.delete(r.id);
    if (this.selectedId === r.id) this.selectedId = null;
  }

  // ===== Charts =====

  get byStatus(): { total: number; rows: { status: SrStatus; count: number; pct: number; color: string; dash: string; offset: number }[] } {
    const list = this.inPeriod;
    const total = list.length;
    const C = 2 * Math.PI * 52;
    let acc = 0;
    const order: SrStatus[] = ['Resolved', 'Open', 'In Progress', 'Closed'];
    const rows = order.map((status) => {
      const count = list.filter((r) => r.status === status).length;
      const len = total ? (count / total) * C : 0;
      const row = { status, count, pct: total ? Math.round((count / total) * 100) : 0, color: SR_STATUS_COLORS[status], dash: `${Math.max(0, len - 2)} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  get hoverStatusCount(): number | null {
    return this.hoverStatus ? (this.byStatus.rows.find((r) => r.status === this.hoverStatus)?.count ?? 0) : null;
  }

  get byCategory(): { bars: { label: string; value: number; x: number; y: number; d: string; color: string }[]; ticks: { y: number; label: string }[]; barW: number } {
    const list = this.inPeriod;
    const values = SR_CATEGORIES.map((c) => list.filter((r) => r.category === c).length);
    const max = Math.max(4, ...values);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / SR_CATEGORIES.length;
    const barW = Math.min(40, slot * 0.6);
    const bars = SR_CATEGORIES.map((c, i) => {
      const x = PAD.l + slot * i + (slot - barW) / 2;
      const h = (values[i] / max) * plotH;
      const y = CH - PAD.b - h;
      const rr = Math.min(4, h);
      const d = h ? `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + barW - rr}Q${x + barW},${y} ${x + barW},${y + rr}V${y + h}Z` : '';
      return { label: c, value: values[i], x, y, d, color: SR_CATEGORY_COLORS[c] };
    });
    const ticks = Array.from({ length: max + 1 }, (_, i) => ({ y: CH - PAD.b - (i / max) * plotH, label: String(i) }));
    return { bars, ticks, barW };
  }

  /** Last six months: requests created (bars) and resolved (line). */
  get trend(): { months: { label: string; created: number; resolved: number; x: number; barY: number; lineY: number; d: string }[]; line: string; ticks: { y: number; label: string }[]; barW: number } {
    const months = Array.from({ length: 6 }, (_, i) => new Date(TODAY.getFullYear(), TODAY.getMonth() - 5 + i, 1));
    const key = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    const created = months.map((m) => this.requests.filter((r) => r.createdAt.startsWith(key(m))).length);
    const resolved = months.map((m) => this.requests.filter((r) => r.resolvedAt.startsWith(key(m))).length);
    const max = Math.max(4, ...created, ...resolved);
    const plotH = CH - PAD.t - PAD.b;
    const slot = (CW - PAD.l - PAD.r) / 6;
    const barW = Math.min(22, slot * 0.5);
    const out = months.map((m, i) => {
      const x = PAD.l + slot * i + (slot - barW) / 2;
      const h = (created[i] / max) * plotH;
      const barY = CH - PAD.b - h;
      const rr = Math.min(4, h);
      const d = h ? `M${x},${barY + h}V${barY + rr}Q${x},${barY} ${x + rr},${barY}H${x + barW - rr}Q${x + barW},${barY} ${x + barW},${barY + rr}V${barY + h}Z` : '';
      return { label: MONTHS[m.getMonth()], created: created[i], resolved: resolved[i], x, barY, lineY: CH - PAD.b - (resolved[i] / max) * plotH, d };
    });
    const line = out.map((p, i) => `${i ? 'L' : 'M'}${p.x + barW / 2},${p.lineY}`).join('');
    const ticks = Array.from({ length: max + 1 }, (_, i) => ({ y: CH - PAD.b - (i / max) * plotH, label: String(i) }));
    return { months: out, line, ticks, barW };
  }

  // ===== Export & menus =====

  export(): void {
    this.menuOpen = '';
    const rows = this.rows
      .filter((r) => !this.checked.size || this.checked.has(r.id))
      .map((r, i) => ({
        '#': i + 1, 'SR Number': r.id, Subject: r.title, Category: r.category, Priority: r.priority, Status: r.status, Created: r.createdAt.replace('T', ' '),
        'Requested By': r.requestedBy, 'Assigned To': r.assignedTo, 'Target Date': r.targetDate, 'Work Order': r.workOrder, Rating: r.rating || '',
      }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Service Requests');
    XLSX.writeFile(book, `${this.asset.tag}_Service_Requests.xlsx`);
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
    this.closing = null;
    this.slaOpen = false;
  }
}
