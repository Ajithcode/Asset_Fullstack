import { Component, HostListener, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowUp,
  Building2,
  CalendarPlus,
  Cctv,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleStop,
  Columns3,
  CreditCard,
  Download,
  EllipsisVertical,
  Forklift,
  Laptop,
  Layers,
  MapPin,
  Monitor,
  Plus,
  Projector,
  Repeat,
  RotateCcw,
  ScanBarcode,
  Search,
  SlidersHorizontal,
  Smartphone,
  Tablet,
  UserRound,
  Wrench,
  X,
} from 'lucide-angular';
import {
  ASSIGNMENTS, Assignment, AssignmentStatus, AssignmentType, AssetKind, CURRENT_ROLE, CURRENT_USER, DEPARTMENTS, LOCATIONS, TARGET_LABELS, assigneeChoices, assigneeDetails, isoDate, stamp,
} from './asset-assignment-data';

type ColumnKey = 'tag' | 'type' | 'assignee' | 'from' | 'to' | 'status';
type DetailTab = 'assignment' | 'history' | 'related';

interface AssignForm {
  mode: 'reassign' | 'bulk';
  /** Assignments being reassigned (reassign / bulk). */
  ids: number[];
  assetName: string;
  assetType: string;
  tag: string;
  serial: string;
  type: AssignmentType;
  assignee: string;
  from: string;
  to: string;
  remarks: string;
}

@Component({
  selector: 'app-asset-assignment',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-assignment.html',
  styleUrls: ['./asset-assignment.css', './asset-assignment-table.css'],
})
export class AssetAssignment implements OnDestroy {
  readonly icons = {
    ArrowUp, Building2, CalendarPlus, ChevronDown, ChevronLeft, ChevronRight, CircleStop, Columns3, Download, EllipsisVertical, Layers, MapPin, Plus,
    Repeat, RotateCcw, Search, SlidersHorizontal, UserRound, X,
  };
  readonly kindIcons: Record<AssetKind, typeof Laptop> = {
    laptop: Laptop, phone: Smartphone, scanner: ScanBarcode, projector: Projector, forklift: Forklift, tool: Wrench, card: CreditCard, monitor: Monitor, tablet: Tablet, camera: Cctv,
  };
  readonly assignmentTypes: AssignmentType[] = ['Employee', 'Department', 'Location', 'Other'];
  readonly targetLabels = TARGET_LABELS;
  readonly assigneeChoices = assigneeChoices;
  readonly statuses: AssignmentStatus[] = ['Assigned', 'Overdue', 'Returned'];
  readonly departments = DEPARTMENTS;
  readonly locations = LOCATIONS;
  readonly pageSizes = [10, 25, 50];
  readonly columns: { key: ColumnKey; label: string }[] = [
    { key: 'tag', label: 'Asset Tag / Serial' },
    { key: 'type', label: 'Assignment Type' },
    { key: 'assignee', label: 'Assigned To' },
    { key: 'from', label: 'From Date' },
    { key: 'to', label: 'To Date' },
    { key: 'status', label: 'Status' },
  ];
  /** DEMO MODE: month-on-month change shown on the summary cards until the API returns it. */
  readonly trends = { total: 6, Employee: 4, Department: 8, Location: 12 };

  /** DEMO MODE: shared in-memory store, so assignments made on the Assign Asset page show in the list. */
  readonly assignments: Assignment[] = ASSIGNMENTS;

  // Filters
  search = '';
  typeFilter: AssignmentType | '' = '';
  assigneeFilter = '';
  departmentFilter = '';
  locationFilter = '';
  statusFilter: AssignmentStatus | '' = '';
  moreFilters = false;
  fromDate = '';
  toDate = '';
  pageSize = 10;
  page = 0;

