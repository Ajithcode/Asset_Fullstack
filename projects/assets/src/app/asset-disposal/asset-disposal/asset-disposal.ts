import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  Building2,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  CircleX,
  Clock,
  Download,
  EllipsisVertical,
  FileText,
  Folder,
  Funnel,
  MapPin,
  Package,
  Paperclip,
  Pencil,
  Plus,
  Recycle,
  Save,
  Search,
  SlidersHorizontal,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../../services/asset-api';
import { AssetDisposalService, DisposalApproverDto, DisposalListItemDto, DisposalRequestDto, toUiStatus } from '../asset-disposal.service';

export type DisposalStatus = 'Draft' | 'Pending' | 'Approved' | 'Completed' | 'Rejected' | 'Cancelled';
/** Disposal methods offered by the Asset API (GET /api/asset-disposals/options). */
export type DisposalMethod = string;

export interface DisposalAttachment {
  name: string;
  size: number;
  added: string;
  /** Media URL of a file stored on the request; unset for files added in this session only. */
  url?: string;
  description?: string;
}

export interface DisposalNote {
  text: string;
  by: string;
  /** ISO date-time */
  at: string;
}

export interface DisposalRequest {
  /** Asset API id of the disposal request. */
  id: string;
  /** Request number, e.g. "DSP-000012" */
  reference: string;
  /** Request title. */
  title: string;
  /** First asset of the request (or the title when it has none). */
  assetName: string;
  tag: string;
  category: string;
  /** Short site name shown in the table. */
  location: string;
  /** Site and area shown in the details panel. */
  locationDetail: string;
  method: DisposalMethod;
  status: DisposalStatus;
  /** ISO date-time */
  requested: string;
  requestedBy: string;
  reason: string;
  serial: string;
  model: string;
  manufacturer: string;
  /** Total estimated value in AED. */
  value: number;
  assetCount: number;
  vendor: string;
  pendingWith: string;
  attachments: DisposalAttachment[];
  notes: DisposalNote[];
  /** Every asset in the request, once its details are loaded. */
  items?: DisposalItem[];
  /** Approval levels, once the details are loaded. */
  approvers?: DisposalApproverDto[];
  submittedAt?: string | null;
  submittedBy?: string | null;
  completedAt?: string | null;
  /** True once GET /api/asset-disposals/{id} has filled in the details. */
  loaded?: boolean;
}

export interface DisposalItem {
  assetName: string;
  tag: string;
  category: string;
  model: string;
  serial: string;
  location: string;
  condition: string;
  value: number;
}

type StepState = 'done' | 'current' | 'pending' | 'rejected';

interface WorkflowStep {
  title: string;
  state: StepState;
  label: string;
  at?: string;
  by?: string;
}

/** Fallback lists until GET /api/asset-disposals/options answers. */
export const DISPOSAL_METHODS: DisposalMethod[] = ['Recycle', 'Scrap', 'Return to Vendor', 'Donate', 'Sell / Auction'];
const DISPOSAL_STATUSES: DisposalStatus[] = ['Draft', 'Pending', 'Approved', 'Completed', 'Rejected', 'Cancelled'];
export const REASONS = ['End of Life', 'Damaged Beyond Repair', 'Obsolete', 'Uneconomical to Repair', 'Lost / Stolen', 'Surplus / Not Required', 'Other'];

