import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Observable, concatMap, defer, from, last, map, of, tap } from 'rxjs';
import { AuthService } from 'shared-ui';
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
import { DISPOSAL_IMAGES, DISPOSAL_METHODS, DisposalItem, DisposalMethod, REASONS, nowStamp } from '../asset-disposal/asset-disposal';
import { isoDate } from '../../asset-assignment/asset-assignment-data';
import {
  AssetDisposalService,
  AssetRegistrationListItemDto,
  DisposalAddAssetsResult,
  DisposalApprovalBody,
  DisposalDetailsBody,
  DisposalMethodBody,
  DisposalRequestDto,
  dateOnly,
} from '../asset-disposal.service';
import { apiErrorMessage } from '../../services/asset-api';

/** A registered asset offered in the Select Assets step (GET /api/asset-registrations). */
interface CandidateAsset extends DisposalItem {
  /** Asset registration id sent to the Asset API. */
  id: string;
  locationDetail: string;
  manufacturer: string;
  status: string;
}

/** Condition filter values; the asset list endpoint filters on condition server-side. */
const CONDITIONS = ['New', 'Good', 'Fair', 'Poor', 'Damaged', 'End of Life', 'Obsolete', 'Under Repair'];

/** Candidate row from an asset list item. */
function fromAssetItem(a: AssetRegistrationListItemDto, condition = ''): CandidateAsset {
  const location = a.siteName || a.buildingName || a.locationPath || '';
  const detail = a.locationPath || [a.siteName, a.buildingName, a.floorName, a.zoneName, a.roomName].filter(Boolean).join(' - ');
  return {
    id: a.assetRegistrationId,
    assetName: a.assetName ?? a.assetRegistrationId,
    tag: a.assetTag || a.assetRegistrationId,
    category: a.category ?? '',
    model: a.assetType ?? '',
    serial: '',
    location,
    locationDetail: detail || location,
    manufacturer: a.manufacturer ?? '',
    condition,
    status: a.status ?? '',
    value: 0,
  };
}

type SortKey = 'assetName' | 'tag' | 'category' | 'serial';

const PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'];
type Priority = string;

interface Vendor {
  name: string;
  /** Methods the vendor handles; its contact fills the form when picked. */
  methods: DisposalMethod[];
  address: string;
  license: string;
  contact: string;
  phone: string;
}

/** Vendor register; empty until the Vendor API serves it (vendors saved on a loaded request are added). */
const VENDORS: Vendor[] = [];

/** Method cards on the Disposal Method step; Sell / Auction stays in the dropdown. */
const METHOD_CARDS: { method: DisposalMethod; text: string; tone: string }[] = [
  { method: 'Recycle', text: 'Send to certified recycler for environmentally responsible disposal.', tone: 'green' },
  { method: 'Scrap', text: 'Dispose as scrap (material recovery).', tone: 'red' },
  { method: 'Return to Vendor', text: 'Return to original vendor or supplier.', tone: 'blue' },
  { method: 'Donate', text: 'Donate to approved organization.', tone: 'pink' },
];

