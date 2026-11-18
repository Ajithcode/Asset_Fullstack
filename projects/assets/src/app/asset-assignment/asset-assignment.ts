import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
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
import { Observable, finalize, switchMap } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import { Assignment, AssignmentStatus, AssignmentType, AssetKind, ENTITIES, TARGET_LABELS, isoDate } from './asset-assignment-data';
import {
  AssetAssignmentDto, AssetAssignmentService, AssignmentLookups, DEFAULT_ASSIGNMENT_TYPE, EMPTY_LOOKUPS, choicesFor, requestFrom, targetFor, toRows, uniqueSorted,
} from './asset-assignment.service';

type ColumnKey = 'tag' | 'type' | 'assignee' | 'from' | 'to' | 'status';
type DetailTab = 'assignment' | 'history' | 'related';

interface AssignForm {
  mode: 'reassign' | 'bulk';
  /** Rows being reassigned (reassign / bulk). */
  ids: string[];
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
export class AssetAssignment implements OnInit, OnDestroy {
  readonly icons = {
    ArrowUp, Building2, CalendarPlus, ChevronDown, ChevronLeft, ChevronRight, CircleStop, Columns3, Download, EllipsisVertical, Layers, MapPin, Plus,
    Repeat, RotateCcw, Search, SlidersHorizontal, UserRound, X,
  };
  readonly kindIcons: Record<AssetKind, typeof Laptop> = {
    laptop: Laptop, phone: Smartphone, scanner: ScanBarcode, projector: Projector, forklift: Forklift, tool: Wrench, card: CreditCard, monitor: Monitor, tablet: Tablet, camera: Cctv,
    other: Layers,
  };
  readonly assignmentTypes: AssignmentType[] = ['Employee', 'Department', 'Location', 'Other'];
  readonly targetLabels = TARGET_LABELS;
  readonly statuses: AssignmentStatus[] = ['Assigned', 'Overdue', 'Returned'];
  readonly pageSizes = [10, 25, 50];
  readonly columns: { key: ColumnKey; label: string }[] = [
    { key: 'tag', label: 'Asset Tag / Serial' },
    { key: 'type', label: 'Assignment Type' },
    { key: 'assignee', label: 'Assigned To' },
    { key: 'from', label: 'From Date' },
    { key: 'to', label: 'To Date' },
    { key: 'status', label: 'Status' },
  ];
  /** One row per asset of each assignment (drafts are not listed). */
  assignments: Assignment[] = [];
  /** API records by id, for building update requests. */
  private records = new Map<string, AssetAssignmentDto>();
  private lookups: AssignmentLookups = EMPTY_LOOKUPS;
  loading = false;
  loadError = '';
  saving = false;
  historyLoading = false;

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
  checked = new Set<string>();
  selectedId: string | null = null;
  tab: DetailTab = 'assignment';
  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: Assignment; top: number; right: number } | null = null;

