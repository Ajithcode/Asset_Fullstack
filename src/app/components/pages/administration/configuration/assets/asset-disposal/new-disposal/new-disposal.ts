import { Component, HostListener, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowRight,
  ArrowUpDown,
  Building2,
  CalendarDays,
  Check,
  CloudUpload,
  ChevronLeft,
  ChevronRight,
  EllipsisVertical,
  FileSpreadsheet,
  FileText,
  Funnel,
  GripVertical,
  HandHeart,
  Hexagon,
  Image,
  Info,
  MapPin,
  Package,
  Paperclip,
  Pencil,
  Plus,
  Recycle,
  RefreshCw,
  Save,
  Search,
  Send,
  Tag,
  ShieldCheck,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import {
  CATEGORIES,
  DISPOSALS,
  DISPOSAL_IMAGES,
  DISPOSAL_METHODS,
  DisposalItem,
  DisposalMethod,
  DisposalRequest,
  DisposalStatus,
  REASONS,
  nextDisposalReference,
  nowStamp,
} from '../asset-disposal/asset-disposal';
import { CURRENT_USER, EMPLOYEES, isoDate } from '../../../../../assets/asset-assignment/asset-assignment-data';

/** DEMO MODE: asset register shown in the Select Assets step until the Asset API serves it. */
interface CandidateAsset extends DisposalItem {
  locationDetail: string;
  manufacturer: string;
  status: AssetStatus;
}

type AssetStatus = 'Active' | 'Inactive' | 'In Store';
type Candidate = [string, string, string, string, string, string, string, string, string, number];

const LOCATION_DETAIL: Record<string, string> = {
  'Head Office': 'Head Office - IT Department',
  'Block A': 'Block A - Control Room',
  'Block B': 'Block B - Design Studio',
  'Data Centre': 'Data Centre - Rack 4',
  'Training Room': 'Training Room - Hall 1',
  'Main Building': 'Main Building - 2nd Floor',
  'Finance Dept': 'Finance Dept - Ground Floor',
  Warehouse: 'Warehouse - Store 1',
};

// name, tag, category, model, serial, location, manufacturer, condition, status, value (AED)
const DESIGN_ROWS: Candidate[] = [
  ['Laptop', 'LT-000125', 'IT Equipment', 'Latitude 7420', '5CD1234ABC', 'Head Office', 'Dell Technologies', 'End of Life', 'Active', 2000],
  ['Laptop', 'LT-000126', 'IT Equipment', 'Latitude 7420', '5CD1234ABD', 'Head Office', 'Dell Technologies', 'End of Life', 'Active', 2000],
  ['Monitor', 'MN-000089', 'IT Equipment', 'UltraSharp 27', '8FG5678XYZ', 'Block A', 'Dell Technologies', 'Good', 'Active', 600],
  ['Desktop PC', 'PC-000334', 'IT Equipment', 'OptiPlex 7090', '9HJ6789KLM', 'Data Centre', 'Dell Technologies', 'End of Life', 'Active', 900],
  ['Projector', 'PR-000089', 'Audio Visual', 'EB-L530U', 'PR12345678', 'Training Room', 'Epson', 'End of Life', 'Active', 2500],
  ['Server', 'SR-000012', 'IT Equipment', 'PowerEdge R740', 'SR98765432', 'Data Centre', 'Dell Technologies', 'Under Repair', 'Active', 6500],
  ['Office Chair', 'CH-000156', 'Furniture', 'Ergonomic Pro', 'CH55667788', 'Main Building', 'Herman Miller', 'End of Life', 'Active', 1700],
  ['Air Conditioner', 'AC-000034', 'HVAC', 'Split AC 2.0T', 'AC33445566', 'Block A', 'Daikin', 'Good', 'Active', 1500],
  ['Network Switch', 'SW-000078', 'Networking', 'Catalyst 9300', 'SW77889900', 'Data Centre', 'Cisco', 'End of Life', 'Active', 1200],
  ['Printer', 'PRN-000221', 'IT Equipment', 'LaserJet Pro', 'PR11223344', 'Finance Dept', 'HP', 'Good', 'Active', 400],
];

// Extra rows so filters, sorting and paging have something to work with
const TEMPLATES: [string, string, string, string, string, number][] = [
  ['LT', 'Laptop', 'IT Equipment', 'Latitude 7420', 'Dell Technologies', 1800],
  ['MN', 'Monitor', 'IT Equipment', 'P2419H', 'Dell Technologies', 350],
  ['PC', 'Desktop PC', 'IT Equipment', 'OptiPlex 7070', 'Dell Technologies', 700],
  ['UPS', 'UPS', 'Electrical', 'Smart-UPS 3000', 'APC', 900],
  ['TB', 'Meeting Table', 'Furniture', 'Boardroom 2400', 'Steelcase', 800],
  ['AC', 'Air Conditioner', 'HVAC', 'FTKF50', 'Daikin', 1200],
  ['SW', 'Access Switch', 'Networking', 'Catalyst 2960', 'Cisco', 500],
  ['PR', 'Projector', 'Audio Visual', 'EB-X51', 'Epson', 900],
];
const GEN_CONDITIONS = ['Good', 'Fair', 'End of Life', 'Damaged', 'Obsolete', 'Under Repair'];
const GEN_STATUSES: AssetStatus[] = ['Active', 'Active', 'Active', 'In Store', 'Inactive'];
const GEN_LOCATIONS = Object.keys(LOCATION_DETAIL);

function generated(count: number): Candidate[] {
  return Array.from({ length: count }, (_, i) => {
    const [prefix, name, category, model, maker, value] = TEMPLATES[i % TEMPLATES.length];
    const tag = `${prefix}-${String(400 + i).padStart(6, '0')}`;
    return [name, tag, category, model, `SN${(73519 * (i + 3)).toString(36).toUpperCase().padStart(8, '0')}`, GEN_LOCATIONS[i % GEN_LOCATIONS.length],
      maker, GEN_CONDITIONS[i % GEN_CONDITIONS.length], GEN_STATUSES[i % GEN_STATUSES.length], value - (i % 5) * 50];
  });
}

const POOL: CandidateAsset[] = [...DESIGN_ROWS, ...generated(58)].map(
  ([assetName, tag, category, model, serial, location, manufacturer, condition, status, value]) => ({
    assetName, tag, category, model, serial, location, locationDetail: LOCATION_DETAIL[location] ?? location, manufacturer, condition,
    status: status as AssetStatus, value,
  }),
);

const CONDITIONS = [...new Set(POOL.map((a) => a.condition))];
const ASSET_STATUSES: AssetStatus[] = ['Active', 'Inactive', 'In Store'];
const MANUFACTURERS = [...new Set(POOL.map((a) => a.manufacturer))].sort();

type SortKey = 'assetName' | 'tag' | 'category' | 'serial';

const PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'] as const;
type Priority = (typeof PRIORITIES)[number];

interface Vendor {
  name: string;
  /** Methods the vendor handles; its contact fills the form when picked. */
  methods: DisposalMethod[];
  address: string;
  license: string;
  contact: string;
  phone: string;
}

// DEMO MODE: vendor register until the Vendor API serves it
const VENDORS: Vendor[] = [
  { name: 'Green Recycle LLC', methods: ['Recycle'], address: 'P.O. Box 12345, Dubai, UAE', license: 'TRN-123456789', contact: 'Mohammed Al Hassan', phone: '+971 50 123 4567' },
  { name: 'EcoTech Disposal', methods: ['Recycle', 'Scrap'], address: 'Industrial Area 4, Sharjah, UAE', license: 'TRN-234567891', contact: 'Priya Nair', phone: '+971 55 234 5678' },
  { name: 'Gulf Scrap Traders', methods: ['Scrap', 'Sell', 'Auction'], address: 'Al Quoz Industrial 3, Dubai, UAE', license: 'TRN-345678912', contact: 'Imran Qureshi', phone: '+971 52 345 6789' },
  { name: 'Dell Asset Recovery', methods: ['Return to Vendor'], address: 'Dubai Internet City, Dubai, UAE', license: 'TRN-456789123', contact: 'Sarah Thompson', phone: '+971 4 456 7890' },
  { name: 'Emirates Charity Foundation', methods: ['Donate'], address: 'Al Barsha 1, Dubai, UAE', license: 'TRN-567891234', contact: 'Fatima Al Mansoori', phone: '+971 56 567 8901' },
  { name: 'Emirates Auction House', methods: ['Sell', 'Auction'], address: 'Al Aweer, Dubai, UAE', license: 'TRN-678912345', contact: 'Khalid Rahman', phone: '+971 50 678 9012' },
];

/** Method cards on the Disposal Method step; Sell and Auction stay in the dropdown. */
const METHOD_CARDS: { method: DisposalMethod; text: string; tone: string }[] = [
  { method: 'Recycle', text: 'Send to certified recycler for environmentally responsible disposal.', tone: 'green' },
  { method: 'Scrap', text: 'Dispose as scrap (material recovery).', tone: 'red' },
  { method: 'Return to Vendor', text: 'Return to original vendor or supplier.', tone: 'blue' },
  { method: 'Donate', text: 'Donate to approved organization.', tone: 'pink' },
];

/** Supporting information shown beside the Disposal Method step. */
const METHOD_NOTES: Record<DisposalMethod, string> = {
  Recycle: 'Assets will be processed as per company e-waste policy. A certificate of recycling will be obtained from the vendor.',
  Scrap: 'Assets will be stripped of reusable parts and data before scrapping. A scrap receipt will be obtained from the vendor.',
  'Return to Vendor': 'Assets will be returned under the purchase or lease agreement. A return acknowledgement will be obtained from the vendor.',
  Donate: 'Assets will be wiped and handed over to the approved organization. A donation receipt will be obtained.',
  Sell: 'Assets will be sold at or above the estimated value. A sale invoice will be obtained from the buyer.',
  Auction: 'Assets will be listed with the auction house. The final bid and settlement will be recorded against this request.',
};

type ApprovalType = 'Mandatory' | 'Optional';

interface Approver {
  name: string;
  role: string;
  /** Avatar colour */
  color: string;
}

interface ApprovalStep {
  level: string;
  approver: string;
  type: ApprovalType;
}

// DEMO MODE: approver directory until the Users API serves it
const APPROVERS: Approver[] = [
  { name: 'Ahmed Salim', role: 'IT Manager', color: '#6b7cc9' },
  { name: 'Nisha Patel', role: 'Head - IT Department', color: '#7c6ee6' },
  { name: 'Ramesh Kumar', role: 'Finance Department', color: '#6d28d9' },
  { name: 'Mohammed Al Hassan', role: 'Operations Director', color: '#0e7490' },
  { name: 'Fatima Hassan', role: 'Finance Manager', color: '#be185d' },
  { name: 'Arjun Mehta', role: 'Asset Manager', color: '#15803d' },
  { name: 'Sara Ahmed', role: 'Procurement Lead', color: '#c2410c' },
];

/** Default approval chain for a new request. */
const DEFAULT_CHAIN: ApprovalStep[] = [
  { level: 'Line Manager', approver: 'Ahmed Salim', type: 'Mandatory' },
  { level: 'Department Head', approver: 'Nisha Patel', type: 'Mandatory' },
  { level: 'Finance Approval', approver: 'Ramesh Kumar', type: 'Mandatory' },
  { level: 'Final Approval', approver: 'Mohammed Al Hassan', type: 'Mandatory' },
];

const STEPS = [
  { title: 'Request Details', sub: 'Basic information' },
  { title: 'Select Assets', sub: 'Assets to dispose' },
  { title: 'Disposal Method', sub: 'Method & vendor' },
  { title: 'Approval & Attachments', sub: 'Approvers & documents' },
  { title: 'Review & Submit', sub: 'Verify & submit' },
];

interface PendingFile {
  name: string;
  size: number;
  added: string;
  by: string;
  description: string;
}

@Component({
  selector: 'app-new-disposal',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './new-disposal.html',
  styleUrls: [
    '../../../../../assets/asset-assignment/asset-assignment.css',
    '../../../../../assets/asset-assignment/asset-assignment-table.css',
    '../../../../../assets/asset-assignment/assign-asset/assign-asset.css',
    './new-disposal.css',
  ],
})
export class NewDisposal implements OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, ArrowUpDown, Building2, CalendarDays, Check, ChevronLeft, ChevronRight, CloudUpload, EllipsisVertical, FileSpreadsheet,
    FileText, Funnel, GripVertical, HandHeart, Hexagon, Image, Info, MapPin, Package, Paperclip, Pencil, Plus, Recycle, RefreshCw, Save, Search, Send, ShieldCheck, Tag, Trash2, Upload, X,
  };
  readonly steps = STEPS;
  readonly reasons = REASONS;
  readonly categories = CATEGORIES;
  readonly priorities = PRIORITIES;
  readonly methods = DISPOSAL_METHODS;
  readonly methodCards = METHOD_CARDS;
  readonly methodIcons: Record<DisposalMethod, typeof Recycle> = {
    Recycle, Scrap: Trash2, 'Return to Vendor': RefreshCw, Donate: HandHeart, Sell: Tag, Auction: Tag,
  };
  readonly approverChoices = APPROVERS;
  readonly approvalTypes: ApprovalType[] = ['Mandatory', 'Optional'];
  readonly people = EMPLOYEES.map((e) => e.name).concat(EMPLOYEES.some((e) => e.name === CURRENT_USER) ? [] : [CURRENT_USER]);
  readonly pageSizes = [10, 25, 50];

  private readonly router = inject(Router);

  step = 1;

  // 1. Request details
  title = '';
  requestDate = isoDate(new Date());
  requestedBy = CURRENT_USER;
  reason = REASONS[0];
  category = '';
  priority: Priority = 'Normal';
  justification = '';

  // 2. Assets: the register, filtered and sorted, and the tags picked for disposal
  included = new Set<string>();
  assetSearch = '';
  categoryFilter = '';
  locationFilter = '';
  conditionFilter = '';
  statusFilter: AssetStatus | '' = 'Active';
  manufacturerFilter = '';
  minValue: number | null = null;
  maxValue: number | null = null;
  moreFilters = false;
  sortKey: SortKey | '' = '';
  sortDir: 1 | -1 = 1;
  listTab: 'all' | 'selected' = 'all';
  pageSize = 10;
  page = 0;

  // Add by Tag dialog
  tagDialog = false;
  tagText = '';

  // 3. Method
  method: DisposalMethod = 'Recycle';
  vendor = VENDORS[0].name;
  contactPerson = VENDORS[0].contact;
  contactNumber = VENDORS[0].phone;
  expectedDate = '';
  instructions = '';

  // 4. Approval & attachments
  approvals: ApprovalStep[] = DEFAULT_CHAIN.map((a) => ({ ...a }));
  notifyApprovers = true;
  /** Row being dragged in the approval table */
  dragRow: number | null = null;
  files: PendingFile[] = [];
  dragging = false;
  remarks = '';

  error = '';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Steps =====

  /** Reason the step can't be left yet, or '' when it is complete. */
  stepError(step: number): string {
    switch (step) {
      case 1:
        if (!this.title.trim()) return 'Enter the request title.';
        if (!this.requestDate) return 'Choose the request date.';
        if (!this.reason) return 'Choose the reason for disposal.';
        if (!this.justification.trim()) return 'Enter the justification / remarks.';
        return '';
      case 2:
        return this.chosen.length ? '' : 'Select at least one asset.';
      case 3:
        if (!this.method) return 'Choose the disposal method.';
        if (!this.vendor) return 'Choose the vendor.';
        if (!this.expectedDate) return 'Choose the expected disposal date.';
        if (this.expectedDate && this.expectedDate < this.requestDate) return 'Expected disposal date can\'t be before the request date.';
        return '';
      case 4:
        if (!this.approvals.length) return 'Add at least one approver.';
        if (this.approvals.some((a) => !a.approver)) return 'Choose an approver for every approval level.';
        if (this.approvals.some((a) => !a.level.trim())) return 'Name every approval level.';
        return '';
      default:
        return '';
    }
  }

  /** Stepper subtitle: what was entered once a step is passed, otherwise its hint. */
  stepSub(i: number): string {
    if (this.step <= i + 1) return STEPS[i].sub;
    switch (i) {
      case 0: return this.title || STEPS[i].sub;
      case 1: return `${this.chosen.length} asset${this.chosen.length === 1 ? '' : 's'}`;
      case 2: return this.method;
      case 3: return `${this.approvals.length} approver${this.approvals.length === 1 ? '' : 's'}`;
      default: return STEPS[i].sub;
    }
  }

  goStep(step: number): void {
    // Moving forward checks every step on the way
    for (let s = this.step; s < step; s++) {
      const err = this.stepError(s);
      if (err) {
        this.step = s;
        this.error = err;
        return;
      }
    }
    this.error = '';
    this.step = step;
  }

  /** Stepper click: open any step directly; Next and Submit still validate. */
  openStep(step: number): void {
    this.error = '';
    this.step = step;
  }

  next(): void {
    if (this.step < STEPS.length) this.goStep(this.step + 1);
  }

  back(): void {
    if (this.step === 1) return void this.router.navigate(['/assets/disposal']);
    this.error = '';
    this.step--;
  }

  // ===== Method =====

  /** Vendors that handle the chosen method. */
  get vendors(): Vendor[] {
    return VENDORS.filter((v) => v.methods.includes(this.method));
  }

  get vendorDetails(): Vendor | null {
    return VENDORS.find((v) => v.name === this.vendor) ?? null;
  }

  methodTone(m: DisposalMethod): string {
    return METHOD_CARDS.find((c) => c.method === m)?.tone ?? 'violet';
  }

  /** One-line description of a method, from its card or its supporting note. */
  methodText(m: DisposalMethod): string {
    return METHOD_CARDS.find((c) => c.method === m)?.text ?? METHOD_NOTES[m];
  }

  get methodNote(): string {
    return METHOD_NOTES[this.method];
  }

  setMethod(m: DisposalMethod): void {
    this.method = m;
    // Keep the vendor only when it handles the new method
    if (!this.vendors.some((v) => v.name === this.vendor)) this.setVendor(this.vendors[0]?.name ?? '');
  }

  setVendor(name: string): void {
    this.vendor = name;
    const v = this.vendorDetails;
    this.contactPerson = v?.contact ?? '';
    this.contactNumber = v?.phone ?? '';
  }

  // ===== Assets =====

  readonly locations = GEN_LOCATIONS;
  readonly conditions = CONDITIONS;
  readonly assetStatuses = ASSET_STATUSES;
  readonly manufacturers = MANUFACTURERS;

  get chosen(): CandidateAsset[] {
    return POOL.filter((a) => this.included.has(a.tag));
  }

  get filteredAssets(): CandidateAsset[] {
    const q = this.assetSearch.trim().toLowerCase();
    const rows = (this.listTab === 'selected' ? this.chosen : POOL).filter(
      (a) =>
        (!this.categoryFilter || a.category === this.categoryFilter) &&
        (!this.locationFilter || a.location === this.locationFilter) &&
        (!this.conditionFilter || a.condition === this.conditionFilter) &&
        (!this.statusFilter || a.status === this.statusFilter) &&
        (!this.manufacturerFilter || a.manufacturer === this.manufacturerFilter) &&
        (this.minValue == null || a.value >= this.minValue) &&
        (this.maxValue == null || a.value <= this.maxValue) &&
        (!q || `${a.assetName} ${a.tag} ${a.serial} ${a.model}`.toLowerCase().includes(q)),
    );
    const key = this.sortKey;
    return key ? [...rows].sort((x, y) => x[key].localeCompare(y[key]) * this.sortDir) : rows;
  }

  get pageRows(): CandidateAsset[] {
    return this.filteredAssets.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filteredAssets.length / this.pageSize));
  }

  /** Page buttons with gaps (null) for long lists: 1 2 3 … 27 */
  get pages(): (number | null)[] {
    const n = this.pageCount;
    if (n <= 7) return Array.from({ length: n }, (_, i) => i);
    const keep = new Set([0, n - 1, this.page - 1, this.page, this.page + 1, ...(this.page < 3 ? [1, 2] : []), ...(this.page > n - 4 ? [n - 2, n - 3] : [])]);
    const out: (number | null)[] = [];
    for (let i = 0; i < n; i++) {
      if (keep.has(i)) out.push(i);
      else if (out[out.length - 1] !== null) out.push(null);
    }
    return out;
  }

  get showingLabel(): string {
    const n = this.filteredAssets.length;
    if (!n) return 'No records';
    return `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records`;
  }

  get allPageIncluded(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((a) => this.included.has(a.tag));
  }

  togglePage(on: boolean): void {
    for (const a of this.pageRows) {
      if (on) this.select(a.tag);
      else this.included.delete(a.tag);
    }
  }

  toggleIncluded(tag: string): void {
    if (!this.included.delete(tag)) this.select(tag);
  }

  private select(tag: string): void {
    this.included.add(tag);
    // The first asset picked sets the category when none was chosen
    if (!this.category) this.category = POOL.find((a) => a.tag === tag)?.category ?? '';
  }

  onFilter(): void {
    this.page = 0;
  }

  resetFilters(): void {
    this.assetSearch = this.categoryFilter = this.locationFilter = this.conditionFilter = this.manufacturerFilter = '';
    this.statusFilter = 'Active';
    this.minValue = this.maxValue = null;
    this.onFilter();
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortDir = this.sortDir === 1 ? -1 : 1;
    else {
      this.sortKey = key;
      this.sortDir = 1;
    }
  }

  setListTab(tab: 'all' | 'selected'): void {
    this.listTab = tab;
    this.onFilter();
  }

  clearAssets(): void {
    this.included.clear();
    this.onFilter();
  }

  removeAsset(a: CandidateAsset): void {
    this.included.delete(a.tag);
    this.page = Math.min(this.page, this.pageCount - 1);
  }

  /** Selects the register assets whose tags are listed and reports any that weren't found. */
  private selectTags(tags: string[]): void {
    const wanted = new Set(tags.map((t) => t.trim().toUpperCase()).filter(Boolean));
    const found = POOL.filter((a) => wanted.has(a.tag));
    found.forEach((a) => this.select(a.tag));
    const missing = wanted.size - found.length;
    this.showToast(`${found.length} asset${found.length === 1 ? '' : 's'} selected${missing ? `, ${missing} tag${missing === 1 ? '' : 's'} not found` : ''}.`);
  }

  // Add by Tag dialog

  openTagDialog(): void {
    this.tagText = '';
    this.tagDialog = true;
  }

  confirmTags(): void {
    this.selectTags(this.tagText.split(/[\s,;]+/));
    this.tagDialog = false;
  }

  /** Reads asset tags from the column named "Asset Tag" (or the first column) of the sheet. */
  importExcel(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    file.arrayBuffer().then((buf) => {
      const book = XLSX.read(buf);
      const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(book.Sheets[book.SheetNames[0]]);
      this.selectTags(rows.map((r) => String(r['Asset Tag'] ?? r['Tag'] ?? Object.values(r)[0] ?? '')));
    });
  }

  // ===== Approvers =====

  approverOf(name: string): Approver | null {
    return APPROVERS.find((a) => a.name === name) ?? null;
  }

  initials(name: string): string {
    return name.split(' ').filter(Boolean).map((w) => w[0]).slice(0, 2).join('').toUpperCase();
  }

  /** Approvers not already used on another level, plus the row's own. */
  approverOptions(row: ApprovalStep): Approver[] {
    return APPROVERS.filter((a) => a.name === row.approver || !this.approvals.some((r) => r.approver === a.name));
  }

  addApprover(): void {
    const free = APPROVERS.find((a) => !this.approvals.some((r) => r.approver === a.name));
    this.approvals.push({ level: `Approval Level ${this.approvals.length + 1}`, approver: free?.name ?? '', type: 'Mandatory' });
  }

  removeApprover(i: number): void {
    this.approvals.splice(i, 1);
  }

  moveApprover(from: number, to: number): void {
    if (to < 0 || to >= this.approvals.length || from === to) return;
    const [row] = this.approvals.splice(from, 1);
    this.approvals.splice(to, 0, row);
  }

  onRowDrop(event: DragEvent, to: number): void {
    event.preventDefault();
    if (this.dragRow !== null) this.moveApprover(this.dragRow, to);
    this.dragRow = null;
  }

  // ===== Attachments =====

  addFiles(list: FileList | null | undefined): void {
    const tooBig = Array.from(list ?? []).filter((f) => f.size > 10 * 1024 * 1024);
    for (const f of Array.from(list ?? [])) {
      if (f.size <= 10 * 1024 * 1024) this.files.push({ name: f.name, size: f.size, added: nowStamp(), by: CURRENT_USER, description: '' });
    }
    if (tooBig.length) this.showToast(`${tooBig.map((f) => f.name).join(', ')} is over 10 MB and was skipped.`);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging = false;
    this.addFiles(event.dataTransfer?.files);
  }

  removeFile(f: PendingFile): void {
    this.files = this.files.filter((x) => x !== f);
  }

  fileSize(bytes: number): string {
    if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  ext(name: string): string {
    return name.split('.').pop()?.toLowerCase() ?? '';
  }

  fileIcon(name: string) {
    const e = this.ext(name);
    if (e === 'pdf' || e === 'doc' || e === 'docx') return FileText;
    if (e === 'xls' || e === 'xlsx' || e === 'csv') return FileSpreadsheet;
    return Image;
  }

  // ===== Summary =====

  /** Asset shown in the side card: the first one included. */
  get featured(): CandidateAsset | null {
    return this.chosen[0] ?? null;
  }

  imageOf(a: CandidateAsset): string | null {
    return DISPOSAL_IMAGES[a.category] ?? null;
  }

  get totalValue(): number {
    return this.chosen.reduce((n, a) => n + a.value, 0);
  }

  conditionClass(c: string): string {
    if (/end of life|damaged|under repair/i.test(c)) return 'cond cond--bad';
    return /good/i.test(c) ? 'cond cond--good' : 'cond cond--warn';
  }

  // ===== Save =====

  saveDraft(): void {
    if (!this.title.trim()) {
      this.step = 1;
      this.error = 'Enter the request title before saving a draft.';
      return;
    }
    this.save('Draft');
  }

  submit(): void {
    for (let s = 1; s < STEPS.length; s++) {
      const err = this.stepError(s);
      if (err) {
        this.step = s;
        this.error = err;
        return;
      }
    }
    this.save('Pending');
  }

  private save(status: DisposalStatus): void {
    const first = this.chosen[0];
    const items: DisposalItem[] = this.chosen.map(({ assetName, tag, category, model, serial, location, condition, value }) => ({
      assetName, tag, category, model, serial, location, condition, value,
    }));
    const created: DisposalRequest = {
      id: DISPOSALS.reduce((n, r) => Math.max(n, r.id), 0) + 1,
      reference: nextDisposalReference(DISPOSALS),
      assetName: this.title.trim(),
      tag: first ? first.tag + (items.length > 1 ? ` +${items.length - 1}` : '') : '-',
      category: this.category || first?.category || '-',
      location: first?.location ?? '-',
      locationDetail: first?.locationDetail ?? '-',
      method: this.method,
      status,
      requested: `${this.requestDate}T${nowStamp().slice(11)}`,
      requestedBy: this.requestedBy,
      reason: this.reason,
      serial: first?.serial ?? '',
      model: first?.model ?? '',
      manufacturer: first?.manufacturer ?? '',
      value: this.totalValue,
      attachments: this.files.map(({ name, size, added }) => ({ name, size, added })),
      notes: [this.justification, this.instructions, this.remarks]
        .map((t) => t.trim())
        .filter(Boolean)
        .map((text) => ({ text, by: this.requestedBy, at: nowStamp() })),
      items,
    };
    DISPOSALS.unshift(created);
    this.router.navigate(['/assets/disposal']);
  }

  // ===== Keys =====

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.tagDialog = false;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