/** Supporting information shown beside the Disposal Method step. */
const METHOD_NOTES: Record<string, string> = {
  Recycle: 'Assets will be processed as per company e-waste policy. A certificate of recycling will be obtained from the vendor.',
  Scrap: 'Assets will be stripped of reusable parts and data before scrapping. A scrap receipt will be obtained from the vendor.',
  'Return to Vendor': 'Assets will be returned under the purchase or lease agreement. A return acknowledgement will be obtained from the vendor.',
  Donate: 'Assets will be wiped and handed over to the approved organization. A donation receipt will be obtained.',
  'Sell / Auction': 'Assets will be sold or listed with the auction house at or above the estimated value. A sale invoice will be obtained from the buyer.',
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

/** Approver directory; empty until the Users API serves it (approvers on a loaded request are added). */
const APPROVERS: Approver[] = [];

/** Default approval chain for a new request. */
const DEFAULT_CHAIN: ApprovalStep[] = [
  { level: 'Line Manager', approver: '', type: 'Mandatory' },
  { level: 'Department Head', approver: '', type: 'Mandatory' },
  { level: 'Finance Approval', approver: '', type: 'Mandatory' },
  { level: 'Final Approval', approver: '', type: 'Mandatory' },
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
  /** Media URL once the file is stored on the request; files without one are not sent to the API. */
  url?: string;
}

@Component({
  selector: 'app-new-disposal',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './new-disposal.html',
  styleUrls: [
    '../../asset-assignment/asset-assignment.css',
    '../../asset-assignment/asset-assignment-table.css',
    '../../asset-assignment/assign-asset/assign-asset.css',
    './new-disposal.css',
  ],
})
export class NewDisposal implements OnInit, OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, ArrowUpDown, Building2, CalendarDays, Check, ChevronLeft, ChevronRight, CloudUpload, EllipsisVertical, FileSpreadsheet,
    FileText, Funnel, GripVertical, HandHeart, Hexagon, Image, Info, MapPin, Package, Paperclip, Pencil, Plus, Recycle, RefreshCw, Save, Search, Send, ShieldCheck, Tag, Trash2, Upload, X,
  };
  readonly steps = STEPS;
  reasons: string[] = REASONS;
  priorities: string[] = [...PRIORITIES];
  methods: DisposalMethod[] = DISPOSAL_METHODS;
  readonly methodCards = METHOD_CARDS;
  readonly methodIcons: Record<string, typeof Recycle> = {
    Recycle, Scrap: Trash2, 'Return to Vendor': RefreshCw, Donate: HandHeart, 'Sell / Auction': Tag, Sell: Tag, Auction: Tag,
  };
  /** Approver directory (anyone already on a loaded request). */
  approverChoices: Approver[] = [...APPROVERS];
  readonly approvalTypes: ApprovalType[] = ['Mandatory', 'Optional'];
  people: string[] = [];
  readonly pageSizes = [10, 25, 50];

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(AssetDisposalService);
  private readonly auth = inject(AuthService);

  step = 1;

  /** Asset API id of the request; set when step 1 is first saved, or from ?id= when editing. */
  requestId: string | null = null;
  requestNumber = '';
  saving = false;
  loadingRequest = false;

  // 1. Request details
  title = '';
  requestDate = isoDate(new Date());
  requestedBy = '';
  reason = REASONS[0];
  category = '';
  priority: Priority = 'Normal';
  justification = '';

  // 2. Assets: the register, filtered and sorted, and the tags picked for disposal
  /** Registered assets from GET /api/asset-registrations. */
  pool: CandidateAsset[] = [];
  loadingAssets = false;
  assetsError = '';
  private loadedCondition = '';
  included = new Set<string>();
  /** Picked assets by tag, kept even when they drop out of the loaded list. */
  private picked = new Map<string, CandidateAsset>();
  assetSearch = '';
  categoryFilter = '';
  locationFilter = '';
  conditionFilter = '';
  statusFilter = 'Active';
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
  /** Vendor register plus vendors already saved on a loaded request. */
  vendorList: Vendor[] = [...VENDORS];
  vendor = '';
  contactPerson = '';
  contactNumber = '';
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

  ngOnInit(): void {
    const user = this.auth.getUser()?.userName;
    if (user) {
      this.requestedBy = user;
      if (!this.people.includes(user)) this.people = [...this.people, user];
    }
    this.api.options().subscribe({
      next: (o) => {
        if (o?.reasons?.length) this.reasons = o.reasons;
        if (o?.priorities?.length) this.priorities = o.priorities;
        if (o?.methods?.length) this.methods = o.methods.map((m) => m.method);
      },
      error: () => undefined,
    });
    this.loadAssets();
    const id = this.route.snapshot.queryParamMap.get('id');
    if (id) this.loadRequest(id);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Load =====

  /** Edit mode: fills every step from GET /api/asset-disposals/{id}. */
  private loadRequest(id: string): void {
    this.loadingRequest = true;
    this.api.get(id).subscribe({
      next: (d) => {
        this.loadingRequest = false;
        this.fill(d);
      },
      error: (err) => {
        this.loadingRequest = false;
        this.error = apiErrorMessage(err, 'Could not load the disposal request.');
      },
    });
  }

  private fill(d: DisposalRequestDto): void {
    this.requestId = d.id;
    this.requestNumber = d.requestNumber;
    this.title = d.requestTitle ?? '';
    this.requestDate = dateOnly(d.requestDate) || this.requestDate;
    if (d.requestedBy?.name) {
      this.requestedBy = d.requestedBy.name;
      if (!this.people.includes(d.requestedBy.name)) this.people = [...this.people, d.requestedBy.name];
    }
    this.reason = d.reason ?? '';
    this.category = d.disposalCategory ?? '';
    this.priority = d.priority || 'Normal';
    this.justification = d.justification ?? '';
    this.syncAssets(d);

    const m = d.method ?? {};
    if (m.method) this.method = m.method;
    this.expectedDate = dateOnly(m.expectedDisposalDate);
    if (m.vendor?.name) {
      const name = m.vendor.name;
      const known = this.vendorList.find((v) => v.name === name);
      if (known) {
        if (!known.methods.includes(this.method)) known.methods = [...known.methods, this.method];
      } else {
        this.vendorList = [...this.vendorList, {
          name, methods: [this.method], address: m.vendor.address ?? '', license: m.vendor.tradeLicense ?? '', contact: m.contactPerson ?? '', phone: m.contactNumber ?? '',
        }];
      }
      this.vendor = name;
    } else {
      this.vendor = '';
    }
    this.contactPerson = m.contactPerson ?? '';
    this.contactNumber = m.contactNumber ?? '';
    this.instructions = m.instructions ?? '';

    if (d.approvers?.length) {
      this.approvals = [...d.approvers].sort((a, b) => a.level - b.level).map((a) => {
        if (a.approver?.name && !this.approverChoices.some((p) => p.name === a.approver.name)) {
          this.approverChoices = [...this.approverChoices, { name: a.approver.name, role: a.approver.roleDepartment ?? '', color: '#6b7cc9' }];
        }
        return { level: a.approvalLevel, approver: a.approver?.name ?? '', type: a.approvalType === 'Optional' ? 'Optional' : 'Mandatory' };
      });
    }
    this.notifyApprovers = d.notifyApprovers ?? true;
    this.files = (d.attachments ?? []).map((f) => ({
      name: f.fileName, size: f.fileSize ?? 0, added: f.uploadedAt ?? '', by: f.uploadedBy ?? '', description: f.description ?? '', url: f.fileUrl,
    }));
    this.remarks = d.remarks ?? '';
  }

  /** Replaces the picked assets with the ones saved on the request (the API fills model, serial and condition). */
  private syncAssets(d: DisposalRequestDto): void {
    this.picked.clear();
    this.included.clear();
    for (const x of d.assets ?? []) {
      const tag = x.assetTag || x.assetRegistrationId;
      const known = this.pool.find((a) => a.id === x.assetRegistrationId);
      this.picked.set(tag, {
        id: x.assetRegistrationId,
        assetName: x.assetName ?? known?.assetName ?? tag,
        tag,
        category: x.category ?? known?.category ?? '',
        model: x.model ?? known?.model ?? '',
        serial: x.serialNumber ?? '',
        location: x.location ?? known?.location ?? '',
        locationDetail: [x.location, x.department].filter(Boolean).join(' - ') || known?.locationDetail || '',
        manufacturer: x.manufacturer ?? known?.manufacturer ?? '',
        condition: x.condition ?? '',
        status: x.status ?? known?.status ?? '',
        value: x.estimatedValue ?? 0,
      });
      this.included.add(tag);
    }
    if (!this.category) this.category = this.chosen[0]?.category ?? '';
  }

  /** Registered assets for the picker; the condition filter is applied by the API. */
  loadAssets(): void {
    this.loadingAssets = true;
    this.assetsError = '';
    const condition = this.conditionFilter;
    this.api.listAssets({ condition: condition || undefined, pageSize: 200 }).subscribe({
      next: (items) => {
        this.loadingAssets = false;
        this.loadedCondition = condition;
        this.pool = items.map((a) => fromAssetItem(a, condition));
      },
      error: (err) => {
        this.loadingAssets = false;
        this.assetsError = apiErrorMessage(err, 'Could not load the asset list.');
        this.showToast(this.assetsError);
      },
    });
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
        if (!this.approvals.some((a) => a.type === 'Mandatory')) return 'At least one approval level must be Mandatory.';
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

  /** Stepper click: open any step directly; Next and Submit still validate and save. */
  openStep(step: number): void {
    this.error = '';
    this.step = step;
  }

  /** Validates and saves the current step (creating the draft on step 1), then moves on. */
  next(): void {
    if (this.step >= STEPS.length || this.saving) return;
    const err = this.stepError(this.step);
    if (err) return void (this.error = err);
    this.error = '';
    const from = this.step;
    this.run(this.saveStep(from, false), () => (this.step = from + 1));
  }

  back(): void {
    if (this.step === 1) return void this.router.navigate(['/assets/disposal']);
    this.error = '';
    this.step--;
  }

  // ===== Method =====

  /** Vendors that handle the chosen method. */
  get vendors(): Vendor[] {
    return this.vendorList.filter((v) => v.methods.includes(this.method));
  }

  get vendorDetails(): Vendor | null {
    return this.vendorList.find((v) => v.name === this.vendor) ?? null;
  }

  methodTone(m: DisposalMethod): string {
    return METHOD_CARDS.find((c) => c.method === m)?.tone ?? 'violet';
  }

  /** One-line description of a method, from its card or its supporting note. */
  methodText(m: DisposalMethod): string {
    return METHOD_CARDS.find((c) => c.method === m)?.text ?? METHOD_NOTES[m] ?? '';
  }

  get methodNote(): string {
    return METHOD_NOTES[this.method] ?? '';
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

  readonly conditions = CONDITIONS;

  get locations(): string[] {
    return [...new Set(this.pool.map((a) => a.location).filter(Boolean))].sort();
  }

  get categories(): string[] {
    return [...new Set(this.pool.map((a) => a.category).filter(Boolean))];
  }

  get assetStatuses(): string[] {
    return [...new Set(['Active', ...this.pool.map((a) => a.status).filter(Boolean)])];
  }

  get manufacturers(): string[] {
    return [...new Set(this.pool.map((a) => a.manufacturer).filter(Boolean))].sort();
  }

  get chosen(): CandidateAsset[] {
    return [...this.picked.values()];
  }

  get filteredAssets(): CandidateAsset[] {
    const q = this.assetSearch.trim().toLowerCase();
    const condition = this.conditionFilter;
    const rows = (this.listTab === 'selected' ? this.chosen : this.pool).filter(
      (a) =>
        (!this.categoryFilter || a.category === this.categoryFilter) &&
        (!this.locationFilter || a.location === this.locationFilter) &&
        // The loaded list is already filtered by condition; picked assets know their own
        (!condition || this.listTab === 'all' || a.condition === condition) &&
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
    if (this.loadingAssets && this.listTab === 'all') return 'Loading assets...';
    if (this.assetsError && this.listTab === 'all') return this.assetsError;
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
      else this.unselect(a.tag);
    }
  }

  toggleIncluded(tag: string): void {
    if (this.included.has(tag)) this.unselect(tag);
    else this.select(tag);
  }

  private select(tag: string): void {
    const asset = this.picked.get(tag) ?? this.pool.find((a) => a.tag === tag);
    if (!asset) return;
    this.included.add(tag);
    this.picked.set(tag, asset);
    // The first asset picked sets the category when none was chosen
    if (!this.category) this.category = asset.category;
  }

  private unselect(tag: string): void {
    this.included.delete(tag);
    this.picked.delete(tag);
  }

  onFilter(): void {
    this.page = 0;
    if (this.conditionFilter !== this.loadedCondition && !this.loadingAssets) this.loadAssets();
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
    this.picked.clear();
    this.onFilter();
  }

  removeAsset(a: CandidateAsset): void {
    this.unselect(a.tag);
    this.page = Math.min(this.page, this.pageCount - 1);
  }

  /**
   * Add by Tag / Import from Excel go through the API, which needs a saved request: the draft is created (or
   * updated) with the current selection first, then the API adds the matching assets and reports the rest.
   */
  private addThroughApi(call: (id: string) => Observable<DisposalAddAssetsResult>): void {
    if (!this.title.trim()) {
      this.showToast('Enter the request title on step 1 first; the request is saved as a draft before assets are added.');
      return;
    }
    if (this.saving) return;
    const work = this.ensureRequest().pipe(
      concatMap((id) => this.api.updateAssets(id, this.assetsBody(), true).pipe(map(() => id))),
      concatMap((id) => call(id)),
    );
    this.saving = true;
    work.subscribe({
      next: (res) => {
        this.saving = false;
        this.syncAssets(res.request);
        const notFound = res.notFound?.length ?? 0;
        const rejected = Object.entries(res.rejected ?? {});
        const parts = [`${res.addedCount} asset${res.addedCount === 1 ? '' : 's'} added`];
        if (notFound) parts.push(`${notFound} tag${notFound === 1 ? '' : 's'} not found`);
        if (rejected.length) parts.push(`${rejected.length} not available (${rejected.map(([t, why]) => `${t}: ${why}`).join('; ')})`);
        this.showToast(parts.join(', ') + '.');
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err));
      },
    });
  }

  // Add by Tag dialog

  openTagDialog(): void {
    this.tagText = '';
    this.tagDialog = true;
  }

  confirmTags(): void {
    const tags = [...new Set(this.tagText.split(/[\s,;]+/).map((t) => t.trim()).filter(Boolean))];
    this.tagDialog = false;
    if (tags.length) this.addThroughApi((id) => this.api.addAssetsByTag(id, tags));
  }

  /** Sends the sheet to the API, which reads the "Asset Tag" column (or the first column). */
  importExcel(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.addThroughApi((id) => this.api.importAssets(id, file));
  }

  // ===== Approvers =====

  approverOf(name: string): Approver | null {
    return this.approverChoices.find((a) => a.name === name) ?? null;
  }

  initials(name: string): string {
    return name.split(' ').filter(Boolean).map((w) => w[0]).slice(0, 2).join('').toUpperCase();
  }

  /** Approvers not already used on another level, plus the row's own. */
  approverOptions(row: ApprovalStep): Approver[] {
    return this.approverChoices.filter((a) => a.name === row.approver || !this.approvals.some((r) => r.approver === a.name));
  }

  addApprover(): void {
    const free = this.approverChoices.find((a) => !this.approvals.some((r) => r.approver === a.name));
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
  // The API stores attachments already uploaded to the Media service (file URL). There is no upload here yet,
  // so new files are listed for this session only and are not sent; files loaded from the request are kept.

  addFiles(list: FileList | null | undefined): void {
    const tooBig = Array.from(list ?? []).filter((f) => f.size > 10 * 1024 * 1024);
    const by = this.auth.getUser()?.userName || this.api.currentUser() || '';
    for (const f of Array.from(list ?? [])) {
      if (f.size <= 10 * 1024 * 1024) this.files.push({ name: f.name, size: f.size, added: nowStamp(), by, description: '' });
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
    return this.chosen.reduce((n, a) => n + (a.value || 0), 0);
  }

  conditionClass(c: string): string {
    if (/end of life|damaged|under repair/i.test(c)) return 'cond cond--bad';
    return /good|new/i.test(c) ? 'cond cond--good' : 'cond cond--warn';
  }

  // ===== API bodies =====

  private detailsBody(): DisposalDetailsBody {
    return {
      requestTitle: this.title.trim(),
      requestDate: this.requestDate || null,
      requestedBy: this.requestedBy ? { userId: null, name: this.requestedBy, roleDepartment: null } : null,
      reason: this.reason || null,
      disposalCategory: this.category || null,
      priority: this.priority || null,
      justification: this.justification.trim() || null,
      currency: 'AED',
    };
  }

  private assetsBody() {
    return this.chosen.map((a) => ({ assetRegistrationId: a.id, estimatedValue: a.value || 0 }));
  }

  private methodBody(): DisposalMethodBody {
    const v = this.vendorDetails;
    return {
      method: this.method || null,
      expectedDisposalDate: this.expectedDate || null,
      vendor: this.vendor ? { name: this.vendor, address: v?.address || null, tradeLicense: v?.license || null } : null,
      contactPerson: this.contactPerson.trim() || null,
      contactNumber: this.contactNumber.trim() || null,
      instructions: this.instructions.trim() || null,
    };
  }

  private approvalBody(): DisposalApprovalBody {
    return {
      approvers: this.approvals
        .filter((a) => a.approver)
        .map((a) => ({
          approvalLevel: a.level.trim(),
          approver: { name: a.approver, roleDepartment: this.approverOf(a.approver)?.role || null },
          approvalType: a.type,
        })),
      notifyApprovers: this.notifyApprovers,
      attachments: this.files
        .filter((f) => f.url)
        .map((f) => ({
          fileName: f.name, fileUrl: f.url!, fileType: this.ext(f.name).toUpperCase() || null, fileSize: f.size || null,
          description: f.description.trim() || null, uploadedBy: f.by || null, uploadedAt: f.added || null,
        })),
      remarks: this.remarks.trim() || null,
    };
  }

  // ===== Save =====

  /** The request id, creating the draft from step 1 when there is none yet. */
  private ensureRequest(): Observable<string> {
    return defer(() =>
      this.requestId
        ? of(this.requestId)
        : this.api.create(this.detailsBody(), true).pipe(
            tap((d) => {
              this.requestId = d.id;
              this.requestNumber = d.requestNumber;
            }),
            map((d) => d.id),
          ),
    );
  }

  /** Saves one wizard step: POST on the first save of step 1, otherwise PUT /{id}/{step}. */
  private saveStep(step: number, draft: boolean): Observable<DisposalRequestDto | null> {
    return defer(() => {
      if (step === 1) {
        if (!this.requestId) {
          return this.api.create(this.detailsBody(), draft).pipe(
            tap((d) => {
              this.requestId = d.id;
              this.requestNumber = d.requestNumber;
            }),
          );
        }
        return this.api.updateDetails(this.requestId, this.detailsBody(), draft);
      }
      if (step > 4) return of(null);
      return this.ensureRequest().pipe(
        concatMap((id) => {
          if (step === 2) return this.api.updateAssets(id, this.assetsBody(), draft).pipe(tap((d) => this.syncAssets(d)));
          if (step === 3) return this.api.updateMethod(id, this.methodBody(), draft);
          return this.api.updateApproval(id, this.approvalBody(), draft);
        }),
      );
    });
  }

  /** Saves steps 1 to 4 in order. */
  private saveAll(draft: boolean): Observable<DisposalRequestDto | null> {
    return from([1, 2, 3, 4]).pipe(concatMap((s) => this.saveStep(s, draft)), last());
  }

  private run<T>(work: Observable<T>, done: (result: T) => void): void {
    this.saving = true;
    work.subscribe({
      next: (result) => {
        this.saving = false;
        done(result);
      },
      error: (err) => {
        this.saving = false;
        this.error = apiErrorMessage(err);
      },
    });
  }

  saveDraft(): void {
    if (this.saving) return;
    if (!this.title.trim()) {
      this.step = 1;
      this.error = 'Enter the request title before saving a draft.';
      return;
    }
    this.error = '';
    this.run(this.saveAll(true), () => this.router.navigate(['/assets/disposal']));
  }

  submit(): void {
    if (this.saving) return;
    for (let s = 1; s < STEPS.length; s++) {
      const err = this.stepError(s);
      if (err) {
        this.step = s;
        this.error = err;
        return;
      }
    }
    this.error = '';
    this.run(
      this.saveAll(false).pipe(concatMap(() => this.api.submit(this.requestId!))),
      () => this.router.navigate(['/assets/disposal']),
    );
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
