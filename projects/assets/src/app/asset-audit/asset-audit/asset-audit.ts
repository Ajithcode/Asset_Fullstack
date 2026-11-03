import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { Subscription } from 'rxjs';
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
import { AssetAuditService, AuditCounts, AuditListItemDto, AuditLocation, apiDate } from '../asset-audit.service';
import { apiErrorMessage } from '../../services/asset-api';

export type AuditType = 'Periodic' | 'Ad-hoc' | 'Compliance' | 'Annual' | 'Spot Check' | 'Preventive';
export type AuditStatus = 'Draft' | 'Scheduled' | 'In Progress' | 'Completed' | 'Overdue';

export interface AssetAuditRecord {
  /** API id of the audit. */
  id: string;
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
  assetCount?: number;
  dueDate?: string;
}

export const AUDIT_TYPES: AuditType[] = ['Periodic', 'Ad-hoc', 'Compliance', 'Annual', 'Spot Check', 'Preventive'];
const AUDIT_STATUSES: AuditStatus[] = ['Draft', 'Scheduled', 'In Progress', 'Completed', 'Overdue'];

export function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function toRecord(dto: AuditListItemDto): AssetAuditRecord {
  return {
    id: dto.id,
    reference: dto.auditReference,
    name: dto.auditName,
    type: (dto.auditType ?? '') as AuditType,
    location: dto.location ?? '',
    scheduled: apiDate(dto.scheduledDate),
    completed: apiDate(dto.completedDate),
    status: (dto.status ?? 'Draft') as AuditStatus,
    auditor: dto.auditedBy ?? '',
    findings: dto.discrepancyCount,
    notes: '',
    assetCount: dto.assetCount,
    dueDate: apiDate(dto.endDate),
  };
}

type FindingsFilter = '' | 'with' | 'none' | 'pending';

interface CompleteForm {
  id: string;
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
export class AssetAudit implements OnInit, OnDestroy {
  readonly icons = {
    CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, CircleX, ClipboardList, Clock, EllipsisVertical, Funnel, Plus, Search, Upload, X,
  };
  readonly types = AUDIT_TYPES;
  readonly statuses = AUDIT_STATUSES;
  /** Location / site filter options (id and name). */
  locations: AuditLocation[] = [];
  readonly pageSizes = [10, 25, 50];

  /** Audits matching the server-side filters (search, type, status, location, dates). */
  audits: AssetAuditRecord[] = [];
  counts: AuditCounts = { total: 0, completed: 0, inProgress: 0, scheduled: 0, overdue: 0, draft: 0 };
  loading = false;
  loadError = '';
  busy = false;

  // Filters
  search = '';
  typeFilter: AuditType | '' = '';
  statusFilter: AuditStatus | '' = '';
  locationFilter = '';
  fromDate = '';
  toDate = '';
  moreFilters = false;
  auditorFilter = '';
  findingsFilter: FindingsFilter = '';
  pageSize = 10;
  page = 0;

  checked = new Set<string>();
  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: AssetAuditRecord; top: number; right: number } | null = null;

  completeForm: CompleteForm | null = null;
  detail: AssetAuditRecord | null = null;
  deleteTarget: AssetAuditRecord | null = null;
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private loadSub?: Subscription;
  private readonly router = inject(Router);
  private readonly api = inject(AssetAuditService);

  constructor() {
    // Message handed over by the New Audit page after saving
    const message = this.router.getCurrentNavigation()?.extras.state?.['toast'];
    if (typeof message === 'string') this.showToast(message);
  }

  ngOnInit(): void {
    this.api.locations().subscribe((list) => (this.locations = list));
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    clearTimeout(this.searchTimer);
    this.loadSub?.unsubscribe();
  }

  // ===== Loading =====

  private get query() {
    return {
      search: this.search.trim(),
      auditType: this.typeFilter,
      status: this.statusFilter,
      locationId: this.locationFilter,
      fromDate: this.fromDate,
      toDate: this.toDate,
    };
  }

  load(): void {
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadError = '';
    this.loadSub = this.api.list(this.query).subscribe({
      next: ({ items, counts }) => {
        this.audits = items.map(toRecord);
        this.counts = counts;
        const ids = new Set(this.audits.map((a) => a.id));
        for (const id of [...this.checked]) if (!ids.has(id)) this.checked.delete(id);
        if (this.page >= this.pageCount) this.page = this.pageCount - 1;
        this.loading = false;
      },
      error: (err) => {
        this.audits = [];
        this.loadError = apiErrorMessage(err, 'Audits could not be loaded.');
        this.loading = false;
      },
    });
  }

  get auditors(): string[] {
    return [...new Set(this.audits.map((a) => a.auditor).filter(Boolean))].sort((x, y) => x.localeCompare(y));
  }

  // ===== Summary =====

  countOf(status: AuditStatus): number {
    switch (status) {
      case 'Completed': return this.counts.completed;
      case 'In Progress': return this.counts.inProgress;
      case 'Scheduled': return this.counts.scheduled;
      case 'Overdue': return this.counts.overdue;
      default: return this.counts.draft;
    }
  }

