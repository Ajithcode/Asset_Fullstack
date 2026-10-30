import { Component, HostListener, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  Bell,
  BellRing,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Copy,
  EllipsisVertical,
  Eye,
  FileText,
  HeartPulse,
  History,
  Inbox,
  Paperclip,
  Plus,
  Search,
  Settings,
  SquarePen,
  Trash2,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';

export type ServiceType = 'Preventive' | 'Corrective' | 'Inspection';
export type ServiceStatus = 'Completed' | 'In Progress' | 'Scheduled' | 'Cancelled';
export type HealthRating = 'Good' | 'Fair' | 'Poor';

export interface PartUsed {
  id: number;
  name: string;
  partNumber: string;
  quantity: number;
  unitCost: number;
}

export interface ServiceDocument {
  id: number;
  name: string;
  /** Bytes; only the file's name and size are kept in this demo, not its contents. */
  size: number;
}

export interface ServiceRecord {
  id: number;
  reference: string;
  /** ISO dates (yyyy-mm-dd); nextDue is empty when no follow-up is planned. */
  date: string;
  nextDue: string;
  type: ServiceType;
  description: string;
  performedBy: string;
  technician: string;
  location: string;
  /** 24-hour "HH:mm"; empty when not recorded. */
  startTime: string;
  endTime: string;
  runningHours: number | null;
  status: ServiceStatus;
  remarks: string;
  workPerformed: string[];
  parts: PartUsed[];
  labourCost: number;
  otherCost: number;
  documents: ServiceDocument[];
  health: HealthRating;
  /** 0–100 condition score after the service. */
  healthScore: number;
  /** Reminder date for the next service, once one has been created. */
  reminder: string;
}

type DetailTab = 'details' | 'work' | 'parts' | 'cost' | 'documents';
type RecordForm = Omit<ServiceRecord, 'id' | 'reference' | 'parts' | 'documents' | 'reminder' | 'workPerformed'> & { workText: string };

const DAY_MS = 86_400_000;
const MAX_DOC_BYTES = 10 * 1024 * 1024;

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

function addMonths(d: Date, months: number): Date {
  const out = new Date(d);
  out.setMonth(out.getMonth() + months);
  return out;
}

interface Template {
  type: ServiceType;
  description: string;
  work: string[];
  parts: [name: string, partNumber: string, qty: number, unitCost: number][];
}

/** Cycled (newest first) to build a believable service log; mirrors the design mock. */
const TEMPLATES: Template[] = [
  {
    type: 'Preventive',
    description: '500 Hours Service',
    work: ['Changed engine oil', 'Replaced oil filter', 'Replaced fuel filter', 'Replaced air filter', 'Checked belts and hoses', 'Load test 30 min'],
    parts: [['Engine Oil 15W-40 (L)', 'CAT-DEO-15W40', 24, 18], ['Oil Filter', '1R-0739', 1, 145], ['Fuel Filter', '1R-0751', 1, 160], ['Air Filter', '1R-1808', 1, 210]],
  },
  {
    type: 'Corrective',
    description: 'Oil filter replacement',
    work: ['Diagnosed oil pressure drop', 'Replaced clogged oil filter', 'Topped up engine oil'],
    parts: [['Oil Filter', '1R-0739', 1, 145], ['Engine Oil 15W-40 (L)', 'CAT-DEO-15W40', 4, 18]],
  },
  {
    type: 'Preventive',
    description: 'Air filter replacement',
    work: ['Inspected air intake', 'Replaced air filter element', 'Reset restriction indicator'],
    parts: [['Air Filter', '1R-1808', 1, 210]],
  },
  {
    type: 'Inspection',
    description: 'Routine inspection',
    work: ['Visual inspection', 'Checked fluid levels', 'Checked for leaks', 'Battery voltage check'],
    parts: [],
  },
  {
    type: 'Corrective',
    description: 'Turbo charger issue',
    work: ['Investigated boost loss', 'Replaced turbo oil feed seal', 'Test run under load'],
    parts: [['Turbo Seal Kit', '358-1471-SK', 1, 420]],
  },
  {
    type: 'Preventive',
    description: 'Fuel filter replacement',
    work: ['Replaced primary fuel filter', 'Replaced secondary fuel filter', 'Bled fuel system'],
    parts: [['Fuel Filter', '1R-0751', 2, 160]],
  },
  {
    type: 'Corrective',
    description: 'Overheating - cooling system',
    work: ['Flushed cooling system', 'Replaced thermostat', 'Refilled coolant', 'Pressure-tested radiator'],
    parts: [['Thermostat', '245-7590-T', 1, 190], ['Coolant ELC (L)', 'CAT-ELC-50', 20, 12]],
  },
  {
    type: 'Preventive',
    description: 'Oil & fuel filter replacement',
    work: ['Changed engine oil', 'Replaced oil filter', 'Replaced fuel filter'],
    parts: [['Oil Filter', '1R-0739', 1, 145], ['Fuel Filter', '1R-0751', 1, 160], ['Engine Oil 15W-40 (L)', 'CAT-DEO-15W40', 24, 18]],
  },
  {
    type: 'Inspection',
    description: 'General inspection',
    work: ['Visual inspection', 'Checked mounting bolts', 'Reviewed controller fault log'],
    parts: [],
  },
];

const VENDORS = ['Caterpillar UAE', 'DCS Service'];
const TECHNICIANS = ['John Mathew', 'Rashid Ali', 'Hamad Lateef', 'Ramesh Kumar'];

/** Deterministic sample log for a component: one record roughly every 3–7 months, newest first. */
export function buildMockServiceHistory(c: AssetComponent): ServiceRecord[] {
  const records: ServiceRecord[] = [];
  // Stagger the latest record per component so an asset-wide activity feed isn't all one date.
  let date = addMonths(today(), -2);
  date.setDate(date.getDate() - (c.id % 9) * 4);
  const baseHours = c.lifeUnit === 'hrs' && c.lifeUsed ? c.lifeUsed : 1250;
  for (let i = 0; i < 18; i++) {
    const t = TEMPLATES[i % TEMPLATES.length];
    const iso = toIso(date);
    const hasFollowUp = t.type !== 'Corrective';
    const vendor = VENDORS[(i + (t.type === 'Corrective' ? 1 : 0)) % VENDORS.length];
    records.push({
      id: nextId++,
      reference: `SRV-${date.getFullYear()}-${String(18 - i + (c.id % 9) * 20).padStart(4, '0')}`,
      date: iso,
      nextDue: hasFollowUp ? toIso(addMonths(date, t.type === 'Inspection' ? 6 : 5)) : '',
      type: t.type,
      description: t.description,
      performedBy: vendor,
      technician: TECHNICIANS[(i + c.id) % TECHNICIANS.length],
      // Empty means "at the asset's own location".
      location: '',
      startTime: '10:00',
      endTime: t.type === 'Inspection' ? '11:30' : '14:30',
      runningHours: Math.max(0, baseHours - i * 250),
      status: 'Completed',
      remarks:
        i === 0
          ? 'Completed 500 hours scheduled service. Engine oil, oil filter, fuel filter and air filter replaced. Overall condition good.'
          : `${t.description} completed. No further issues observed.`,
      workPerformed: [...t.work],
      parts: t.parts.map(([name, partNumber, quantity, unitCost]) => ({ id: nextId++, name, partNumber, quantity, unitCost })),
      labourCost: t.type === 'Inspection' ? 250 : 600,
      otherCost: t.type === 'Corrective' ? 150 : 0,
      documents:
        i % 3 === 0
          ? [
              { id: nextId++, name: `Service-Report-${iso}.pdf`, size: 482_000 },
              { id: nextId++, name: `Invoice-${iso}.pdf`, size: 126_000 },
            ]
          : [],
      health: i % 4 === 1 ? 'Fair' : 'Good',
      healthScore: i % 4 === 1 ? 68 : 85 - (i % 3) * 3,
      reminder: '',
    });
    date = addMonths(date, -(3 + ((i * 5 + c.id) % 5)));
  }
  return records;
}

@Component({
  selector: 'app-component-service-history',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './component-service-history.html',
  // Reuses the page's card, table, pill, menu and modal styles.
  styleUrls: ['../asset-components.css', './component-service-history.css'],
})
export class ComponentServiceHistory {
  readonly icons = {
    Bell, BellRing, CalendarDays, Check, ChevronLeft, ChevronRight, ClipboardCheck, Copy, EllipsisVertical, Eye,
    FileText, HeartPulse, History, Inbox, Paperclip, Plus, Search, Settings, SquarePen, Trash2, X,
  };

  readonly typeIcons: Record<ServiceType, typeof Zap> = { Preventive: Zap, Corrective: Wrench, Inspection: Search };
  readonly typeLabels: Record<ServiceType, string> = {
    Preventive: 'Preventive Maintenance',
    Corrective: 'Corrective Maintenance',
    Inspection: 'Inspection',
  };
  readonly serviceTypes: ServiceType[] = ['Preventive', 'Corrective', 'Inspection'];
  readonly statuses: ServiceStatus[] = ['Completed', 'In Progress', 'Scheduled', 'Cancelled'];
  readonly healthRatings: HealthRating[] = ['Good', 'Fair', 'Poor'];
  readonly vendors = VENDORS;
  readonly technicians = TECHNICIANS;
  readonly pageSizeOptions = [10, 25, 50];
  readonly detailTabs: { key: DetailTab; label: string }[] = [
    { key: 'details', label: 'Details' },
    { key: 'work', label: 'Work Performed' },
    { key: 'parts', label: 'Parts Used' },
    { key: 'cost', label: 'Cost' },
    { key: 'documents', label: 'Documents' },
  ];

  /** Where the asset sits; shown for records that have no service location of their own. */
  @Input() location = '';

  private comp!: AssetComponent;

  /** Sample log is created on first view and kept on the component, so it survives tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    c.serviceHistory ??= buildMockServiceHistory(c);
    this.comp = c;
    this.search = '';
    this.typeFilter = '';
    this.statusFilter = '';
    this.fromDate = '';
    this.toDate = '';
    this.page = 1;
    this.checked.clear();
    this.openMenu = null;
    this.selectedId = this.sorted[0]?.id ?? null;
    this.detailTab = 'details';
  }

  get component(): AssetComponent {
    return this.comp;
  }

  get records(): ServiceRecord[] {
    return this.comp.serviceHistory!;
  }

  search = '';
  typeFilter: '' | ServiceType = '';
  statusFilter: '' | ServiceStatus = '';
  fromDate = '';
  toDate = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  openMenu: number | null = null;

  selectedId: number | null = null;
  detailTab: DetailTab = 'details';

  // Inline "add part" / "add work item" rows
  newPart = { name: '', partNumber: '', quantity: 1, unitCost: 0 };
  newWorkItem = '';

  // Reminder editor
  reminderDraft = '';

  formOpen = false;
  editing: ServiceRecord | null = null;
  form: RecordForm = this.emptyForm();
  formSubmitted = false;

  deleteTarget: ServiceRecord | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  // ===== Helpers =====

  daysUntil(iso: string): number {
    return Math.round((fromIso(iso).getTime() - today().getTime()) / DAY_MS);
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  formatTime(hhmm: string): string {
    if (!hhmm) return '–';
    const [h, m] = hhmm.split(':').map(Number);
    return `${((h + 11) % 12) + 1}:${String(m).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
  }

  formatSize(bytes: number): string {
    return bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  // ===== Table =====

  get sorted(): ServiceRecord[] {
    return [...this.records].sort((a, b) => b.date.localeCompare(a.date) || b.id - a.id);
  }

  get dateRangeInvalid(): boolean {
    return !!this.fromDate && !!this.toDate && this.fromDate > this.toDate;
  }

  get filtered(): ServiceRecord[] {
    const q = this.search.trim().toLowerCase();
    return this.sorted.filter(
      (r) =>
        (!q || [r.description, r.reference, r.performedBy, r.technician, r.remarks].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || r.type === this.typeFilter) &&
        (!this.statusFilter || r.status === this.statusFilter) &&
        (this.dateRangeInvalid || ((!this.fromDate || r.date >= this.fromDate) && (!this.toDate || r.date <= this.toDate))),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): ServiceRecord[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filtered.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  clearDates(): void {
    this.fromDate = '';
    this.toDate = '';
    this.onFilterChange();
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((r) => this.checked.has(r.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((r) => (on ? this.checked.add(r.id) : this.checked.delete(r.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  exportChecked(): void {
    const rows = this.sorted.filter((r) => this.checked.has(r.id));
    const header = ['Reference', 'Service Date', 'Type', 'Description', 'Performed By', 'Technician', 'Next Due', 'Status', 'Total Cost'];
    const escape = (v: string) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);
    const lines = [header, ...rows.map((r) => [r.reference, r.date, r.type, r.description, r.performedBy, r.technician, r.nextDue, r.status, String(this.totalCost(r))])]
      .map((cols) => cols.map(escape).join(','));
    const url = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.comp.tag}-service-history.csv`;
    link.click();
    URL.revokeObjectURL(url);
    this.showToast(`Exported ${rows.length} record${rows.length === 1 ? '' : 's'}.`);
  }

  // ===== Selected record =====

  get selected(): ServiceRecord | null {
    return this.records.find((r) => r.id === this.selectedId) ?? null;
  }

  select(r: ServiceRecord): void {
    if (this.selectedId === r.id) return;
    this.selectedId = r.id;
    this.newPart = { name: '', partNumber: '', quantity: 1, unitCost: 0 };
    this.newWorkItem = '';
    this.reminderDraft = '';
  }

  partsTotal(r: ServiceRecord): number {
    return r.parts.reduce((sum, p) => sum + p.quantity * p.unitCost, 0);
  }

  totalCost(r: ServiceRecord): number {
    return this.partsTotal(r) + r.labourCost + r.otherCost;
  }

  tabCount(r: ServiceRecord, tab: DetailTab): number | null {
    if (tab === 'parts') return r.parts.length;
    if (tab === 'documents') return r.documents.length;
    if (tab === 'work') return r.workPerformed.length;
    return null;
  }

  addWorkItem(r: ServiceRecord): void {
    const item = this.newWorkItem.trim();
    if (!item) return;
    r.workPerformed.push(item);
    this.newWorkItem = '';
  }

  removeWorkItem(r: ServiceRecord, index: number): void {
    r.workPerformed.splice(index, 1);
  }

  get newPartValid(): boolean {
    const p = this.newPart;
    return !!p.name.trim() && p.quantity > 0 && p.unitCost >= 0;
  }

  addPart(r: ServiceRecord): void {
    if (!this.newPartValid) return;
    const p = this.newPart;
    r.parts.push({ id: nextId++, name: p.name.trim(), partNumber: p.partNumber.trim(), quantity: Number(p.quantity), unitCost: Number(p.unitCost) });
    this.newPart = { name: '', partNumber: '', quantity: 1, unitCost: 0 };
  }

  removePart(r: ServiceRecord, part: PartUsed): void {
    r.parts = r.parts.filter((p) => p !== part);
  }

  onDocumentsSelected(r: ServiceRecord, event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const tooBig = files.filter((f) => f.size > MAX_DOC_BYTES);
    files.filter((f) => f.size <= MAX_DOC_BYTES).forEach((f) => r.documents.push({ id: nextId++, name: f.name, size: f.size }));
    if (tooBig.length) this.showToast(`${tooBig.length} file${tooBig.length === 1 ? ' is' : 's are'} over 10 MB and ${tooBig.length === 1 ? 'was' : 'were'} skipped.`);
  }

  removeDocument(r: ServiceRecord, doc: ServiceDocument): void {
    r.documents = r.documents.filter((d) => d !== doc);
  }

  // ===== Reminder =====

  /** The date the reminder panel works with: an existing reminder, else the record's next due date. */
  reminderDate(r: ServiceRecord): string {
    return this.reminderDraft || r.reminder || r.nextDue;
  }

  saveReminder(r: ServiceRecord): void {
    const date = this.reminderDate(r);
    if (!date) {
      this.showToast('Pick a due date for the reminder first.');
      return;
    }
    if (this.daysUntil(date) < 0) {
      this.showToast('The reminder date is in the past.');
      return;
    }
    const isUpdate = !!r.reminder;
    r.reminder = date;
    this.reminderDraft = '';
    this.showToast(`Reminder ${isUpdate ? 'updated' : 'created'} for ${fromIso(date).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}.`);
  }

  clearReminder(r: ServiceRecord): void {
    r.reminder = '';
    this.reminderDraft = '';
    this.showToast('Reminder removed.');
  }

  // ===== Menus =====

  toggleMenu(id: number, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === id ? null : id;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.formOpen = false;
    this.deleteTarget = null;
  }

  // ===== Add / edit =====

  private emptyForm(): RecordForm {
    return {
      date: toIso(today()),
      nextDue: '',
      type: 'Preventive',
      description: '',
      performedBy: '',
      technician: '',
      location: this.location,
      startTime: '',
      endTime: '',
      runningHours: null,
      status: 'Completed',
      remarks: '',
      labourCost: 0,
      otherCost: 0,
      health: 'Good',
      healthScore: 85,
      workText: '',
    };
  }

  openForm(r?: ServiceRecord, duplicate = false): void {
    this.openMenu = null;
    this.editing = r && !duplicate ? r : null;
    if (r) {
      this.form = {
        date: r.date,
        nextDue: r.nextDue,
        type: r.type,
        description: r.description,
        performedBy: r.performedBy,
        technician: r.technician,
        location: r.location || this.location,
        startTime: r.startTime,
        endTime: r.endTime,
        runningHours: r.runningHours,
        status: r.status,
        remarks: r.remarks,
        labourCost: r.labourCost,
        otherCost: r.otherCost,
        health: r.health,
        healthScore: r.healthScore,
        workText: r.workPerformed.join('\n'),
      };
      if (duplicate) {
        this.form.date = toIso(today());
        this.form.status = 'Scheduled';
      }
    } else {
      this.form = this.emptyForm();
      this.form.runningHours = this.comp.lifeUnit === 'hrs' && this.comp.lifeUsed ? this.comp.lifeUsed : null;
    }
    this.formSubmitted = false;
    this.formOpen = true;
  }

  get timesInvalid(): boolean {
    return !!this.form.startTime && !!this.form.endTime && this.form.endTime <= this.form.startTime;
  }

  get nextDueInvalid(): boolean {
    return !!this.form.nextDue && !!this.form.date && this.form.nextDue <= this.form.date;
  }

  get scoreInvalid(): boolean {
    const s = Number(this.form.healthScore);
    return !Number.isFinite(s) || s < 0 || s > 100;
  }

  get costsInvalid(): boolean {
    return Number(this.form.labourCost) < 0 || Number(this.form.otherCost) < 0;
  }

  /** A record marked Completed can't be dated in the future. */
  get completedInFuture(): boolean {
    return this.form.status === 'Completed' && !!this.form.date && this.daysUntil(this.form.date) > 0;
  }

  saveForm(): void {
    this.formSubmitted = true;
    const f = this.form;
    if (
      !f.description.trim() ||
      !f.date ||
      !f.performedBy.trim() ||
      this.timesInvalid ||
      this.nextDueInvalid ||
      this.scoreInvalid ||
      this.costsInvalid ||
      this.completedInFuture
    ) {
      return;
    }
    const { workText, ...rest } = f;
    const values = {
      ...rest,
      description: f.description.trim(),
      performedBy: f.performedBy.trim(),
      technician: f.technician.trim(),
      location: f.location.trim(),
      remarks: f.remarks.trim(),
      runningHours: f.runningHours === null || (f.runningHours as unknown) === '' ? null : Number(f.runningHours),
      labourCost: Number(f.labourCost) || 0,
      otherCost: Number(f.otherCost) || 0,
      healthScore: Math.round(Number(f.healthScore)),
      workPerformed: workText.split('\n').map((l) => l.trim()).filter(Boolean),
    };

    if (this.editing) {
      Object.assign(this.editing, values);
      this.showToast(`${this.editing.reference} updated.`);
    } else {
      const created: ServiceRecord = { id: nextId++, reference: this.nextReference(values.date), parts: [], documents: [], reminder: '', ...values };
      this.records.push(created);
      this.selectedId = created.id;
      this.page = 1;
      this.showToast(`${created.reference} added.`);
    }
    this.formOpen = false;
  }

  /** SRV-<year>-<n>, one higher than the largest number already used in that year. */
  private nextReference(dateIso: string): string {
    const year = dateIso.slice(0, 4);
    const used = this.records
      .filter((r) => r.reference.startsWith(`SRV-${year}-`))
      .map((r) => Number(r.reference.split('-')[2]) || 0);
    return `SRV-${year}-${String(Math.max(0, ...used) + 1).padStart(4, '0')}`;
  }

  // ===== Delete =====

  askDelete(r: ServiceRecord): void {
    this.openMenu = null;
    this.deleteTarget = r;
  }

  confirmDelete(): void {
    const r = this.deleteTarget;
    if (!r) return;
    this.comp.serviceHistory = this.records.filter((x) => x !== r);
    this.checked.delete(r.id);
    if (this.selectedId === r.id) this.selectedId = this.sorted[0]?.id ?? null;
    this.deleteTarget = null;
    this.showToast(`${r.reference} deleted.`);
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
