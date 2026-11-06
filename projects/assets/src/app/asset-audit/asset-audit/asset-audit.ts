import { Component, HostListener, OnDestroy, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  CircleX,
  ClipboardList,
  Clock,
  EllipsisVertical,
  Funnel,
  Plus,
  Search,
  Upload,
  X,
} from 'lucide-angular';
/**
 * DEMO MODE: asset audit sample data (DEMO MODE) until the Asset Audit API returns audit type, location, status, auditor and findings.
 * The first ten rows match the design; the rest fill out the list so filters and paging have something to work with.
 */

export type AuditType = 'Periodic' | 'Ad-hoc' | 'Compliance' | 'Annual' | 'Spot Check' | 'Preventive';
export type AuditStatus = 'Draft' | 'Scheduled' | 'In Progress' | 'Completed' | 'Overdue';

export interface AssetAuditRecord {
  id: number;
  /** "AUD-YYYY-NNN" */
  reference: string;
  name: string;
  type: AuditType;
  location: string;
  /** "YYYY-MM-DD" */
  scheduled: string;
  /** "YYYY-MM-DD"; empty until the audit is completed. */
  completed: string;
  status: AuditStatus;
  auditor: string;
  /** Discrepancies found; null until the audit has findings recorded. */
  findings: number | null;
  notes: string;
  /** Set by the New Audit page. */
  assetCount?: number;
  dueDate?: string;
}

export const AUDIT_TYPES: AuditType[] = ['Periodic', 'Ad-hoc', 'Compliance', 'Annual', 'Spot Check', 'Preventive'];
const AUDIT_STATUSES: AuditStatus[] = ['Draft', 'Scheduled', 'In Progress', 'Completed', 'Overdue'];
export const AUDIT_LOCATIONS = ['Main Plant - Block A', 'Main Plant - Block B', 'Head Office', 'Data Centre', 'Warehouse', 'Field Operations', 'Transport Yard', 'All Locations'];
export const AUDITORS = ['Salim Ahmed', 'Nisha Patel', 'Ahmed Khan', 'Ramesh Kumar', 'Priya Nair'];

type Row = [string, AuditType, string, string, string, AuditStatus, string, number | null];