  /** Summary cards double as a quick status filter. */
  filterStatus(status: AuditStatus | ''): void {
    this.statusFilter = this.statusFilter === status ? '' : status;
    this.onFilter();
  }

  // ===== Filters & paging =====

  /** Audited By and Findings are filtered here; the rest by the API. */
  get filtered(): AssetAuditRecord[] {
    return this.audits.filter((a) => (!this.auditorFilter || a.auditor === this.auditorFilter) && this.matchesFindings(a));
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
    if (this.loading) return 'Loading...';
    const n = this.filtered.length;
    if (!n) return 'No records';
    return `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records`;
  }

  rowNo(i: number): number {
    return this.page * this.pageSize + i + 1;
  }

  /** Server-side filters reload the list; the search waits for typing to pause. */
  onFilter(): void {
    this.page = 0;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 300);
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

  toggleCheck(id: string): void {
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

  /** Draft and Scheduled audits can still be changed in the wizard. */
  canEdit(a: AssetAuditRecord): boolean {
    return a.status === 'Draft' || a.status === 'Scheduled' || a.status === 'Overdue';
  }

  // ===== New / edit =====

  openNew(): void {
    this.closeMenus();
    this.router.navigate(['/assets/audit/new-audit']);
  }

  /** Opens the audit in the New Audit wizard. */
  openEdit(a: AssetAuditRecord): void {
    this.closeMenus();
    this.detail = null;
    this.router.navigate(['/assets/audit/new-audit'], { queryParams: { id: a.id } });
  }

  // ===== Status changes =====

  startAudit(a: AssetAuditRecord): void {
    this.closeMenus();
    this.api.start(a.id).subscribe({
      next: () => {
        this.showToast(`${a.reference} started.`);
        this.load();
      },
      error: (err) => this.showToast(apiErrorMessage(err, `${a.reference} could not be started.`)),
    });
  }

  openComplete(a: AssetAuditRecord): void {
    this.closeMenus();
    this.formError = '';
    this.completeForm = { id: a.id, completed: a.completed || isoDate(new Date()), findings: a.findings ?? 0, notes: a.notes };
  }

  saveComplete(): void {
    const f = this.completeForm;
    const a = this.audits.find((x) => x.id === f?.id);
    if (!f || !a || this.busy) return;
    if (!f.completed) return void (this.formError = 'Choose the completed date.');
    if (f.findings == null || f.findings < 0) return void (this.formError = 'Enter the number of discrepancies (0 if none).');
    this.busy = true;
    this.api.complete(a.id, Math.floor(f.findings), f.notes.trim()).subscribe({
      next: () => {
        this.busy = false;
        this.completeForm = null;
        this.showToast(`${a.reference} completed.`);
        this.load();
      },
      error: (err) => {
        this.busy = false;
        this.formError = apiErrorMessage(err, `${a.reference} could not be completed.`);
      },
    });
  }

  // ===== Details / delete =====

  openDetail(a: AssetAuditRecord): void {
    this.closeMenus();
    this.detail = a;
    // Notes are only on the full audit
    this.api.get(a.id).subscribe({
      next: (dto) => (a.notes = dto.completionRemarks || dto.notes || dto.description || ''),
      error: () => undefined,
    });
  }

  openDelete(a: AssetAuditRecord): void {
    this.closeMenus();
    this.deleteTarget = a;
  }

  confirmDelete(): void {
    const a = this.deleteTarget;
    if (!a || this.busy) return;
    this.busy = true;
    this.api.delete(a.id).subscribe({
      next: () => {
        this.busy = false;
        this.checked.delete(a.id);
        this.deleteTarget = null;
        this.showToast(`${a.reference} deleted.`);
        this.load();
      },
      error: (err) => {
        this.busy = false;
        this.deleteTarget = null;
        this.showToast(apiErrorMessage(err, `${a.reference} could not be deleted.`));
      },
    });
  }

  // ===== Export =====

  /** Excel of the whole filtered list comes from the API; CSV and ticked rows are built here from the loaded data. */
  export(format: 'xlsx' | 'csv'): void {
    this.menuOpen = '';
    if (format === 'xlsx' && !this.checked.size && !this.auditorFilter && !this.findingsFilter) {
      this.api.export(this.query).subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = 'Asset_Audits.xlsx';
          link.click();
          URL.revokeObjectURL(url);
        },
        error: (err) => this.showToast(apiErrorMessage(err, 'Export failed.')),
      });
      return;
    }
    const source = this.checked.size ? this.filtered.filter((a) => this.checked.has(a.id)) : this.filtered;
    const rows = source.map((a, i) => ({
      '#': i + 1, 'Audit Reference': a.reference, 'Audit Name': a.name, 'Audit Type': a.type, 'Location / Site': a.location,
      'Scheduled Date': a.scheduled, 'Completed Date': a.completed, Status: a.status, 'Audited By': a.auditor, Findings: a.findings ?? '', Notes: a.notes,
    }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Asset Audits');
    XLSX.writeFile(book, `Asset_Audits.${format}`, { bookType: format });
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
    this.completeForm = this.detail = this.deleteTarget = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
