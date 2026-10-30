import { Component, HostListener, OnDestroy, inject } from '@angular/core';
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

/**
 * DEMO MODE: asset disposal sample data until the Asset Disposal API returns requests, workflow steps, attachments and notes.
 * The first ten rows match the design; the rest fill out the list so filters and paging have something to work with.
 */

export type DisposalStatus = 'Draft' | 'Pending' | 'Approved' | 'Completed' | 'Rejected' | 'Cancelled';
export type DisposalMethod = 'Recycle' | 'Return to Vendor' | 'Donate' | 'Scrap' | 'Sell' | 'Auction';

export interface DisposalAttachment {
  name: string;
  size: number;
  added: string;
}

export interface DisposalNote {
  text: string;
  by: string;
  /** ISO date-time */
  at: string;
}

export interface DisposalRequest {
  id: number;
  /** "DISP-YYYY-NNN" */
  reference: string;
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
  /** Estimated recovery value in AED. */
  value: number;
  attachments: DisposalAttachment[];
  notes: DisposalNote[];
  /** Every asset in a multi-asset request (New Disposal Request page); single-asset requests leave it unset. */
  items?: DisposalItem[];
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

interface DisposalForm {
  /** Request being edited; null for a new request. */
  id: number | null;
  assetName: string;
  tag: string;
  category: string;
  location: string;
  locationDetail: string;
  serial: string;
  model: string;
  manufacturer: string;
  method: DisposalMethod;
  reason: string;
  value: number;
}

export const DISPOSAL_METHODS: DisposalMethod[] = ['Recycle', 'Return to Vendor', 'Donate', 'Scrap', 'Sell', 'Auction'];
const DISPOSAL_STATUSES: DisposalStatus[] = ['Draft', 'Pending', 'Approved', 'Completed', 'Rejected', 'Cancelled'];
export const CATEGORIES = ['IT Equipment', 'Networking', 'Furniture', 'HVAC', 'Audio Visual', 'Electrical', 'Vehicles', 'Tools'];
const LOCATIONS = ['Head Office', 'Data Centre', 'Main Building', 'Block A', 'Block B', 'Training Room', 'Finance Dept', 'Warehouse'];
export const REASONS = ['End of Life', 'Damaged Beyond Repair', 'Obsolete', 'Replaced by Upgrade', 'Lost / Stolen', 'Surplus'];
const CURRENT_USER = 'Rex Kumar';

type Row = [string, string, string, string, string, DisposalMethod, DisposalStatus, string, string, string, string, string, number];

// name, tag, category, location, location detail, method, status, requested, requested by, reason, serial, model, value (AED)
const ROWS: Row[] = [
  ['Dell Latitude 7420', 'LT-000125', 'IT Equipment', 'Head Office', 'Head Office - IT Department', 'Recycle', 'Pending', '2025-12-12T10:30', 'Ramesh Kumar', 'End of Life', '5CD1234ABC', 'Latitude 7420', 0],
  ['Network Switch', 'SW-000078', 'Networking', 'Data Centre', 'Data Centre - Rack 4', 'Return to Vendor', 'Approved', '2025-12-10T09:15', 'Ahmed Khan', 'Replaced by Upgrade', 'FOC2231X0QZ', 'Catalyst 2960', 350],
  ['Office Chair', 'CH-000156', 'Furniture', 'Main Building', 'Main Building - 2nd Floor', 'Donate', 'Completed', '2025-12-08T14:00', 'Priya Nair', 'Surplus', 'HM-AER-5521', 'Aeron B', 0],
  ['Air Conditioner', 'AC-000034', 'HVAC', 'Block A', 'Block A - Server Room', 'Scrap', 'Rejected', '2025-12-05T11:45', 'Salim Ahmed', 'Damaged Beyond Repair', 'DK-FTKF-88213', 'FTKF140', 200],
  ['Projector', 'PR-000089', 'Audio Visual', 'Training Room', 'Training Room - Hall 1', 'Recycle', 'Pending', '2025-12-04T16:20', 'Nisha Patel', 'Obsolete', 'EP-EB-X51-773', 'EB-X51', 120],
  ['Server Rack', 'SR-000012', 'IT Equipment', 'Data Centre', 'Data Centre - Row B', 'Scrap', 'Approved', '2025-11-28T08:50', 'Ahmed Khan', 'End of Life', 'APC-AR3100-019', 'NetShelter SX', 400],
  ['Printer', 'PRN-000221', 'IT Equipment', 'Finance Dept', 'Finance Dept - Ground Floor', 'Return to Vendor', 'Completed', '2025-11-25T13:10', 'Ramesh Kumar', 'Replaced by Upgrade', 'VNB3K12345', 'LaserJet M404', 150],
  ['Workstation PC', 'PC-000334', 'IT Equipment', 'Block B', 'Block B - Design Studio', 'Recycle', 'Pending', '2025-11-22T10:05', 'Priya Nair', 'Obsolete', 'MXL9021ZQ1', 'OptiPlex 7070', 80],
  ['Meeting Table', 'TB-000067', 'Furniture', 'Main Building', 'Main Building - Board Room', 'Donate', 'Completed', '2025-11-18T15:30', 'Salim Ahmed', 'Surplus', 'STC-MT-2400', 'Boardroom 2400', 0],
  ['UPS', 'UPS-000045', 'Electrical', 'Data Centre', 'Data Centre - Power Room', 'Recycle', 'Pending', '2025-11-15T09:40', 'Ahmed Khan', 'End of Life', 'SE-GVS-100-551', 'Galaxy VS 100', 600],
  ['HP EliteBook 840', 'LT-000098', 'IT Equipment', 'Head Office', 'Head Office - Sales', 'Sell', 'Completed', '2025-11-10T11:00', 'Nisha Patel', 'Replaced by Upgrade', '5CG8123KLM', 'EliteBook 840 G5', 900],
  ['Ceiling Speaker Set', 'AV-000041', 'Audio Visual', 'Training Room', 'Training Room - Hall 2', 'Scrap', 'Completed', '2025-11-04T10:30', 'Priya Nair', 'Damaged Beyond Repair', 'BO-CS-6611', 'FreeSpace DS', 0],
  ['Firewall Appliance', 'FW-000009', 'Networking', 'Data Centre', 'Data Centre - Rack 1', 'Return to Vendor', 'Completed', '2025-10-28T09:20', 'Ahmed Khan', 'End of Life', 'FGT60E3U1800', 'FortiGate 60E', 250],
  ['Filing Cabinet', 'FC-000210', 'Furniture', 'Finance Dept', 'Finance Dept - Records', 'Donate', 'Completed', '2025-10-20T14:45', 'Ramesh Kumar', 'Surplus', 'STL-FC4-210', '4-Drawer Steel', 0],
  ['Split AC - 2 Ton', 'AC-000052', 'HVAC', 'Block B', 'Block B - 1st Floor', 'Scrap', 'Rejected', '2025-10-14T12:10', 'Salim Ahmed', 'Damaged Beyond Repair', 'DK-FTKF-90122', 'FTKF71', 150],
  ['Delivery Van', 'VH-000007', 'Vehicles', 'Warehouse', 'Warehouse - Transport Yard', 'Auction', 'Completed', '2025-10-06T08:30', 'Ramesh Kumar', 'End of Life', 'JTFSS22P0K0012', 'Hiace 2015', 18500],
  ['Cordless Drill Kit', 'TL-000133', 'Tools', 'Warehouse', 'Warehouse - Tool Crib', 'Recycle', 'Completed', '2025-09-29T16:00', 'Priya Nair', 'Lost / Stolen', 'MKT-DHP482-77', 'DHP482', 0],
  ['Monitor 27"', 'MN-000412', 'IT Equipment', 'Block A', 'Block A - Control Room', 'Recycle', 'Completed', '2025-09-22T10:15', 'Nisha Patel', 'Damaged Beyond Repair', 'CN-0P2719-411', 'P2719H', 0],
];

export const DISPOSALS: DisposalRequest[] = ROWS.map(
  ([assetName, tag, category, location, locationDetail, method, status, requested, requestedBy, reason, serial, model, value], i) => ({
    id: i + 1,
    reference: `DISP-2025-${String(i + 1).padStart(3, '0')}`,
    assetName, tag, category, location, locationDetail, method, status, requested, requestedBy, reason, serial, model, value,
    manufacturer: assetName.split(' ')[0],
    attachments: i === 0
      ? [{ name: 'Asset_Condition_Report.pdf', size: 248_000, added: requested }, { name: 'Laptop_Photo.jpg', size: 1_240_000, added: requested }]
      : [],
    notes: [],
  }),
);

/** Next reference number for a new request, e.g. "DISP-2025-019". */
export function nextDisposalReference(requests: DisposalRequest[], year = new Date().getFullYear()): string {
  const max = requests.reduce((n, r) => Math.max(n, Number(r.reference.split('-')[2]) || 0), 0);
  return `DISP-${year}-${String(max + 1).padStart(3, '0')}`;
}

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
export class AssetDisposal implements OnDestroy {
  readonly icons = {
    Building2, CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, CircleX, Clock, Download, EllipsisVertical, FileText,
    Folder, Funnel, MapPin, Package, Paperclip, Pencil, Plus, Recycle, Save, Search, SlidersHorizontal, Trash2, Upload, X,
  };
  readonly methods = DISPOSAL_METHODS;
  readonly statuses = DISPOSAL_STATUSES;
  readonly categories = CATEGORIES;
  readonly locations = LOCATIONS;
  readonly reasons = REASONS;
  readonly pageSizes = [10, 25, 50];
  private readonly router = inject(Router);