const ROWS: Row[] = [
  ['Q4 Asset Audit - Main Plant', 'Periodic', 'Main Plant - Block A', '2025-12-15', '', 'Scheduled', 'Salim Ahmed', null],
  ['IT Equipment Verification', 'Ad-hoc', 'Head Office', '2025-10-10', '2025-10-12', 'Completed', 'Nisha Patel', 2],
  ['Critical Assets Audit', 'Compliance', 'Data Centre', '2025-10-01', '2025-10-03', 'Completed', 'Ahmed Khan', 1],
  ['Warehouse Asset Count', 'Periodic', 'Warehouse', '2025-09-20', '2025-09-21', 'Completed', 'Ramesh Kumar', 4],
  ['Mobile Assets Audit', 'Periodic', 'Field Operations', '2025-09-10', '2025-09-12', 'In Progress', 'Priya Nair', 1],
  ['Annual Asset Verification', 'Annual', 'All Locations', '2025-08-01', '2025-08-05', 'Completed', 'Salim Ahmed', 3],
  ['Safety Equipment Audit', 'Compliance', 'Main Plant - Block B', '2025-07-15', '2025-07-16', 'Completed', 'Nisha Patel', 0],
  ['Office Assets Spot Check', 'Spot Check', 'Head Office', '2025-06-10', '2025-06-10', 'Completed', 'Ahmed Khan', 1],
  ['Vehicle Assets Audit', 'Periodic', 'Transport Yard', '2025-06-05', '', 'Overdue', 'Ramesh Kumar', null],
  ['HVAC Assets Audit', 'Preventive', 'Main Plant - Block A', '2025-05-20', '2025-05-22', 'Completed', 'Priya Nair', 2],
  ['Server Room Verification', 'Compliance', 'Data Centre', '2025-05-08', '2025-05-09', 'Completed', 'Ahmed Khan', 0],
  ['Spare Parts Stock Check', 'Spot Check', 'Warehouse', '2025-04-25', '2025-04-25', 'Completed', 'Ramesh Kumar', 2],
  ['Fire Safety Equipment Audit', 'Compliance', 'Main Plant - Block B', '2025-04-14', '2025-04-15', 'Completed', 'Nisha Patel', 1],
  ['Laptop Fleet Audit', 'Periodic', 'Head Office', '2025-04-02', '2025-04-04', 'Completed', 'Salim Ahmed', 0],
  ['Field Tools Verification', 'Ad-hoc', 'Field Operations', '2025-03-20', '', 'In Progress', 'Priya Nair', null],
  ['Forklift Inspection Audit', 'Preventive', 'Warehouse', '2025-03-11', '2025-03-12', 'Completed', 'Ramesh Kumar', 1],
  ['Network Equipment Audit', 'Periodic', 'Data Centre', '2025-03-03', '2025-03-05', 'Completed', 'Ahmed Khan', 0],
  ['Q1 Asset Audit - Block B', 'Periodic', 'Main Plant - Block B', '2025-02-24', '2025-02-27', 'Completed', 'Salim Ahmed', 3],
  ['Furniture Count', 'Spot Check', 'Head Office', '2025-02-12', '2025-02-12', 'Completed', 'Nisha Patel', 0],
  ['Fleet Tyre Audit', 'Preventive', 'Transport Yard', '2025-02-03', '', 'In Progress', 'Ramesh Kumar', null],
  ['Generator Compliance Audit', 'Compliance', 'Main Plant - Block A', '2025-01-28', '2025-01-29', 'Completed', 'Priya Nair', 1],
  ['CCTV Assets Audit', 'Ad-hoc', 'All Locations', '2025-01-20', '2025-01-22', 'Completed', 'Ahmed Khan', 0],
  ['Calibration Equipment Audit', 'Compliance', 'Main Plant - Block B', '2025-12-28', '', 'Scheduled', 'Nisha Patel', null],
  ['Year-start Asset Verification', 'Annual', 'All Locations', '2025-01-06', '2025-01-10', 'Completed', 'Salim Ahmed', 2],
];

export const AUDITS: AssetAuditRecord[] = ROWS.map(([name, type, location, scheduled, completed, status, auditor, findings], i) => ({
  id: i + 1,
  reference: `AUD-2025-${String(ROWS.length - i).padStart(3, '0')}`,
  name, type, location, scheduled, completed, status, auditor, findings, notes: '',
}));

/** Next reference number for a new audit, e.g. "AUD-2025-025". */
export function nextReference(audits: AssetAuditRecord[], year = new Date().getFullYear()): string {
  const max = audits.reduce((n, a) => Math.max(n, Number(a.reference.split('-')[2]) || 0), 0);
  return `AUD-${year}-${String(max + 1).padStart(3, '0')}`;
}

export function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

type FindingsFilter = '' | 'with' | 'none' | 'pending';

interface AuditForm {
  /** Audit being edited; null for a new audit. */
  id: number | null;
  name: string;
  type: AuditType;
  location: string;
  scheduled: string;
  auditor: string;
  notes: string;
}

interface CompleteForm {
  id: number;
  completed: string;
  findings: number;
  notes: string;
}

@Component({
  selector: 'app-asset-audit',
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './asset-audit.html',
  styleUrl: './asset-audit.css',
})
export class AssetAudit implements OnDestroy {
  readonly icons = {
    CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, CircleX, ClipboardList, Clock, EllipsisVertical, Funnel, Plus, Search, Upload, X,
  };
  readonly types = AUDIT_TYPES;
  readonly statuses = AUDIT_STATUSES;
  readonly locations = AUDIT_LOCATIONS;
  readonly auditors = AUDITORS;
  readonly pageSizes = [10, 25, 50];

  /** DEMO MODE: shared in-memory list until the Asset Audit API carries these fields, so audits made on the New Audit page show here. */
  readonly audits: AssetAuditRecord[] = AUDITS;