  hidden = new Set<ColumnKey>();
  checked = new Set<number>();
  selectedId: number | null = ASSIGNMENTS[0]?.id ?? null;
  tab: DetailTab = 'assignment';
  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: Assignment; top: number; right: number } | null = null;

  assignForm: AssignForm | null = null;
  formError = '';
  extendTarget: { id: number; to: string } | null = null;
  endTarget: Assignment | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    // Message handed over by the Assign Asset page after saving
    const message = this.router.getCurrentNavigation()?.extras.state?.['toast'];
    if (typeof message === 'string') this.showToast(message);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Summary =====

  get active(): Assignment[] {
    return this.assignments.filter((a) => a.status !== 'Returned');
  }

  countOf(type: AssignmentType): number {
    return this.active.filter((a) => a.type === type).length;
  }

  // ===== Filters & paging =====

  /** "Assignee Type" narrows by the kind of assignee: employees by job title, others by department / site. */
  get assigneeOptions(): string[] {
    return [...new Set(this.assignments.filter((a) => !this.typeFilter || a.type === this.typeFilter).map((a) => a.assigneeSub))].sort();
  }

  get filtered(): Assignment[] {
    const q = this.search.trim().toLowerCase();
    return this.assignments.filter(
      (a) =>
        (!this.typeFilter || a.type === this.typeFilter) &&
        (!this.assigneeFilter || a.assigneeSub === this.assigneeFilter) &&
        (!this.departmentFilter || a.department === this.departmentFilter) &&
        (!this.locationFilter || a.location === this.locationFilter) &&
        (!this.statusFilter || a.status === this.statusFilter) &&
        (!this.fromDate || a.from >= this.fromDate) &&
        (!this.toDate || a.from <= this.toDate) &&
        (!q || `${a.assetName} ${a.assetType} ${a.tag} ${a.serial} ${a.assignee}`.toLowerCase().includes(q)),
    );
  }

  get pageRows(): Assignment[] {
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
    return `Showing ${this.page * this.pageSize + 1} - ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n.toLocaleString()} records`;
  }

  rowNo(i: number): number {
    return this.page * this.pageSize + i + 1;
  }

  onFilter(): void {
    this.page = 0;
  }

  onTypeFilter(): void {
    if (this.assigneeFilter && !this.assigneeOptions.includes(this.assigneeFilter)) this.assigneeFilter = '';
    this.onFilter();
  }

  resetFilters(): void {
    this.search = this.typeFilter = this.assigneeFilter = this.departmentFilter = this.locationFilter = this.statusFilter = this.fromDate = this.toDate = '';
    this.moreFilters = false;
    this.onFilter();
  }

  get visibleColumnCount(): number {
    return 4 + this.columns.length - this.hidden.size;
  }

  show(key: ColumnKey): boolean {
    return !this.hidden.has(key);
  }

  toggleColumn(key: ColumnKey): void {
    if (this.hidden.has(key)) this.hidden.delete(key);
    else this.hidden.add(key);
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

  get selected(): Assignment | undefined {
    return this.assignments.find((a) => a.id === this.selectedId);
  }

  select(a: Assignment): void {
    this.selectedId = a.id;
    this.tab = 'assignment';
  }

  /** Other assignments of the same asset or to the same assignee. */
  get related(): Assignment[] {
    const s = this.selected;
    if (!s) return [];
    return this.assignments.filter((a) => a.id !== s.id && (a.tag === s.tag || a.assignee === s.assignee)).slice(0, 8);
  }

  // ===== Display helpers =====

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
  }

  /** Stable avatar colour per name. */
  avatarHue(name: string): number {
    let h = 0;
    for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) % 360;
    return h;
  }

  // ===== Assign / reassign / bulk =====

  /** Opens the Assign Asset page, optionally for one kind of assignee. */
  goAssign(type?: AssignmentType): void {
    this.closeMenus();
    this.router.navigate(['/assets/assignment/assign-asset'], { queryParams: { type: type ?? null } });
  }

  openReassign(a: Assignment): void {
    this.closeMenus();
    this.formError = '';
    this.assignForm = { mode: 'reassign', ids: [a.id], assetName: a.assetName, assetType: a.assetType, tag: a.tag, serial: a.serial, type: a.type, assignee: '', from: isoDate(new Date()), to: '', remarks: '' };
  }

  openBulk(): void {
    this.menuOpen = '';
    this.formError = '';
    const ids = [...this.checked].filter((id) => this.assignments.some((a) => a.id === id && a.status !== 'Returned'));
    if (!ids.length) return this.showToast('Tick the assignments to move in the table first.');
    this.assignForm = { mode: 'bulk', ids, assetName: '', assetType: '', tag: '', serial: '', type: 'Employee', assignee: '', from: isoDate(new Date()), to: '', remarks: '' };
  }

  onFormType(): void {
    if (this.assignForm) this.assignForm.assignee = '';
  }

  saveAssign(): void {
    const f = this.assignForm;
    if (!f) return;
    if (!f.assignee) return void (this.formError = `Choose the ${f.type.toLowerCase()} to assign to.`);
    if (!f.from) return void (this.formError = 'Choose the from date.');
    if (f.to && f.to < f.from) return void (this.formError = 'The to date must be on or after the from date.');

    const target = assigneeDetails(f.type, f.assignee);
    const now = stamp(new Date());
    for (const a of this.assignments.filter((x) => f.ids.includes(x.id))) {
      a.history.push({ at: now, action: 'Reassigned', by: CURRENT_USER, note: `From ${a.assignee} to ${target.assignee}` });
      Object.assign(a, { type: f.type, ...target, from: f.from, to: f.to, status: 'Assigned', assignedBy: CURRENT_USER, assignedByRole: CURRENT_ROLE, assignedOn: now });
      if (f.remarks.trim()) a.remarks = f.remarks.trim();
    }
    this.checked.clear();
    this.showToast(f.ids.length === 1 ? `Reassigned to ${target.assignee}.` : `${f.ids.length} assets reassigned to ${target.assignee}.`);
    this.assignForm = null;
    this.page = 0;
  }

  // ===== Extend / end =====

  openExtend(a: Assignment): void {
    this.closeMenus();
    this.formError = '';
    this.extendTarget = { id: a.id, to: a.to };
  }

  saveExtend(): void {
    const t = this.extendTarget;
    const a = this.assignments.find((x) => x.id === t?.id);
    if (!t || !a) return;
    if (!t.to) return void (this.formError = 'Choose the new to date.');
    if (t.to < a.from) return void (this.formError = 'The to date must be on or after the from date.');
    a.history.push({ at: stamp(new Date()), action: 'Extended', by: CURRENT_USER, note: `To date ${a.to || 'open'} → ${t.to}` });
    a.to = t.to;
    if (a.status === 'Overdue' && t.to >= isoDate(new Date())) a.status = 'Assigned';
    this.extendTarget = null;
    this.showToast(`${a.tag} extended to ${t.to}.`);
  }

  openEnd(a: Assignment): void {
    this.closeMenus();
    this.endTarget = a;
  }

  confirmEnd(): void {
    const a = this.endTarget;
    if (!a) return;
    const today = isoDate(new Date());
    a.history.push({ at: stamp(new Date()), action: 'Assignment ended', by: CURRENT_USER, note: `Returned by ${a.assignee}` });
    a.status = 'Returned';
    a.to = today;
    this.endTarget = null;
    this.showToast(`Assignment of ${a.tag} ended.`);
  }

  /** Dates edited in the details panel. */
  onDatesChange(a: Assignment): void {
    if (a.to && a.to < a.from) {
      a.to = '';
      return this.showToast('The to date must be on or after the from date.');
    }
    a.history.push({ at: stamp(new Date()), action: 'Dates updated', by: CURRENT_USER, note: `${a.from} → ${a.to || 'open'}` });
  }

  // ===== Export =====

  export(): void {
    const source = this.checked.size ? this.filtered.filter((a) => this.checked.has(a.id)) : this.filtered;
    const rows = source.map((a, i) => ({
      '#': i + 1, Asset: a.assetName, 'Asset Type': a.assetType, 'Asset Tag': a.tag, 'Serial Number': a.serial, 'Assignment Type': a.type, 'Assigned To': a.assignee,
      Department: a.department, Location: a.location, 'From Date': a.from, 'To Date': a.to, Status: a.status, 'Assigned By': a.assignedBy, Remarks: a.remarks,
    }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Asset Assignments');
    XLSX.writeFile(book, 'Asset_Assignments.xlsx');
    this.menuOpen = '';
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.rowMenu = null;
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  openRowMenu(row: Assignment, event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen = '';
    if (this.rowMenu?.row === row) return void (this.rowMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 190 > window.innerHeight ? r.top - 186 : r.bottom + 6;
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
    this.assignForm = this.extendTarget = this.endTarget = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