/** Local "YYYY-MM-DDTHH:mm", the format the requested date is kept in. */
export function nowStamp(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** Preview pictures from public/asset-images; other categories show an icon. */
export const DISPOSAL_IMAGES: Record<string, string> = {
  'IT Equipment': 'asset-images/laptop-overview.svg',
  HVAC: 'asset-images/ac-overview.svg',
  Electrical: 'asset-images/ups-overview.svg',
  'Audio Visual': 'asset-images/cctvdome-overview.svg',
};

/** Table row from a list item of GET /api/asset-disposals. */
export function fromListItem(d: DisposalListItemDto): DisposalRequest {
  const a = d.firstAsset;
  const more = d.assetCount > 1 ? ` +${d.assetCount - 1}` : '';
  return {
    id: d.id,
    reference: d.requestNumber,
    title: d.requestTitle,
    assetName: a?.assetName || d.requestTitle,
    tag: (a?.assetTag || '-') + more,
    category: a?.category || d.disposalCategory || '',
    location: a?.location || '',
    locationDetail: a?.location || '',
    method: d.disposalMethod ?? '',
    status: toUiStatus(d.status),
    requested: d.requestDate || d.createdAt || '',
    requestedBy: d.requestedBy ?? '',
    reason: d.reason ?? '',
    serial: a?.serialNumber ?? '',
    model: a?.model ?? '',
    manufacturer: a?.manufacturer ?? '',
    value: d.totalEstimatedValue ?? 0,
    assetCount: d.assetCount ?? 0,
    vendor: d.vendor ?? '',
    pendingWith: d.pendingWith ?? '',
    attachments: [],
    notes: [],
  };
}

/** Fills a row with the full request from GET /api/asset-disposals/{id} (or a workflow action's response). */
export function applyDetails(r: DisposalRequest, d: DisposalRequestDto): DisposalRequest {
  const a = d.assets?.[0];
  const more = d.assetCount > 1 ? ` +${d.assetCount - 1}` : '';
  Object.assign(r, {
    reference: d.requestNumber,
    title: d.requestTitle,
    assetName: a?.assetName || d.requestTitle,
    tag: (a?.assetTag || '-') + more,
    category: a?.category || d.disposalCategory || '',
    location: a?.location || '',
    locationDetail: [a?.location, a?.department].filter(Boolean).join(' - '),
    method: d.method?.method ?? '',
    status: toUiStatus(d.status),
    requested: d.requestDate || d.createdAt || '',
    requestedBy: d.requestedBy?.name ?? '',
    reason: d.reason ?? '',
    serial: a?.serialNumber ?? '',
    model: a?.model ?? '',
    manufacturer: a?.manufacturer ?? '',
    value: d.totalEstimatedValue ?? 0,
    assetCount: d.assetCount ?? 0,
    vendor: d.method?.vendor?.name ?? '',
    pendingWith: d.currentApprover ? `${d.currentApprover.approver.name} (${d.currentApprover.approvalLevel})` : '',
    items: (d.assets ?? []).map((x) => ({
      assetName: x.assetName ?? '', tag: x.assetTag ?? '', category: x.category ?? '', model: x.model ?? '', serial: x.serialNumber ?? '',
      location: x.location ?? '', condition: x.condition ?? '', value: x.estimatedValue ?? 0,
    })),
    attachments: (d.attachments ?? []).map((f) => ({
      name: f.fileName, size: f.fileSize ?? 0, added: f.uploadedAt ?? '', url: f.fileUrl, description: f.description ?? '',
    })),
    approvers: d.approvers ?? [],
    submittedAt: d.submittedAt,
    submittedBy: d.submittedBy,
    completedAt: d.completion?.completedAt ?? null,
    loaded: true,
  });
  // Notes: what was written on the request and by the approvers
  const notes: DisposalNote[] = [];
  if (d.justification) notes.push({ text: d.justification, by: d.requestedBy?.name || d.createdBy || '', at: d.createdAt ?? '' });
  if (d.method?.instructions) notes.push({ text: `Instructions: ${d.method.instructions}`, by: d.createdBy ?? '', at: d.createdAt ?? '' });
  if (d.remarks) notes.push({ text: d.remarks, by: d.updatedBy || d.createdBy || '', at: d.updatedAt ?? d.createdAt ?? '' });
  for (const ap of d.approvers ?? []) {
    if (ap.comments) notes.push({ text: `${ap.status} (${ap.approvalLevel}): ${ap.comments}`, by: ap.actedBy || ap.approver.name, at: ap.actedAt ?? '' });
  }
  if (d.cancelledReason) notes.push({ text: `Cancelled: ${d.cancelledReason}`, by: d.updatedBy ?? '', at: d.updatedAt ?? '' });
  if (d.completion?.remarks) notes.push({ text: `Completed: ${d.completion.remarks}`, by: d.completion.completedBy ?? '', at: d.completion.completedAt ?? '' });
  r.notes = notes.sort((x, y) => (y.at || '').localeCompare(x.at || ''));
  return r;
}

type Tab = 'details' | 'items' | 'workflow' | 'files' | 'notes';

@Component({
  selector: 'app-asset-disposal',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-disposal.html',
  styleUrls: [
    '../../asset-assignment/asset-assignment.css',
    '../../asset-assignment/asset-assignment-table.css',
    './asset-disposal.css',
  ],
})
export class AssetDisposal implements OnInit, OnDestroy {
  readonly icons = {
    Building2, CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, CircleX, Clock, Download, EllipsisVertical, FileText,
    Folder, Funnel, MapPin, Package, Paperclip, Pencil, Plus, Recycle, Save, Search, SlidersHorizontal, Trash2, Upload, X,
  };
  methods: DisposalMethod[] = DISPOSAL_METHODS;
  readonly statuses = DISPOSAL_STATUSES;
  readonly pageSizes = [10, 25, 50];
  private readonly router = inject(Router);
  private readonly api = inject(AssetDisposalService);

  /** Requests from GET /api/asset-disposals. */
  requests: DisposalRequest[] = [];
  loading = false;
  loadError = '';
  /** An action is in flight; buttons stay usable but repeated clicks are ignored. */
  busy = false;

  // Filters
  search = '';
  statusFilter: DisposalStatus | '' = '';
  methodFilter: DisposalMethod | '' = '';
  fromDate = '';
  toDate = '';
  moreFilters = false;
  categoryFilter = '';
  locationFilter = '';
  pageSize = 10;
  page = 0;

  checked = new Set<string>();
  /** Request shown in the side panel and the tabs under the table. */
  selected: DisposalRequest | null = null;
  tab: Tab = 'details';
  noteText = '';

  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: DisposalRequest; top: number; right: number } | null = null;

  deleteTarget: DisposalRequest | null = null;
  rejectTarget: DisposalRequest | null = null;
  rejectReason = '';
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.api.options().subscribe({
      next: (o) => {
        if (o?.methods?.length) this.methods = o.methods.map((m) => m.method);
      },
      error: () => undefined,
    });
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  /** Loads the request list (all pages up to the API maximum); filters and paging work on it locally. */
  load(): void {
    this.loading = true;
    this.loadError = '';
    this.api.list({ pageSize: 200 }).subscribe({
      next: (res) => {
        const selectedId = this.selected?.id;
        this.requests = (res?.items ?? []).map(fromListItem);
        this.loading = false;
        const again = this.requests.find((r) => r.id === selectedId) ?? this.requests[0] ?? null;
        if (again) this.select(again);
        else this.selected = null;
        if (this.page >= this.pageCount) this.page = this.pageCount - 1;
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load disposal requests.');
        this.showToast(this.loadError);
      },
    });
  }

  // ===== Summary =====

  countOf(status: DisposalStatus): number {
    return this.requests.filter((r) => r.status === status).length;
  }

  /** Summary cards double as a quick status filter. */
  filterStatus(status: DisposalStatus | ''): void {
    this.statusFilter = this.statusFilter === status ? '' : status;
    this.onFilter();
  }

  /** Category and location filters list what the loaded requests contain. */
  get categories(): string[] {
    return [...new Set(this.requests.map((r) => r.category).filter(Boolean))].sort();
  }

  get locations(): string[] {
    return [...new Set(this.requests.map((r) => r.location).filter(Boolean))].sort();
  }

  // ===== Filters & paging =====

  get filtered(): DisposalRequest[] {
    const q = this.search.trim().toLowerCase();
    return this.requests.filter(
      (r) =>
        (!this.statusFilter || r.status === this.statusFilter) &&
        (!this.methodFilter || r.method === this.methodFilter) &&
        (!this.categoryFilter || r.category === this.categoryFilter) &&
        (!this.locationFilter || r.location === this.locationFilter) &&
        (!this.fromDate || r.requested.slice(0, 10) >= this.fromDate) &&
        (!this.toDate || r.requested.slice(0, 10) <= this.toDate) &&
        (!q || `${r.reference} ${r.title} ${r.assetName} ${r.tag}`.toLowerCase().includes(q)),
    );
  }

  get pageRows(): DisposalRequest[] {
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

  onFilter(): void {
    this.page = 0;
  }

  resetFilters(): void {
    this.search = this.statusFilter = this.methodFilter = this.categoryFilter = this.locationFilter = '';
    this.fromDate = this.toDate = '';
    this.onFilter();
  }

  // ===== Selection =====

  get allPageChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.id));
  }

  togglePage(on: boolean): void {
    for (const r of this.pageRows) {
      if (on) this.checked.add(r.id);
      else this.checked.delete(r.id);
    }
  }

  toggleCheck(id: string): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  select(r: DisposalRequest): void {
    this.closeMenus();
    this.selected = r;
    this.noteText = '';
    if (!r.loaded) this.loadDetails(r);
  }

  /** Items, workflow, files and notes come from GET /api/asset-disposals/{id}. */
  private loadDetails(r: DisposalRequest): void {
    this.api.get(r.id).subscribe({
      next: (d) => applyDetails(r, d),
      error: (err) => this.showToast(apiErrorMessage(err, `Could not load ${r.reference}.`)),
    });
  }

  // ===== Display helpers =====

  statusClass(status: DisposalStatus): string {
    return 'status--' + status.toLowerCase();
  }

  imageOf(r: DisposalRequest): string | null {
    return DISPOSAL_IMAGES[r.category] ?? null;
  }

  /** Approval steps: the request's approval levels once loaded, otherwise worked out from its status. */
  workflow(r: DisposalRequest): WorkflowStep[] {
    if (r.approvers?.length) return this.approverFlow(r, r.approvers);
    const titles = ['Request Submitted', 'Manager Approval', 'Finance Approval', 'Disposal Execution', 'Completed'];
    // Steps finished for each status; Rejected stops at the manager
    const done: Record<DisposalStatus, number> = { Draft: 0, Pending: 1, Approved: 3, Completed: 5, Rejected: 1, Cancelled: 1 };
    const n = done[r.status];
    return titles.map((title, i) => {
      if (i === 0 && n > 0) return { title, state: 'done', label: 'Submitted', at: r.requested, by: r.requestedBy };
      if (i < n) return { title, state: 'done', label: 'Approved' };
      if (i === n && r.status === 'Rejected') return { title, state: 'rejected', label: 'Rejected' };
      if (i === n && r.status === 'Cancelled') return { title, state: 'rejected', label: 'Cancelled' };
      if (i === n && r.status !== 'Draft') return { title, state: 'current', label: 'Pending' };
      return { title, state: 'pending', label: i === 0 ? 'Draft' : 'Pending' };
    });
  }

  private approverFlow(r: DisposalRequest, approvers: DisposalApproverDto[]): WorkflowStep[] {
    const submitted = r.status !== 'Draft';
    const steps: WorkflowStep[] = [
      submitted
        ? { title: 'Request Submitted', state: 'done', label: 'Submitted', at: r.submittedAt || r.requested, by: r.submittedBy || r.requestedBy }
        : { title: 'Request Submitted', state: 'pending', label: 'Draft' },
    ];
    const current = r.status === 'Pending' ? [...approvers].sort((a, b) => a.level - b.level).find((a) => a.status === 'Pending') : undefined;
    for (const ap of [...approvers].sort((a, b) => a.level - b.level)) {
      const title = `${ap.approvalLevel} (${ap.approver.name})`;
      if (ap.status === 'Approved' || ap.status === 'Skipped') {
        steps.push({ title, state: 'done', label: ap.status, at: ap.actedAt ?? undefined, by: ap.actedAt ? ap.actedBy || ap.approver.name : undefined });
      } else if (ap.status === 'Rejected') {
        steps.push({ title, state: 'rejected', label: 'Rejected', at: ap.actedAt ?? undefined, by: ap.actedAt ? ap.actedBy || ap.approver.name : undefined });
      } else {
        steps.push({ title, state: ap === current ? 'current' : 'pending', label: 'Pending' });
      }
    }
    if (r.status === 'Completed') steps.push({ title: 'Completed', state: 'done', label: 'Completed', at: r.completedAt ?? undefined });
    else if (r.status === 'Cancelled') steps.push({ title: 'Completed', state: 'rejected', label: 'Cancelled' });
    else steps.push({ title: 'Completed', state: r.status === 'Approved' ? 'current' : 'pending', label: 'Pending' });
    return steps;
  }

  /** Steps with an outcome, newest first, for the Approval History tab (the submission itself isn't an approval). */
  history(r: DisposalRequest): WorkflowStep[] {
    return this.workflow(r).slice(1).filter((s) => s.state === 'done' || s.state === 'rejected').reverse();
  }

  fileSize(bytes: number): string {
    if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  // ===== New / edit =====

  openNew(): void {
    this.closeMenus();
    this.router.navigate(['/assets/disposal/new-disposal']);
  }

  /** Assets in the request: the loaded item list, otherwise the request's first asset. */
  itemsOf(r: DisposalRequest): DisposalItem[] {
    return r.items ?? [{ assetName: r.assetName, tag: r.tag, category: r.category, model: r.model, serial: r.serial, location: r.location, condition: r.reason, value: r.value }];
  }

  /** Drafts and rejected requests can be changed (the API refuses anything else). */
  canEdit(r: DisposalRequest): boolean {
    return r.status === 'Draft' || r.status === 'Rejected';
  }

  /** Opens the request in the New Disposal Request wizard. */
  openEdit(r: DisposalRequest): void {
    this.closeMenus();
    if (!this.canEdit(r)) return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't be edited.`);
    this.router.navigate(['/assets/disposal/new-disposal'], { queryParams: { id: r.id } });
  }

  // ===== Status changes =====

  /** Runs a workflow action and refreshes the row from the response. */
  private act(r: DisposalRequest, call: Observable<DisposalRequestDto>, done: string, after?: () => void): void {
    if (this.busy) return;
    this.busy = true;
    call.subscribe({
      next: (d) => {
        this.busy = false;
        applyDetails(r, d);
        after?.();
        this.showToast(done);
      },
      error: (err) => {
        this.busy = false;
        this.showToast(apiErrorMessage(err));
      },
    });
  }

  saveDraft(r: DisposalRequest): void {
    this.closeMenus();
    if (r.status === 'Draft') return this.showToast(`${r.reference} is saved as a draft.`);
    this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't go back to draft.`);
  }

  submitForApproval(r: DisposalRequest): void {
    this.closeMenus();
    if (r.status === 'Pending') return this.showToast(`${r.reference} is already waiting for approval.`);
    if (!this.canEdit(r)) return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't be submitted.`);
    this.act(r, this.api.submit(r.id), `${r.reference} submitted for approval.`);
  }

  /** Approves the current approval level; the request is Approved once every mandatory level has. */
  approve(r: DisposalRequest): void {
    this.closeMenus();
    this.act(r, this.api.approve(r.id), `${r.reference} approved.`);
  }

  complete(r: DisposalRequest): void {
    this.closeMenus();
    this.act(r, this.api.complete(r.id, { actualDisposalDate: nowStamp().slice(0, 10) }), `${r.reference} marked as disposed.`);
  }

  openReject(r: DisposalRequest): void {
    this.closeMenus();
    this.rejectReason = '';
    this.formError = '';
    this.rejectTarget = r;
  }

  confirmReject(): void {
    const r = this.rejectTarget;
    if (!r) return;
    const reason = this.rejectReason.trim();
    if (!reason) return void (this.formError = 'Give a reason for rejecting.');
    this.formError = '';
    this.act(r, this.api.reject(r.id, reason), `${r.reference} rejected.`, () => (this.rejectTarget = null));
  }

  cancelRequest(r: DisposalRequest): void {
    this.closeMenus();
    if (!this.canCancel(r)) return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't be cancelled.`);
    this.act(r, this.api.cancel(r.id), `${r.reference} cancelled.`);
  }

  canCancel(r: DisposalRequest): boolean {
    return r.status !== 'Completed' && r.status !== 'Cancelled';
  }

  // ===== Delete =====

  openDelete(r: DisposalRequest): void {
    this.closeMenus();
    this.deleteTarget = r;
  }

  /** Only drafts can be deleted; the API explains when a request must be cancelled instead. */
  confirmDelete(): void {
    const r = this.deleteTarget;
    if (!r || this.busy) return;
    this.busy = true;
    this.api.delete(r.id).subscribe({
      next: () => {
        this.busy = false;
        const i = this.requests.indexOf(r);
        if (i >= 0) this.requests.splice(i, 1);
        this.checked.delete(r.id);
        if (this.selected === r) this.selected = null;
        if (!this.selected && this.requests[0]) this.select(this.requests[0]);
        this.deleteTarget = null;
        if (this.page >= this.pageCount) this.page = this.pageCount - 1;
        this.showToast(`${r.reference} deleted.`);
      },
      error: (err) => {
        this.busy = false;
        this.deleteTarget = null;
        this.showToast(apiErrorMessage(err, `Could not delete ${r.reference}.`));
      },
    });
  }

  // ===== Attachments & notes =====
  // No Asset API for adding files (Media upload) or free-text notes after submission: these stay in this session only.

  addFiles(input: HTMLInputElement): void {
    const r = this.selected;
    if (!r || !input.files) return;
    for (const file of Array.from(input.files)) r.attachments.push({ name: file.name, size: file.size, added: nowStamp() });
    input.value = '';
  }

  removeAttachment(a: DisposalAttachment): void {
    this.selected?.attachments.splice(this.selected.attachments.indexOf(a), 1);
  }

  addNote(): void {
    const text = this.noteText.trim();
    if (!this.selected || !text) return;
    this.selected.notes.unshift({ text, by: this.api.currentUser(), at: nowStamp() });
    this.noteText = '';
  }

  // ===== Export =====

  export(format: 'xlsx' | 'csv'): void {
    const source = this.checked.size ? this.filtered.filter((r) => this.checked.has(r.id)) : this.filtered;
    const rows = source.map((r, i) => ({
      '#': i + 1, 'Disposal ID': r.reference, 'Request Title': r.title, 'Asset Name': r.assetName, 'Asset Tag': r.tag, Assets: r.assetCount,
      Category: r.category, Location: r.locationDetail || r.location, 'Disposal Method': r.method, Vendor: r.vendor, Status: r.status,
      'Requested Date': r.requested.slice(0, 10), 'Requested By': r.requestedBy, Reason: r.reason, 'Serial Number': r.serial,
      'Est. Value (AED)': r.value, 'Pending With': r.pendingWith,
    }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Asset Disposals');
    XLSX.writeFile(book, `Asset_Disposals.${format}`, { bookType: format });
    this.menuOpen = '';
  }

  // ===== Menus =====

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.rowMenu = null;
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  openRowMenu(row: DisposalRequest, event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen = '';
    if (this.rowMenu?.row === row) return void (this.rowMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 260 > window.innerHeight ? r.top - 256 : r.bottom + 6;
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
    this.deleteTarget = this.rejectTarget = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