  /** DEMO MODE: shared in-memory list until the Asset Disposal API is available. */
  readonly requests: DisposalRequest[] = DISPOSALS;

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

  checked = new Set<number>();
  /** Request shown in the side panel and the tabs under the table. */
  selected: DisposalRequest | null = this.requests[0] ?? null;
  tab: Tab = 'details';
  noteText = '';

  menuOpen = '';
  /** Row actions menu, fixed to the viewport so the scrolling table doesn't clip it. */
  rowMenu: { row: DisposalRequest; top: number; right: number } | null = null;

  form: DisposalForm | null = null;
  deleteTarget: DisposalRequest | null = null;
  rejectTarget: DisposalRequest | null = null;
  rejectReason = '';
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
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
        (!q || `${r.reference} ${r.assetName} ${r.tag}`.toLowerCase().includes(q)),
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

  toggleCheck(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  select(r: DisposalRequest): void {
    this.closeMenus();
    this.selected = r;
    this.noteText = '';
  }

  // ===== Display helpers =====

  statusClass(status: DisposalStatus): string {
    return 'status--' + status.toLowerCase();
  }

  imageOf(r: DisposalRequest): string | null {
    return DISPOSAL_IMAGES[r.category] ?? null;
  }

  /** Approval steps, worked out from the request's status. */
  workflow(r: DisposalRequest): WorkflowStep[] {
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

  /** Assets in the request: the item list of a multi-asset request, otherwise the request's own asset. */
  itemsOf(r: DisposalRequest): DisposalItem[] {
    return r.items ?? [{ assetName: r.assetName, tag: r.tag, category: r.category, model: r.model, serial: r.serial, location: r.location, condition: r.reason, value: r.value }];
  }

  openEdit(r: DisposalRequest): void {
    this.closeMenus();
    this.formError = '';
    const { id, assetName, tag, category, location, locationDetail, serial, model, manufacturer, method, reason, value } = r;
    this.form = { id, assetName, tag, category, location, locationDetail, serial, model, manufacturer, method, reason, value };
  }

  saveForm(): void {
    const f = this.form;
    if (!f) return;
    if (!f.assetName.trim()) return void (this.formError = 'Enter the asset name.');
    if (!f.tag.trim()) return void (this.formError = 'Enter the asset tag.');
    if (!f.category) return void (this.formError = 'Choose the category.');
    if (!f.location) return void (this.formError = 'Choose the location.');
    if (f.value == null || f.value < 0) return void (this.formError = 'Enter the estimated value (0 if none).');

    const fields = {
      assetName: f.assetName.trim(), tag: f.tag.trim().toUpperCase(), category: f.category, location: f.location,
      locationDetail: f.locationDetail.trim() || f.location, serial: f.serial.trim(), model: f.model.trim(),
      manufacturer: f.manufacturer.trim(), method: f.method, reason: f.reason, value: Number(f.value),
    };
    const existing = this.requests.find((r) => r.id === f.id);
    if (existing) {
      Object.assign(existing, fields);
      this.showToast(`${existing.reference} updated.`);
    } else {
      const created: DisposalRequest = {
        id: this.requests.reduce((n, r) => Math.max(n, r.id), 0) + 1,
        reference: nextDisposalReference(this.requests),
        ...fields,
        status: 'Draft',
        requested: nowStamp(),
        requestedBy: CURRENT_USER,
        attachments: [],
        notes: [],
      };
      this.requests.unshift(created);
      this.selected = created;
      this.page = 0;
      this.showToast(`${created.reference} saved as draft.`);
    }
    this.form = null;
  }

  // ===== Status changes =====

  saveDraft(r: DisposalRequest): void {
    this.closeMenus();
    if (r.status === 'Draft') return this.showToast(`${r.reference} saved as draft.`);
    if (r.status !== 'Pending') return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't go back to draft.`);
    r.status = 'Draft';
    this.showToast(`${r.reference} moved back to draft.`);
  }

  submitForApproval(r: DisposalRequest): void {
    this.closeMenus();
    if (r.status === 'Pending') return this.showToast(`${r.reference} is already waiting for approval.`);
    if (r.status !== 'Draft') return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't be submitted.`);
    r.status = 'Pending';
    r.requested = nowStamp();
    this.showToast(`${r.reference} submitted for approval.`);
  }

  approve(r: DisposalRequest): void {
    this.closeMenus();
    r.status = 'Approved';
    this.showToast(`${r.reference} approved.`);
  }

  complete(r: DisposalRequest): void {
    this.closeMenus();
    r.status = 'Completed';
    this.showToast(`${r.reference} marked as disposed.`);
  }

  openReject(r: DisposalRequest): void {
    this.closeMenus();
    this.rejectReason = '';
    this.rejectTarget = r;
  }

  confirmReject(): void {
    const r = this.rejectTarget;
    if (!r) return;
    r.status = 'Rejected';
    if (this.rejectReason.trim()) r.notes.unshift({ text: `Rejected: ${this.rejectReason.trim()}`, by: CURRENT_USER, at: nowStamp() });
    this.rejectTarget = null;
    this.showToast(`${r.reference} rejected.`);
  }

  cancelRequest(r: DisposalRequest): void {
    this.closeMenus();
    if (r.status !== 'Draft' && r.status !== 'Pending') return this.showToast(`${r.reference} is ${r.status.toLowerCase()} and can't be cancelled.`);
    r.status = 'Cancelled';
    this.showToast(`${r.reference} cancelled.`);
  }

  canCancel(r: DisposalRequest): boolean {
    return r.status === 'Draft' || r.status === 'Pending';
  }

  // ===== Delete =====

  openDelete(r: DisposalRequest): void {
    this.closeMenus();
    this.deleteTarget = r;
  }

  confirmDelete(): void {
    const r = this.deleteTarget;
    if (!r) return;
    this.requests.splice(this.requests.indexOf(r), 1);
    this.checked.delete(r.id);
    if (this.selected === r) this.selected = this.requests[0] ?? null;
    this.deleteTarget = null;
    if (this.page >= this.pageCount) this.page = this.pageCount - 1;
    this.showToast(`${r.reference} deleted.`);
  }

  // ===== Attachments & notes =====

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
    this.selected.notes.unshift({ text, by: CURRENT_USER, at: nowStamp() });
    this.noteText = '';
  }

  // ===== Export =====

  export(format: 'xlsx' | 'csv'): void {
    const source = this.checked.size ? this.filtered.filter((r) => this.checked.has(r.id)) : this.filtered;
    const rows = source.map((r, i) => ({
      '#': i + 1, 'Disposal ID': r.reference, 'Asset Name': r.assetName, 'Asset Tag': r.tag, Category: r.category, Location: r.locationDetail,
      'Disposal Method': r.method, Status: r.status, 'Requested Date': r.requested.slice(0, 10), 'Requested By': r.requestedBy,
      Reason: r.reason, 'Serial Number': r.serial, 'Est. Value (AED)': r.value,
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
    this.form = null;
    this.deleteTarget = this.rejectTarget = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