  // Filters
  search = '';
  typeFilter: AuditType | '' = '';
  statusFilter: AuditStatus | '' = '';
  locationFilter = '';
  fromDate = '2025-01-01';
  toDate = '2025-12-31';
  moreFilters = false;
  auditorFilter = '';
  findingsFilter: FindingsFilter = '';
  pageSize = 10;
  page = 0;

  checked = new Set<number>();
  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: AssetAuditRecord; top: number; right: number } | null = null;

  auditForm: AuditForm | null = null;
  completeForm: CompleteForm | null = null;
  detail: AssetAuditRecord | null = null;
  deleteTarget: AssetAuditRecord | null = null;
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    // Message handed over by the New Audit page after saving
    const message = this.router.getCurrentNavigation()?.extras.state?.['toast'];
    if (typeof message === 'string') this.showToast(message);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Summary =====

  countOf(status: AuditStatus): number {
    return this.audits.filter((a) => a.status === status).length;
  }

  /** Summary cards double as a quick status filter. */
  filterStatus(status: AuditStatus | ''): void {
    this.statusFilter = this.statusFilter === status ? '' : status;
    this.onFilter();
  }

  // ===== Filters & paging =====

  get filtered(): AssetAuditRecord[] {
    const q = this.search.trim().toLowerCase();
    return this.audits.filter(
      (a) =>
        (!this.typeFilter || a.type === this.typeFilter) &&
        (!this.statusFilter || a.status === this.statusFilter) &&
        (!this.locationFilter || a.location === this.locationFilter) &&
        (!this.fromDate || a.scheduled >= this.fromDate) &&
        (!this.toDate || a.scheduled <= this.toDate) &&
        (!this.auditorFilter || a.auditor === this.auditorFilter) &&
        this.matchesFindings(a) &&
        (!q || `${a.reference} ${a.name} ${a.location} ${a.auditor}`.toLowerCase().includes(q)),
    );
  }

  private matchesFindings(a: AssetAuditRecord): boolean {
    switch (this.findingsFilter) {
      case 'with': return (a.findings ?? 0) > 0;
      case 'none': return a.findings === 0;
      case 'pending': return a.findings === null;
      default: return true;
    }
  }

  get pageRows(): AssetAuditRecord[] {
    return this.filtered.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showingLabel(): string {
    const n = this.filtered.length;
    if (!n) return 'No records';
    return `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records`;
  }

  rowNo(i: number): number {
    return this.page * this.pageSize + i + 1;
  }

  onFilter(): void {
    this.page = 0;
  }

  resetFilters(): void {
    this.search = this.typeFilter = this.statusFilter = this.locationFilter = this.auditorFilter = this.findingsFilter = '';
    this.fromDate = this.toDate = '';
    this.onFilter();
  }

  // ===== Selection =====

  get allPageChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((a) => this.checked.has(a.id));
  }

  togglePage(on: boolean): void {
    for (const a of this.pageRows) {
      if (on) this.checked.add(a.id);
      else this.checked.delete(a.id);
    }
  }

  toggleCheck(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  // ===== Display helpers =====

  findingsLabel(a: AssetAuditRecord): string {
    if (a.findings === null) return '-';
    if (a.findings === 0) return '0';
    return `${a.findings} ${a.findings === 1 ? 'Discrepancy' : 'Discrepancies'}`;
  }

  statusClass(status: AuditStatus): string {
    return 'status--' + status.toLowerCase().replace(' ', '-');
  }

  // ===== New / edit =====

  openNew(): void {
    this.closeMenus();
    this.router.navigate(['/assets/audit/new-audit']);
  }

  openEdit(a: AssetAuditRecord): void {
    this.closeMenus();
    this.formError = '';
    this.auditForm = { id: a.id, name: a.name, type: a.type, location: a.location, scheduled: a.scheduled, auditor: a.auditor, notes: a.notes };
  }

  saveAudit(): void {
    const f = this.auditForm;
    if (!f) return;
    if (!f.name.trim()) return void (this.formError = 'Enter the audit name.');
    if (!f.location) return void (this.formError = 'Choose the location / site.');
    if (!f.scheduled) return void (this.formError = 'Choose the scheduled date.');
    if (!f.auditor) return void (this.formError = 'Choose who will audit.');

    const fields = { name: f.name.trim(), type: f.type, location: f.location, scheduled: f.scheduled, auditor: f.auditor, notes: f.notes.trim() };
    const existing = this.audits.find((a) => a.id === f.id);
    if (existing) {
      Object.assign(existing, fields);
      this.showToast(`${existing.reference} updated.`);
    } else {
      const reference = nextReference(this.audits);
      const id = this.audits.reduce((n, a) => Math.max(n, a.id), 0) + 1;
      this.audits.unshift({ id, reference, ...fields, completed: '', status: 'Scheduled', findings: null });
      this.page = 0;
      this.showToast(`${reference} scheduled.`);
    }
    this.auditForm = null;
  }

  // ===== Status changes =====

  startAudit(a: AssetAuditRecord): void {
    this.closeMenus();
    a.status = 'In Progress';
    this.showToast(`${a.reference} started.`);
  }

  openComplete(a: AssetAuditRecord): void {
    this.closeMenus();
    this.formError = '';
    this.completeForm = { id: a.id, completed: isoDate(new Date()), findings: a.findings ?? 0, notes: a.notes };
  }

  saveComplete(): void {
    const f = this.completeForm;
    const a = this.audits.find((x) => x.id === f?.id);
    if (!f || !a) return;
    if (!f.completed) return void (this.formError = 'Choose the completed date.');
    if (f.findings == null || f.findings < 0) return void (this.formError = 'Enter the number of discrepancies (0 if none).');
    Object.assign(a, { status: 'Completed', completed: f.completed, findings: Math.floor(f.findings), notes: f.notes.trim() });
    this.completeForm = null;
    this.showToast(`${a.reference} completed.`);
  }

  // ===== Details / delete =====

  openDetail(a: AssetAuditRecord): void {
    this.closeMenus();
    this.detail = a;
  }

  openDelete(a: AssetAuditRecord): void {
    this.closeMenus();
    this.deleteTarget = a;
  }

  confirmDelete(): void {
    const a = this.deleteTarget;
    if (!a) return;
    this.audits.splice(this.audits.indexOf(a), 1);
    this.checked.delete(a.id);
    this.deleteTarget = null;
    if (this.page >= this.pageCount) this.page = this.pageCount - 1;
    this.showToast(`${a.reference} deleted.`);
  }

  // ===== Export =====

  export(format: 'xlsx' | 'csv'): void {
    const source = this.checked.size ? this.filtered.filter((a) => this.checked.has(a.id)) : this.filtered;
    const rows = source.map((a, i) => ({
      '#': i + 1, 'Audit Reference': a.reference, 'Audit Name': a.name, 'Audit Type': a.type, 'Location / Site': a.location,
      'Scheduled Date': a.scheduled, 'Completed Date': a.completed, Status: a.status, 'Audited By': a.auditor, Findings: a.findings ?? '', Notes: a.notes,
    }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Asset Audits');
    XLSX.writeFile(book, `Asset_Audits.${format}`, { bookType: format });
    this.menuOpen = '';
  }

  // ===== Menus =====

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.rowMenu = null;
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  openRowMenu(row: AssetAuditRecord, event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen = '';
    if (this.rowMenu?.row === row) return void (this.rowMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 220 > window.innerHeight ? r.top - 216 : r.bottom + 6;
    this.rowMenu = { row, top, right: window.innerWidth - r.right };
  }

  @HostListener('document:click')
  @HostListener('window:scroll')
  @HostListener('window:resize')
  @HostListener('document:wheel')
  closeMenus(): void {
    this.menuOpen = '';
    this.rowMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.auditForm = this.completeForm = this.detail = this.deleteTarget = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