  assignForm: AssignForm | null = null;
  formError = '';
  extendTarget: { id: string; to: string } | null = null;
  endTarget: Assignment | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);
  private readonly api = inject(AssetAssignmentService);

  constructor() {
    // Message handed over by the Assign Asset page after saving
    const message = this.router.getCurrentNavigation()?.extras.state?.['toast'];
    if (typeof message === 'string') this.showToast(message);
  }

  ngOnInit(): void {
    this.load();
    this.api.lookups().subscribe((lk) => (this.lookups = lk));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  /** Reads every assignment; keeps the selected row when it is still there. */
  load(): void {
    this.loading = true;
    this.loadError = '';
    this.api
      .listAll()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (list) => {
          const shown = list.filter((d) => !/^draft$/i.test(d.status ?? ''));
          this.records = new Map(shown.map((d) => [d.id, d]));
          this.assignments = shown.flatMap(toRows);
          for (const id of [...this.checked]) if (!this.assignments.some((a) => a.id === id)) this.checked.delete(id);
          if (!this.assignments.some((a) => a.id === this.selectedId)) this.selectedId = this.assignments[0]?.id ?? null;
          if (this.page >= this.pageCount) this.page = 0;
        },
        error: (err) => {
          this.loadError = apiErrorMessage(err, 'Assignments could not be loaded.');
          this.showToast(this.loadError);
        },
      });
  }

  /** Puts a changed assignment back into the list without reloading everything. */
  private replace(dto: AssetAssignmentDto): void {
    this.records.set(dto.id, dto);
    const rows = toRows(dto);
    const at = this.assignments.findIndex((a) => a.assignmentId === dto.id);
    const rest = this.assignments.filter((a) => a.assignmentId !== dto.id);
    rest.splice(at < 0 ? 0 : at, 0, ...rows);
    this.assignments = rest;
  }

  /** Runs a save; shows the error in the open form (or as a toast) and keeps the form open. */
  private run<T>(call: Observable<T>, done: (result: T) => void): void {
    if (this.saving) return;
    this.saving = true;
    this.formError = '';
    call.pipe(finalize(() => (this.saving = false))).subscribe({
      next: done,
      error: (err) => {
        const message = apiErrorMessage(err);
        if (this.assignForm || this.extendTarget) this.formError = message;
        else this.showToast(message);
      },
    });
  }

  // ===== Summary =====

  get active(): Assignment[] {
    return this.assignments.filter((a) => a.status !== 'Returned');
  }

  countOf(type: AssignmentType): number {
    return this.active.filter((a) => a.type === type).length;
  }

  /** Change on the summary cards: assets assigned in the last 30 days, as a share of those assigned before. */
  get trends(): Record<'total' | AssignmentType, number> {
    const since = isoDate(new Date(Date.now() - 30 * 86_400_000));
    const trend = (rows: Assignment[]) => {
      const recent = rows.filter((a) => a.assignedOn.slice(0, 10) >= since).length;
      const before = rows.length - recent;
      return before ? Math.round((recent / before) * 100) : recent ? 100 : 0;
    };
    const of = (t: AssignmentType) => this.active.filter((a) => a.type === t);
    return { total: trend(this.active), Employee: trend(of('Employee')), Department: trend(of('Department')), Location: trend(of('Location')), Other: trend(of('Other')) };
  }

  get departments(): string[] {
    return uniqueSorted(this.assignments.map((a) => a.department));
  }

  get locations(): string[] {
    return uniqueSorted(this.assignments.map((a) => a.location));
  }

  /** Names offered in Reassign for a kind of assignee. */
  assigneeChoices(type: AssignmentType): string[] {
    return choicesFor(this.lookups, type, ENTITIES.map((e) => e.name));
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

  toggleCheck(id: string): void {
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

  /** History tab: the assignment's event log. */
  openHistory(a: Assignment): void {
    this.tab = 'history';
    this.historyLoading = true;
    this.api
      .history(a.assignmentId)
      .pipe(finalize(() => (this.historyLoading = false)))
      .subscribe({
        next: (events) => {
          for (const row of this.assignments) if (row.assignmentId === a.assignmentId) row.history = events;
        },
        error: (err) => this.showToast(apiErrorMessage(err, 'History could not be loaded.')),
      });
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

  /** Ends the current assignment of each asset and assigns them all to the new assignee in one assignment. */
  saveAssign(): void {
    const f = this.assignForm;
    if (!f) return;
    if (!f.assignee) return void (this.formError = `Choose the ${f.type.toLowerCase()} to assign to.`);
    if (!f.from) return void (this.formError = 'Choose the from date.');
    if (f.to && f.to < f.from) return void (this.formError = 'The to date must be on or after the from date.');

    const rows = this.assignments.filter((x) => f.ids.includes(x.id) && x.status !== 'Returned');
    if (!rows.length) return void (this.formError = 'Nothing to reassign.');
    const first = this.records.get(rows[0].assignmentId);
    const target = targetFor(this.lookups, f.type, f.assignee, ENTITIES.find((e) => e.name === f.assignee)?.kind);
    if (f.type === 'Employee' && !target.employee?.employeeId) return void (this.formError = `Employee '${f.assignee}' was not found in master data.`);
    if (f.type === 'Department' && !target.department?.departmentId) return void (this.formError = `Department '${f.assignee}' was not found in master data.`);
    if (f.type === 'Location' && !target.location?.siteId) return void (this.formError = `Site '${f.assignee}' was not found in master data.`);

    const base = first ? requestFrom(first) : null;
    const request = {
      ...(base ?? {
        selectionMode: null, assignmentType: null, fromDate: null, toDate: null, isOpenEnded: true, costCenter: null, purpose: null, approvalReference: null, remarks: null,
        notes: null, attachments: [], notification: null, reminder: null, customFields: null, createdBy: null, clientId: null, tenantId: null,
      }),
      ...target,
      selectionMode: rows.length > 1 ? 'Multiple' : 'Single',
      assetRegistrationIds: [...new Set(rows.map((r) => r.assetRegistrationId))],
      assignmentType: DEFAULT_ASSIGNMENT_TYPE[f.type],
      fromDate: f.from,
      toDate: f.to || null,
      isOpenEnded: !f.to,
      remarks: f.remarks.trim() || null,
      attachments: [],
      reminder: null,
      createdBy: null,
    };
    if (f.type !== 'Location' && base?.location && !target.location) request.location = base.location;
    const label = f.assignee;
    this.run(this.api.returnRows(rows, `Reassigned to ${label}`).pipe(switchMap(() => this.api.create(request))), () => {
      this.checked.clear();
      this.showToast(rows.length === 1 ? `Reassigned to ${label}.` : `${rows.length} assets reassigned to ${label}.`);
      this.assignForm = null;
      this.page = 0;
      this.load();
    });
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
    const dto = a && this.records.get(a.assignmentId);
    if (!t || !a || !dto) return;
    if (!t.to) return void (this.formError = 'Choose the new to date.');
    if (t.to < a.from) return void (this.formError = 'The to date must be on or after the from date.');
    const request = { ...requestFrom(dto), toDate: t.to, isOpenEnded: false };
    this.run(this.api.update(dto.id, request), (saved) => {
      this.replace(saved);
      this.extendTarget = null;
      this.showToast(`${a.tag} extended to ${t.to}.`);
    });
  }

  openEnd(a: Assignment): void {
    this.closeMenus();
    this.endTarget = a;
  }

  /** Returns the asset: its assignment ends today and the asset is released. */
  confirmEnd(): void {
    const a = this.endTarget;
    if (!a) return;
    this.run(this.api.returnAssets(a.assignmentId, [a.assetRegistrationId]), () => {
      this.endTarget = null;
      this.showToast(`Assignment of ${a.tag} ended.`);
      this.load();
    });
  }

  /** Dates edited in the details panel are saved on the assignment (all its assets). */
  onDatesChange(a: Assignment): void {
    const dto = this.records.get(a.assignmentId);
    if (!dto) return;
    if (!a.from) return this.revert(dto, 'Choose the from date.');
    if (a.to && a.to < a.from) return this.revert(dto, 'The to date must be on or after the from date.');
    const request = { ...requestFrom(dto), fromDate: a.from, toDate: a.to || null, isOpenEnded: !a.to };
    this.saveRow(dto, request, 'Dates updated.');
  }

  /** Remarks edited in the details panel. */
  saveRemarks(a: Assignment): void {
    const dto = this.records.get(a.assignmentId);
    if (!dto || (dto.remarks ?? '') === a.remarks.trim()) return;
    this.saveRow(dto, { ...requestFrom(dto), remarks: a.remarks.trim() || null }, 'Remarks saved.');
  }

  private saveRow(dto: AssetAssignmentDto, request: ReturnType<typeof requestFrom>, message: string): void {
    this.api.update(dto.id, request).subscribe({
      next: (saved) => {
        this.replace(saved);
        this.showToast(message);
      },
      error: (err) => this.revert(dto, apiErrorMessage(err)),
    });
  }

  /** Puts the saved values back after a failed edit. */
  private revert(dto: AssetAssignmentDto, message: string): void {
    this.replace(dto);
    this.showToast(message);
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
