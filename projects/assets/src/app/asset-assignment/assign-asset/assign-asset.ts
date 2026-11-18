import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowLeftRight,
  ArrowRight,
  Barcode,
  Bell,
  Building2,
  CalendarClock,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  EllipsisVertical,
  ListFilter,
  ChevronRight,
  Camera,
  Cctv,
  Check,
  CircleCheck,
  CloudUpload,
  CreditCard,
  Download,
  FileSpreadsheet,
  FileText,
  Forklift,
  Hexagon,
  ImageIcon,
  Info,
  Keyboard,
  Laptop,
  LayoutGrid,
  Layers,
  Mail,
  MapPin,
  Monitor,
  Paperclip,
  Pencil,
  Phone,
  Plus,
  Projector,
  ScanBarcode,
  ScanLine,
  Settings,
  Search,
  Smartphone,
  Tablet,
  UserRound,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import { Observable, finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../services/asset-api';
import {
  Assignment, AssignmentType, AssetKind, COST_CENTERS, DepartmentManager, ENTITIES, ENTITY_KINDS, EntityKind, LocationDetails, SiteNode, TARGET_LABELS, isoDate,
} from '../asset-assignment-data';
import {
  AssetAssignmentDto, AssetAssignmentRequest, AssetAssignmentService, AssignmentAttachmentDto, AssignmentLookups, EMPTY_LOOKUPS, LookupEmployee, PoolItem,
  assignToOf, choicesFor, dayOf, departmentRequest, employeeRequest, locationFrom, siteTreeOf, toRows, typeOf, uniqueSorted,
} from '../asset-assignment.service';

type SelectMode = 'single' | 'multiple' | 'scan';
type ScanSource = 'camera' | 'external';
type SiteTab = 'tree' | 'search';

/** Assignment Type choices for each kind of assignee; the first is the default. */
const TRANSFER_TYPES: Record<AssignmentType, string[]> = {
  Employee: ['Primary User', 'Secondary User', 'Shared User', 'Temporary Use'],
  Department: ['Department Use', 'Shared Use', 'Temporary Transfer', 'Loan'],
  Location: ['Primary Location', 'Temporary Location', 'Storage', 'Shared Space'],
  Other: ['Temporary Transfer', 'Permanent', 'Loan', 'Shared Use'],
};

interface Attachment {
  name: string;
  size: number;
  /** "YYYY-MM-DD" */
  added: string;
  /** A file picked now; uploaded when the assignment is saved. */
  file?: File;
  /** A file already uploaded (reopened draft). */
  stored?: AssignmentAttachmentDto;
}

/** The Assign Asset wizard state. */
interface Wizard {
  step: number;
  /** The saved draft being continued, if any. */
  draftId?: string;
  /** Tags of the assets being assigned. */
  tags: string[];
  selectMode: SelectMode;
  assetSearch: string;
  target: AssignmentType;
  /** Employee, department or location name (not used for other entities). */
  assignee: string;
  entityKind: EntityKind;
  entity: string;
  contact: string;
  phone: string;
  email: string;
  agreement: string;
  approvalRef: string;
  /** Where the assets sit: filled from the employee, department or site picked, and editable. */
  costCenter: string;
  site: string;
  building: string;
  floor: string;
  room: string;
  reason: string;
  transfer: string;
  from: string;
  to: string;
  openEnded: boolean;
  serviceType: string;
  workOrder: string;
  serviceLocation: string;
  expectedReturn: string;
  returnCondition: string;
  responsible: string;
  remarks: string;
  notes: string;
  files: Attachment[];
  notify: boolean;
  notifyMessage: string;
  notifyManager: boolean;
  notifyDepartment: boolean;
  reminder: boolean;
  reminderDate: string;
  reminderMessage: string;
  reference: string;
  requestor: string;
  priority: string;
  customReason: string;
}

/** Registered asset with its current assignment and the facts shown when it's scanned or picked. */
interface PoolRow extends PoolItem {
  current?: Assignment;
  brand: string;
  model: string;
}

/** Browser barcode reader (Chrome / Edge / Android); not in the TypeScript DOM types yet. */
interface BarcodeReader {
  detect(source: CanvasImageSource | ImageBitmap): Promise<{ rawValue: string }[]>;
}
type BarcodeReaderCtor = new (options?: { formats?: string[] }) => BarcodeReader;

const ACCEPTED_FILES = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'jpg', 'jpeg', 'png'];
const MAX_FILE_BYTES = 10 * 1024 * 1024;

/** Assign Asset page (route assets/assignment/assign-asset); ?type= preselects the assignee kind, ?assets= the asset tags. */
@Component({
  selector: 'app-assign-asset',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './assign-asset.html',
  styleUrls: ['../asset-assignment.css', '../asset-assignment-table.css', './assign-asset.css'],
})
export class AssignAsset implements OnInit, OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowLeftRight, ArrowRight, Barcode, Bell, Building2, CalendarClock, CalendarDays, Camera, ChevronDown, ChevronLeft, ChevronRight, EllipsisVertical, ListFilter, Check, CircleCheck, CloudUpload, Download, FileSpreadsheet, FileText, Hexagon,
    ImageIcon, Info, Keyboard, LayoutGrid, Layers, Mail, MapPin, Paperclip, Pencil, Phone, Plus, ScanLine, Search, Settings, UserRound, Wrench, X, Zap,
  };
  readonly kindIcons: Record<AssetKind, typeof Laptop> = {
    laptop: Laptop, phone: Smartphone, scanner: ScanBarcode, projector: Projector, forklift: Forklift, tool: Wrench, card: CreditCard, monitor: Monitor, tablet: Tablet, camera: Cctv,
    other: Hexagon,
  };
  readonly assignmentTypes: AssignmentType[] = ['Employee', 'Department', 'Location', 'Other'];
  readonly targetLabels = TARGET_LABELS;
  readonly entityKinds = ENTITY_KINDS;
  readonly selectModes: { key: SelectMode; label: string }[] = [
    { key: 'single', label: 'Single Asset' },
    { key: 'multiple', label: 'Multiple Assets' },
    { key: 'scan', label: 'Scan Asset' },
  ];
  readonly steps = [
    { title: 'Asset Selection', sub: 'Choose asset(s) to assign' },
    { title: 'Assignment Details', sub: 'Set assignment information' },
    { title: 'Additional Information', sub: 'Notes, documents, etc.' },
    { title: 'Review & Confirm', sub: 'Verify and assign' },
  ];
  readonly reasons = ['Operational Use', 'Repair / Service', 'New Joiner', 'Project Work', 'Replacement', 'Temporary Use', 'Relocation', 'Calibration', 'Other'];
  readonly serviceTypes = ['AMC Support', 'Warranty Repair', 'Calibration', 'Upgrade', 'Inspection', 'Installation'];
  readonly returnConditions = ['Same as Assigned', 'Serviced / Repaired', 'Upgraded', 'Not Applicable'];
  readonly priorities = ['Low', 'Normal', 'High', 'Urgent'];
  readonly acceptAttr = ACCEPTED_FILES.map((x) => '.' + x).join(',');
  readonly remarksMax = 500;

  /** Registered assets, with the active assignment each is in. */
  pool: PoolRow[] = [];
  lookups: AssignmentLookups = EMPTY_LOOKUPS;
  siteTree: SiteNode[] = [];
  loading = false;
  loadError = '';
  wizard: Wizard;
  wizardError = '';
  saving = false;
  fileError = '';
  dragOver = false;

  // ===== Multiple Assets table =====
  readonly pageSizes = [10, 25, 50];
  poolCategory = '';
  poolLocation = '';
  poolStatus = '';
  poolType = '';
  moreFilters = false;
  page = 0;
  pageSize = 10;
  /** Row whose action menu is open, and where to draw it. */
  rowMenu: { tag: string; top: number; right: number } | null = null;

  // ===== Asset lookup (single pick and scanning) =====
  /** Asset shown in the result card. */
  found: PoolRow | null = null;
  scanSource: ScanSource = 'camera';
  scanError = '';
  manualOpen = false;
  manualTag = '';
  externalTag = '';
  cameraOn = false;
  cameraStarting = false;
  torchOn = false;
  torchSupported = false;
  private stream: MediaStream | null = null;
  private video?: HTMLVideoElement;
  private scanTimer?: ReturnType<typeof setInterval>;
  private reader: BarcodeReader | null = null;

  @ViewChild('scanVideo') set scanVideo(ref: ElementRef<HTMLVideoElement> | undefined) {
    this.video = ref?.nativeElement;
    if (this.video && this.stream) {
      this.video.srcObject = this.stream;
      this.video.play().catch(() => undefined);
    }
  }

  private readonly router = inject(Router);
  private readonly api = inject(AssetAssignmentService);
  /** Page opened with ?type= or ?assets=; a saved draft is not restored then. */
  private readonly preset: boolean;

  constructor() {
    // ?type=Department preselects the target, ?assets=IT-000456,IT-000457 the assets
    const query = inject(ActivatedRoute).snapshot.queryParamMap;
    const type = query.get('type') as AssignmentType | null;
    const tags = (query.get('assets') ?? '').split(',').filter(Boolean);
    this.preset = !!type || !!tags.length;
    this.wizard = this.newWizard(type && this.assignmentTypes.includes(type) ? type : 'Employee', tags);
  }

  /** Loads the assets, the assignments they are in, master data and (when opened plainly) the user's last draft. */
  ngOnInit(): void {
    this.loading = true;
    forkJoin({ assets: this.api.listAssets(), assignments: this.api.listAll(), lookups: this.api.lookups() })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ assets, assignments, lookups }) => {
          this.lookups = lookups;
          this.siteTree = siteTreeOf(lookups);
          if (this.siteTree[0]) this.openNodes.add(this.siteTree[0].name);
          this.pool = this.buildPool(assets, assignments);
          const w = this.wizard;
          w.tags = w.tags.filter((t) => this.pool.some((p) => p.tag === t));
          if (w.step === 2 && !w.tags.length) w.step = 1;
          if (!w.reference) w.reference = `ASSIGN-${new Date().getFullYear()}-${String(assignments.length + 1).padStart(3, '0')}`;
          if (!w.requestor) w.requestor = this.requestors[0] ?? '';
          const user = this.api.user;
          const draft = this.preset ? undefined : assignments.find((d) => /^draft$/i.test(d.status ?? '') && (!user || d.createdBy === user));
          if (draft) {
            this.wizard = this.fromDraft(draft);
            if (this.wizard.target === 'Employee' && this.wizard.assignee) this.fillSiteFrom(this.wizard, this.wizard.assignee, false);
          }
        },
        error: (err) => (this.loadError = apiErrorMessage(err, 'Assets could not be loaded.')),
      });
  }

  /** Assets with the active assignment each is in, and the brand / model shown when picked. */
  private buildPool(assets: PoolItem[], assignments: AssetAssignmentDto[]): PoolRow[] {
    const current = new Map<string, Assignment>();
    for (const d of assignments) {
      if (!/^active$/i.test(d.status ?? '')) continue;
      for (const row of toRows(d)) if (!current.has(row.assetRegistrationId)) current.set(row.assetRegistrationId, row);
    }
    return assets.map((a) => this.poolRow(a, current.get(a.assetRegistrationId)));
  }

  private poolRow(a: PoolItem, current?: Assignment): PoolRow {
    const [first, ...rest] = a.assetName.split(' ');
    return {
      ...a, current, serial: a.serial || current?.serial || '', brand: a.manufacturer || first, model: a.manufacturer ? a.assetName : rest.join(' ') || a.assetName,
      category: a.category || a.assetType, location: current?.location || a.location || '—',
    };
  }

  /** The wizard as it was saved with Save as Draft. */
  private fromDraft(d: AssetAssignmentDto): Wizard {
    const target = d.assignTo ? typeOf(d.assignTo) : 'Employee';
    const l = d.location;
    const e = d.otherEntity;
    const mode = (d.selectionMode ?? '').toLowerCase();
    const assignee =
      target === 'Employee' ? d.employee?.employeeName ?? '' : target === 'Department' ? d.department?.departmentName ?? '' : target === 'Location' ? (l?.buildingName ? `${l.siteName} - ${l.buildingName}` : l?.siteName ?? '') : '';
    const base = this.newWizard(target, (d.assets ?? []).map((a) => a.assetTag || a.assetRegistrationId));
    return {
      ...base,
      step: 1, draftId: d.id, selectMode: mode === 'multiple' || mode === 'scan' ? mode : base.selectMode, assignee,
      entityKind: (ENTITY_KINDS.find((k) => k === e?.entityType) ?? base.entityKind), entity: e?.entityName ?? '', contact: e?.contactPerson ?? '', phone: e?.contactPhone ?? '',
      email: e?.contactEmail ?? '', agreement: e?.agreementReference ?? '', approvalRef: d.approvalReference ?? '', costCenter: d.costCenter ?? '', site: l?.siteName ?? '',
      building: l?.buildingName ?? '', floor: l?.floorName ?? '', room: l?.roomName ?? '', reason: d.purpose ?? e?.reason ?? '', transfer: d.assignmentType || base.transfer,
      from: dayOf(d.fromDate) || base.from, to: dayOf(d.toDate), openEnded: d.isOpenEnded, serviceType: e?.serviceType ?? '', workOrder: e?.serviceRequestNumber ?? '',
      serviceLocation: e?.serviceLocation ?? '', expectedReturn: dayOf(e?.expectedReturnDate), returnCondition: e?.returnCondition || base.returnCondition,
      responsible: e?.responsiblePerson?.name ?? '', remarks: d.remarks ?? '', notes: d.notes ?? '',
      files: (d.attachments ?? []).map((a) => ({ name: a.fileName, size: a.fileSize ?? 0, added: dayOf(d.updatedAt ?? d.createdAt), stored: a })),
      notify: d.notification?.notifyAssignees ?? base.notify, notifyMessage: d.notification?.message ?? base.notifyMessage, notifyManager: d.notification?.notifyManager ?? base.notifyManager,
      notifyDepartment: d.notification?.notifyDepartment ?? false, reminder: d.reminder?.isEnabled ?? false, reminderDate: dayOf(d.reminder?.reminderDate), reminderMessage: d.reminder?.message ?? '',
      reference: d.customFields?.assignmentReference ?? '', requestor: d.customFields?.requestorName ?? base.requestor, priority: d.customFields?.priority || base.priority,
      customReason: d.customFields?.assignmentReason ?? '',
    };
  }

  // ===== Master data choices =====

  assigneeChoices(type: AssignmentType): string[] {
    return choicesFor(this.lookups, type, ENTITIES.map((e) => e.name));
  }

  get costCenters(): string[] {
    return uniqueSorted([...COST_CENTERS, ...this.lookups.employees.map((e) => e.costCenter), this.wizard.costCenter]);
  }

  get sites(): string[] {
    return uniqueSorted([...this.lookups.sites.map((x) => x.name), this.wizard.site]);
  }

  private get siteId(): string | undefined {
    return this.lookups.sites.find((x) => x.name === this.wizard.site)?.id;
  }

  /** Buildings of the chosen site (all when none is chosen). */
  get buildings(): string[] {
    const id = this.siteId;
    return uniqueSorted([...this.lookups.buildings.filter((b) => !id || !b.siteId || b.siteId === id).map((b) => b.name), this.wizard.building]);
  }

  get floors(): string[] {
    const w = this.wizard;
    const id = this.siteId;
    return uniqueSorted([...this.lookups.floors.filter((f) => (!id || !f.siteId || f.siteId === id) && (!w.building || !f.buildingName || f.buildingName === w.building)).map((f) => f.name), w.floor]);
  }

  get rooms(): string[] {
    const w = this.wizard;
    const id = this.siteId;
    return uniqueSorted([
      ...this.lookups.rooms.filter((r) => (!id || !r.siteId || r.siteId === id) && (!w.building || !r.buildingName || r.buildingName === w.building) && (!w.floor || !r.floorName || r.floorName === w.floor)).map((r) => r.name),
      w.room,
    ]);
  }

  get requestors(): string[] {
    return uniqueSorted([this.api.user, ...this.lookups.employees.map((e) => e.name)]);
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }

  private newWizard(target: AssignmentType, tags: string[]): Wizard {
    return {
      step: tags.length ? 2 : 1, tags, selectMode: tags.length > 1 ? 'multiple' : 'single', assetSearch: '', target, assignee: '', entityKind: 'Vendor / Supplier', entity: '',
      contact: '', phone: '', email: '', agreement: '', approvalRef: '', costCenter: '', site: '', building: '', floor: '', room: '', reason: '', transfer: TRANSFER_TYPES[target][0], from: isoDate(new Date()),
      to: '', openEnded: target !== 'Other', serviceType: '', workOrder: '', serviceLocation: '', expectedReturn: '', returnCondition: 'Same as Assigned', responsible: '',
      remarks: '', notes: '', files: [], notify: true, notifyMessage: 'Your asset(s) have been assigned to you. Please check the details in Asset360.', notifyManager: true,
      notifyDepartment: false, reminder: false, reminderDate: '', reminderMessage: '', reference: '',
      requestor: this.api.user, priority: 'Normal', customReason: '',
    };
  }

  // ===== Asset selection =====

  get poolFiltered(): PoolRow[] {
    const q = this.wizard.assetSearch.trim().toLowerCase();
    const multiple = this.wizard.selectMode === 'multiple';
    return this.pool.filter((p) =>
      (!q || `${p.assetName} ${p.assetType} ${p.tag} ${p.serial} ${p.category}`.toLowerCase().includes(q)) &&
      (!multiple || ((!this.poolCategory || p.category === this.poolCategory) &&
        (!this.poolLocation || this.locationMain(p.location) === this.poolLocation) &&
        (!this.poolStatus || this.poolStatusOf(p) === this.poolStatus) &&
        (!this.poolType || p.assetType === this.poolType))),
    );
  }

  get poolCategories(): string[] {
    return [...new Set(this.pool.map((p) => p.category))].sort();
  }

  get poolLocations(): string[] {
    return [...new Set(this.pool.map((p) => this.locationMain(p.location)))].sort();
  }

  get poolTypes(): string[] {
    return [...new Set(this.pool.map((p) => p.assetType))].sort();
  }

  poolStatusOf(p: PoolRow): 'Available' | 'Assigned' {
    return p.current ? 'Assigned' : 'Available';
  }

  /** "Main Office - IT Store" splits into the place and the spot within it. */
  locationMain(location: string): string {
    return location.split(' - ')[0];
  }

  locationSub(location: string): string {
    return location.split(' - ').slice(1).join(' - ');
  }

  get filtersOn(): boolean {
    return !!(this.poolCategory || this.poolLocation || this.poolStatus || this.poolType);
  }

  resetFilters(): void {
    this.poolCategory = this.poolLocation = this.poolStatus = this.poolType = '';
    this.page = 0;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.poolFiltered.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get poolPage(): PoolRow[] {
    const page = Math.min(this.page, this.pageCount - 1);
    return this.poolFiltered.slice(page * this.pageSize, (page + 1) * this.pageSize);
  }

  get pageFrom(): number {
    return this.poolFiltered.length ? Math.min(this.page, this.pageCount - 1) * this.pageSize + 1 : 0;
  }

  get pageTo(): number {
    return Math.min(this.pageFrom + this.pageSize - 1, this.poolFiltered.length);
  }

  get allPageChosen(): boolean {
    const rows = this.poolPage;
    return rows.length > 0 && rows.every((p) => this.isChosen(p.tag));
  }

  togglePagePool(on: boolean): void {
    const w = this.wizard;
    const tags = this.poolPage.map((p) => p.tag);
    w.tags = on ? [...new Set([...w.tags, ...tags])] : w.tags.filter((t) => !tags.includes(t));
  }

  openRowMenu(tag: string, event: MouseEvent): void {
    event.stopPropagation();
    if (this.rowMenu?.tag === tag) return void (this.rowMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.rowMenu = { tag, top: r.bottom + 6, right: window.innerWidth - r.right };
  }

  @HostListener('document:click')
  @HostListener('window:scroll')
  @HostListener('window:resize')
  @HostListener('document:keydown.escape')
  closeRowMenu(): void {
    this.rowMenu = null;
  }

  /** Row menu: assign just this asset and move on to the details step. */
  assignOnly(tag: string): void {
    this.rowMenu = null;
    this.wizard.tags = [tag];
    this.goStep(2);
  }

  /** Single Asset mode lists matches only once something is typed. */
  get singleMatches(): PoolRow[] {
    return this.wizard.assetSearch.trim() ? this.poolFiltered.slice(0, 8) : [];
  }

  get chosen(): PoolRow[] {
    const pool = this.pool;
    return this.wizard.tags.map((t) => pool.find((p) => p.tag === t)).filter((p): p is PoolRow => !!p);
  }

  /** Selected assets that are assigned elsewhere now. */
  get handovers(): number {
    return this.chosen.filter((p) => p.current).length;
  }

  isChosen(tag: string): boolean {
    return this.wizard.tags.includes(tag);
  }

  toggleAsset(tag: string): void {
    const w = this.wizard;
    w.tags = w.tags.includes(tag) ? w.tags.filter((t) => t !== tag) : [...w.tags, tag];
  }

  removeAsset(tag: string): void {
    this.wizard.tags = this.wizard.tags.filter((t) => t !== tag);
  }

  clearSelection(): void {
    this.wizard.tags = [];
  }

  get allPoolChosen(): boolean {
    const rows = this.poolFiltered;
    return rows.length > 0 && rows.every((p) => this.isChosen(p.tag));
  }

  toggleAllPool(on: boolean): void {
    const w = this.wizard;
    const tags = this.poolFiltered.map((p) => p.tag);
    w.tags = on ? [...new Set([...w.tags, ...tags])] : w.tags.filter((t) => !tags.includes(t));
  }

  setSelectMode(mode: SelectMode): void {
    const w = this.wizard;
    if (w.selectMode === mode) return;
    w.selectMode = mode;
    this.found = null;
    this.scanError = '';
    if (mode === 'scan' && this.scanSource === 'camera') this.startCamera();
    else this.stopCamera();
  }

  /** Asset shown in the Single Asset card: the last pick, or the only selected asset. */
  get singlePicked(): PoolRow | null {
    return this.found ?? (this.wizard.tags.length === 1 ? this.chosen[0] : null);
  }

  /** Single Asset mode holds one asset, so a pick replaces the selection. */
  pickSingle(p: PoolRow): void {
    this.found = p;
    this.wizard.tags = [p.tag];
    this.wizard.assetSearch = '';
    this.scanError = '';
  }

  changeAsset(search: HTMLInputElement): void {
    this.found = null;
    this.wizard.tags = [];
    search.focus();
  }

  addFound(): void {
    if (this.found && !this.isChosen(this.found.tag)) this.wizard.tags = [...this.wizard.tags, this.found.tag];
  }

  /** Finds an asset by tag or serial number, ignoring case. */
  private lookup(code: string): PoolRow | undefined {
    const c = code.trim().toLowerCase();
    return c ? this.pool.find((p) => p.tag.toLowerCase() === c || p.serial.toLowerCase() === c) : undefined;
  }

  /** A code read by the camera, an uploaded image, the external scanner or typed in. */
  onCode(code: string): void {
    const p = this.lookup(code);
    if (p) {
      this.scanError = '';
      this.found = p;
      return;
    }
    // Not a known tag: ask the API (matches serial numbers, which the asset list does not return)
    const c = code.trim();
    this.found = null;
    this.scanError = '';
    this.api.searchAssets(c).subscribe({
      next: (items) => {
        const hit = items.length === 1 ? items[0] : items.find((i) => i.tag.toLowerCase() === c.toLowerCase());
        const row = hit && (this.pool.find((x) => x.assetRegistrationId === hit.assetRegistrationId) ?? this.poolRow({ ...hit, serial: c }));
        if (!row) return void (this.scanError = `No asset found for "${c}".`);
        if (!row.serial) row.serial = c;
        if (!this.pool.includes(row)) this.pool = [...this.pool, row];
        this.found = row;
      },
      error: (err) => (this.scanError = apiErrorMessage(err, `No asset found for "${c}".`)),
    });
  }

  submitManual(): void {
    if (!this.manualTag.trim()) return;
    this.onCode(this.manualTag);
    if (this.found) this.manualTag = '';
  }

  submitExternal(): void {
    if (!this.externalTag.trim()) return;
    this.onCode(this.externalTag);
    this.externalTag = '';
  }

  /** Clears the result and keeps reading. */
  scanNext(): void {
    this.found = null;
    this.scanError = '';
  }

  setScanSource(source: ScanSource): void {
    if (this.scanSource === source) return;
    this.scanSource = source;
    this.scanNext();
    if (source === 'camera') this.startCamera();
    else this.stopCamera();
  }

  private barcodeReader(): BarcodeReader | null {
    const Ctor = (window as unknown as { BarcodeDetector?: BarcodeReaderCtor }).BarcodeDetector;
    if (!this.reader && Ctor) this.reader = new Ctor({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13', 'ean_8', 'upc_a', 'data_matrix'] });
    return this.reader;
  }

  async startCamera(): Promise<void> {
    if (this.stream || this.cameraStarting) return;
    if (!navigator.mediaDevices?.getUserMedia) return void (this.scanError = 'This browser cannot use the camera. Use the external scanner or enter the tag manually.');
    this.cameraStarting = true;
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
      this.cameraOn = true;
      this.scanError = '';
      const track = this.stream.getVideoTracks()[0];
      this.torchSupported = !!(track?.getCapabilities?.() as { torch?: boolean } | undefined)?.torch;
      if (this.video) this.scanVideo = new ElementRef(this.video);
      const reader = this.barcodeReader();
      if (!reader) {
        this.scanError = 'This browser cannot read barcodes from the camera. Use the external scanner or enter the tag manually.';
        return;
      }
      this.scanTimer = setInterval(() => {
        const v = this.video;
        if (this.found || !v || v.readyState < 2) return;
        reader
          .detect(v)
          .then((codes) => {
            if (codes[0] && !this.found) this.onCode(codes[0].rawValue);
          })
          .catch(() => undefined);
      }, 500);
    } catch {
      this.scanError = 'Camera access was blocked or no camera was found. Allow camera access, or use the external scanner.';
      this.stopCamera();
    } finally {
      this.cameraStarting = false;
    }
  }

  stopCamera(): void {
    clearInterval(this.scanTimer);
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    this.cameraOn = this.torchOn = this.torchSupported = false;
    if (this.video) this.video.srcObject = null;
  }

  toggleTorch(): void {
    const track = this.stream?.getVideoTracks()[0];
    if (!track) return;
    const on = !this.torchOn;
    track
      .applyConstraints({ advanced: [{ torch: on } as MediaTrackConstraintSet] })
      .then(() => (this.torchOn = on))
      .catch(() => (this.torchSupported = false));
  }

  async scanImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const reader = this.barcodeReader();
    if (!reader) return void (this.scanError = 'This browser cannot read barcodes from images. Enter the asset tag manually.');
    try {
      const codes = await reader.detect(await createImageBitmap(file));
      if (codes[0]) this.onCode(codes[0].rawValue);
      else this.scanError = 'No barcode or QR code found in that image.';
    } catch {
      this.scanError = 'That image could not be read.';
    }
  }

  // ===== Assignment details =====

  setTarget(t: AssignmentType): void {
    const w = this.wizard;
    if (w.target === t) return;
    w.target = t;
    w.assignee = '';
    w.transfer = TRANSFER_TYPES[t][0];
    if (t === 'Other') w.openEnded = false;
    this.wizardError = '';
    this.profileOpen = false;
  }

  get transferTypes(): string[] {
    return TRANSFER_TYPES[this.wizard.target];
  }

  get employee(): LookupEmployee | undefined {
    const w = this.wizard;
    return w.target === 'Employee' ? this.lookups.employees.find((e) => e.name === w.assignee) : undefined;
  }

  /** Manager shown next to the employee (their reporting manager) or the department (its head). */
  get manager(): DepartmentManager | undefined {
    const e = this.employee;
    if (e?.managerId || e?.managerName) {
      const m = this.lookups.employees.find((x) => x.mdmId === e.managerId);
      return { name: m?.name || e.managerName, id: m?.id ?? '', title: m?.title ?? '', location: m?.location ?? '' };
    }
    return this.reviewDepartment?.manager;
  }

  profileOpen = false;

  /** Picking an employee or department also fills in where the assets will sit. */
  onAssignee(name: string): void {
    const w = this.wizard;
    w.assignee = name;
    this.fillSiteFrom(w, name);
  }

  /** The employee's site and cost center, or the department head's site, fill the site fields (only empty ones when `overwrite` is false). */
  private fillSiteFrom(w: Wizard, name: string, overwrite = true): void {
    w.assignee = name;
    const e = w.target === 'Employee' ? this.lookups.employees.find((x) => x.name === name) : undefined;
    const site = e?.location || (w.target === 'Department' ? this.lookups.departments.find((d) => d.name === name)?.manager?.location : '') || '';
    const d: LocationDetails = { site: this.lookups.sites.find((x) => x.name === site || x.code === site)?.name ?? site, building: '', floor: '', room: '', costCenter: e?.costCenter ?? '' };
    if (!d.site && !d.costCenter) return;
    if (overwrite || !w.costCenter) w.costCenter = d.costCenter || w.costCenter;
    if (d.site && (overwrite || !w.site)) {
      if (w.site !== d.site) w.building = w.floor = w.room = '';
      w.site = d.site;
    }
  }

  // ===== Location / site picker =====

  siteTab: SiteTab = 'tree';
  siteQuery = '';
  /** Tree rows that are open, by path. */
  readonly openNodes = new Set<string>(['All Locations', 'UAE', 'UAE/Head Office']);

  toggleNode(path: string): void {
    if (this.openNodes.has(path)) this.openNodes.delete(path);
    else this.openNodes.add(path);
  }

  /** Every site and building with its path, for the search tab. */
  get siteRows(): { node: SiteNode; path: string[] }[] {
    const rows: { node: SiteNode; path: string[] }[] = [];
    const walk = (nodes: SiteNode[], path: string[]) => {
      for (const n of nodes) {
        const p = [...path, n.name];
        if (n.kind === 'Site' || n.kind === 'Building') rows.push({ node: n, path: p });
        if (n.children) walk(n.children, p);
      }
    };
    walk(this.siteTree, []);
    const q = this.siteQuery.trim().toLowerCase();
    return q ? rows.filter((r) => r.path.join(' ').toLowerCase().includes(q)) : rows;
  }

  /** The site the picked location belongs to, with its path, for the Selected Location card. */
  get pickedSite(): { node: SiteNode; path: string[] } | null {
    const w = this.wizard;
    if (w.target !== 'Location' || !w.site) return null;
    return this.siteRows.find((r) => r.node.kind === 'Site' && r.node.name === w.site) ?? null;
  }

  /** A site sets the location; a building inside it sets the building as well. */
  pickSite(node: SiteNode, path: string[]): void {
    const w = this.wizard;
    const site = node.kind === 'Building' ? path[path.length - 2] : node.name;
    const facts = this.siteRows.find((r) => r.node.name === site)?.node;
    w.site = site;
    w.building = node.kind === 'Building' ? node.name : facts?.building ?? '';
    w.assignee = node.kind === 'Building' ? `${site} - ${node.name}` : site;
    this.wizardError = '';
  }

  changeLocation(): void {
    const w = this.wizard;
    w.assignee = w.site = w.building = '';
    this.siteTab = 'tree';
  }

  /** "Until reassigned", or the number of days between the dates. */
  get duration(): string {
    const w = this.wizard;
    if (w.openEnded) return 'Until reassigned';
    if (!w.from || !w.to) return '—';
    const days = Math.round((Date.parse(w.to) - Date.parse(w.from)) / 86400000) + 1;
    return days > 0 ? `${days} day${days === 1 ? '' : 's'}` : '—';
  }

  get periodRows(): { label: string; value: string }[] {
    const w = this.wizard;
    return [
      { label: 'From Date', value: this.date(w.from) || '—' },
      { label: 'To Date', value: w.openEnded ? 'Open Ended' : this.date(w.to) || '—' },
      { label: 'Duration', value: this.duration },
    ];
  }

  get entityOptions() {
    return ENTITIES.filter((e) => e.kind === this.wizard.entityKind);
  }

  get entityPeople(): string[] {
    return ENTITIES.find((e) => e.name === this.wizard.entity)?.people ?? [];
  }

  setEntityKind(k: EntityKind): void {
    if (this.wizard.entityKind === k) return;
    this.wizard.entityKind = k;
    this.onEntity('');
  }

  /** Picking an entity fills in its contact details and service location. */
  onEntity(name: string): void {
    const w = this.wizard;
    const e = ENTITIES.find((x) => x.name === name);
    w.entity = name;
    w.contact = e?.contact ?? '';
    w.phone = e?.phone ?? '';
    w.email = e?.email ?? '';
    w.serviceLocation = e?.location ?? '';
    w.responsible = e?.people[0] ?? '';
  }

  onOpenEnded(): void {
    if (this.wizard.openEnded) this.wizard.to = '';
  }

  /** The assignee's display name for the summary. */
  get wizardAssignee(): string {
    const w = this.wizard;
    return w.target === 'Other' ? w.entity : w.assignee;
  }

  /** Department the assets end up with (the employee's, or the department assigned to). */
  get reviewDepartment(): { name: string; manager?: DepartmentManager } | null {
    const w = this.wizard;
    const name = w.target === 'Department' ? w.assignee : w.target === 'Employee' ? this.employee?.department : '';
    return name ? { name, manager: this.lookups.departments.find((d) => d.name === name)?.manager } : null;
  }

  /** Where the assets end up, as entered on the Assignment Details step. */
  get reviewLocation(): (LocationDetails & { name: string }) | null {
    const w = this.wizard;
    if (w.target === 'Other' || !w.site) return null;
    return { name: w.site, site: w.site, building: w.building, floor: w.floor, room: w.room, costCenter: w.costCenter };
  }

  /** "Head Office - Block A (IT-204)", or the vendor's service location. */
  get locationLabel(): string {
    const l = this.reviewLocation;
    if (l) return `${l.site} - ${l.building} (${l.room})`;
    return this.wizard.target === 'Other' ? this.wizard.serviceLocation : '';
  }

  /** Phone and email of the other entity, for the review step. */
  get otherContact(): string {
    const w = this.wizard;
    return w.target === 'Other' ? [w.phone, w.email].filter(Boolean).join(' · ') : '';
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((x) => x[0].toUpperCase()).join('');
  }

  get notifyLabel(): string {
    const w = this.wizard;
    const who = [w.notify && 'Assignee', w.notifyManager && 'Manager', w.notifyDepartment && 'Department'].filter(Boolean);
    return who.length ? who.join(', ') : 'None';
  }

  private date(d: string): string {
    return d ? new Date(`${d}T00:00`).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
  }

  /** Label / value pairs shown in the Assignment Summary card. */
  get summaryRows(): { label: string; value: string }[] {
    const w = this.wizard;
    const yes = (on: boolean) => (on ? 'Yes' : 'No');
    let rows: { label: string; value: string }[];
    if (w.step === 4) {
      rows = [
        { label: 'Total Assets', value: String(w.tags.length) },
        { label: 'Assign To', value: this.wizardAssignee },
        { label: 'Assignment Type', value: w.transfer },
        { label: 'Location', value: this.locationLabel },
        { label: 'From Date', value: this.date(w.from) },
        { label: 'To Date', value: w.openEnded ? 'Open Ended' : this.date(w.to) },
        { label: 'Purpose', value: w.reason },
        { label: 'Attachments', value: w.files.length ? `${w.files.length} file${w.files.length === 1 ? '' : 's'}` : '' },
        { label: 'Notifications', value: this.notifyLabel },
      ];
    } else {
      rows =
        w.target === 'Other'
          ? [
              { label: 'Assign To', value: w.entityKind },
              { label: w.entityKind, value: w.entity },
              { label: 'Contact Person', value: w.contact },
              { label: 'Agreement / PO', value: w.agreement },
              { label: 'Service Type', value: w.serviceType },
              { label: 'Service Request', value: w.workOrder },
            ]
          : w.target === 'Location'
            ? [
                { label: 'Assignment Type', value: 'Location/Site' },
                { label: 'Location', value: w.site },
                { label: 'Building', value: w.building },
                { label: 'Floor', value: w.floor },
                { label: 'Room / Space', value: w.room },
              ]
            : [{ label: 'Assign To', value: w.assignee || this.targetLabels[w.target] }];
      if (w.target !== 'Location') rows.push({ label: 'Assignment Type', value: w.transfer });
      rows.push(
        { label: 'From Date', value: this.date(w.from) },
        { label: 'To Date', value: w.openEnded ? 'Open Ended' : this.date(w.to) },
      );
      if (w.step === 3) {
        rows.push(
          { label: 'No. of Assets', value: String(w.tags.length) },
          { label: 'Priority', value: w.priority },
          { label: 'Attachments', value: w.files.length ? `${w.files.length} file${w.files.length === 1 ? '' : 's'}` : '' },
          { label: 'Notify Assignee(s)', value: yes(w.notify) },
          { label: 'Notify Manager', value: yes(w.notifyManager) },
          { label: 'Notify Department', value: yes(w.notifyDepartment) },
          { label: 'Reminder', value: w.reminder && w.reminderDate ? this.date(w.reminderDate) : 'Not set' },
          { label: 'Custom Reference', value: w.reference },
        );
      } else {
        rows.push({ label: 'Purpose', value: w.target === 'Location' ? w.remarks || w.reason : w.reason });
        if (w.target === 'Other') rows.push({ label: 'Location', value: w.serviceLocation });
      }
    }
    return rows.map((r) => ({ ...r, value: r.value || '—' }));
  }

  /** Rows of the Assignment Details card on the review step. */
  get reviewRows(): { label: string; value: string }[] {
    const w = this.wizard;
    return [
      { label: 'Assign To', value: this.wizardAssignee },
      { label: 'Assignment Type', value: w.transfer },
      { label: 'From Date', value: this.date(w.from) },
      { label: 'To Date', value: w.openEnded ? 'Open Ended' : this.date(w.to) },
      { label: 'Purpose / Reason', value: w.reason },
      { label: 'Approval Reference', value: w.approvalRef },
    ].map((r) => ({ ...r, value: r.value || '—' }));
  }

  /** "Assign to IT Department" once the step is done. */
  stepSub(i: number): string {
    const w = this.wizard;
    if (i === 0 && w.tags.length) return `${w.tags.length} asset${w.tags.length === 1 ? '' : 's'} selected`;
    if (i === 1 && w.step > 2 && this.wizardAssignee) return `Assign to ${this.wizardAssignee}`;
    return this.steps[i].sub;
  }

  // ===== Steps =====

  /** Checks a step; returns the first problem, or '' when it's complete. */
  private stepError(step: number): string {
    const w = this.wizard;
    if (step === 1) return w.tags.length ? '' : 'Select at least one asset.';
    if (step === 2) {
      if (w.target === 'Other') {
        if (!w.entity) return `Choose the ${w.entityKind.toLowerCase()}.`;
        if (!w.responsible.trim()) return 'Enter the responsible person at the entity.';
      } else if (!w.assignee) return `Choose the ${this.targetLabels[w.target].toLowerCase()} to assign to.`;
      if (!w.transfer) return 'Choose the assignment type.';
      if (!w.from) return 'Choose the from date.';
      if (!w.openEnded && !w.to) return 'Choose the to date, or tick Open Ended.';
      if (!w.openEnded && w.to < w.from) return 'The to date must be on or after the from date.';
      if (w.expectedReturn && w.expectedReturn < w.from) return 'The expected return date must be on or after the from date.';
      if (w.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(w.email)) return 'Enter a valid contact email.';
    }
    if (step === 3) {
      if (w.reminder && !w.reminderDate) return 'Choose the reminder date, or untick Set a reminder.';
      if (w.reminder && w.reminderDate < isoDate(new Date())) return 'The reminder date must be today or later.';
    }
    return '';
  }

  /** Next checks every step on the way; the stepper tabs (validate = false) switch freely, and Confirm checks everything. */
  goStep(step: number, validate = true): void {
    const w = this.wizard;
    for (let s = w.step; validate && s < step; s++) {
      const error = this.stepError(s);
      if (error) {
        w.step = s;
        this.wizardError = error;
        return;
      }
    }
    this.wizardError = '';
    w.step = step;
    if (step !== 1) this.stopCamera();
    else if (w.selectMode === 'scan' && this.scanSource === 'camera') this.startCamera();
  }

  wizardBack(): void {
    const w = this.wizard;
    if (w.step === 1) return void this.router.navigate(['/assets/assignment']);
    this.goStep(w.step - 1);
  }

  // ===== Attachments =====

  addFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) this.takeFiles(Array.from(input.files));
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver = false;
    this.takeFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  private takeFiles(files: File[]): void {
    const w = this.wizard;
    const rejected: string[] = [];
    for (const f of files) {
      if (!ACCEPTED_FILES.includes(this.ext(f.name)) || f.size > MAX_FILE_BYTES) rejected.push(f.name);
      else if (!w.files.some((x) => x.name === f.name)) w.files.push({ name: f.name, size: f.size, added: isoDate(new Date()), file: f });
    }
    this.fileError = rejected.length ? `Not added (wrong type or over 10 MB): ${rejected.join(', ')}` : '';
  }

  removeFile(name: string): void {
    this.wizard.files = this.wizard.files.filter((f) => f.name !== name);
  }

  downloadFile(a: Attachment): void {
    if (!a.file) {
      if (a.stored?.fileUrl) window.open(a.stored.fileUrl, '_blank', 'noopener');
      return;
    }
    const url = URL.createObjectURL(a.file);
    const link = document.createElement('a');
    link.href = url;
    link.download = a.name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url));
  }

  ext(name: string): string {
    return name.split('.').pop()?.toLowerCase() ?? '';
  }

  fileIcon(name: string): typeof FileText {
    const e = this.ext(name);
    return e === 'xls' || e === 'xlsx' ? FileSpreadsheet : e === 'jpg' || e === 'jpeg' || e === 'png' ? ImageIcon : FileText;
  }

  fileSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  fileDate(d: string): string {
    return this.date(d);
  }

  // ===== Save =====

  /** Saves the wizard as a draft on the server and returns to the list; the page reopens with it next time. */
  saveDraft(): void {
    const w = this.wizard;
    if (this.saving) return;
    if (!w.tags.length) {
      w.step = 1;
      this.wizardError = 'Select at least one asset.';
      return;
    }
    this.save(true, 'Assignment saved as draft.');
  }

  /** Creates the assignment; assets already assigned elsewhere are handed over (their old assignment ends). */
  confirmWizard(): void {
    const w = this.wizard;
    if (this.saving) return;
    for (const s of [1, 2, 3]) {
      const error = this.stepError(s);
      if (error) {
        w.step = s;
        this.wizardError = error;
        return;
      }
    }
    const lookupError = this.targetError();
    if (lookupError) {
      w.step = 2;
      this.wizardError = lookupError;
      return;
    }
    const n = w.tags.length;
    this.save(false, `${n} asset${n === 1 ? '' : 's'} assigned to ${this.wizardAssignee}.`);
  }

  /** The picked employee, department or site must exist in master data, since the API stores its id. */
  private targetError(): string {
    const w = this.wizard;
    if (w.target === 'Employee' && !this.employee) return `Employee '${w.assignee}' was not found in master data.`;
    if (w.target === 'Department' && !this.lookups.departments.some((d) => d.name === w.assignee)) return `Department '${w.assignee}' was not found in master data.`;
    if (w.target === 'Location' && !this.lookups.sites.some((x) => x.name === w.site)) return `Site '${w.site}' was not found in master data.`;
    return '';
  }

  /** Uploads new attachments, ends the handed-over assets' assignments (when confirming), then creates or updates the assignment. */
  private save(draft: boolean, message: string): void {
    const w = this.wizard;
    this.saving = true;
    this.wizardError = '';
    const uploads: Observable<AssignmentAttachmentDto>[] = w.files.map((f) => (f.stored ? of(f.stored) : this.api.uploadFile(f.file!)));
    const handovers = draft ? [] : this.chosen.filter((p) => p.current && p.current.assignmentId !== w.draftId).map((p) => p.current!);
    (uploads.length ? forkJoin(uploads) : of([] as AssignmentAttachmentDto[]))
      .pipe(
        map((attachments) => this.buildRequest(attachments)),
        switchMap((request) => this.api.returnRows(handovers, `Handed over to ${this.wizardAssignee}`).pipe(map(() => request))),
        switchMap((request) => (w.draftId ? this.api.update(w.draftId, request, draft) : this.api.create(request, draft))),
        finalize(() => (this.saving = false)),
      )
      .subscribe({
        next: () => {
          this.stopCamera();
          this.router.navigate(['/assets/assignment'], { state: { toast: message } });
        },
        error: (err) => (this.wizardError = apiErrorMessage(err, 'The assignment could not be saved.')),
      });
  }

  /** The wizard as an API request. */
  private buildRequest(attachments: AssignmentAttachmentDto[]): AssetAssignmentRequest {
    const w = this.wizard;
    const text = (v: string) => v.trim() || null;
    const requestor = this.lookups.employees.find((e) => e.name === w.requestor);
    const manager = this.manager;
    const employee = w.target === 'Employee' ? employeeRequest(this.employee, w.assignee) : null;
    if (employee && manager && !employee.manager) employee.manager = { id: null, code: manager.id || null, name: manager.name, designation: manager.title || null };
    return {
      selectionMode: w.selectMode === 'multiple' ? 'Multiple' : w.selectMode === 'scan' ? 'Scan' : 'Single',
      assetRegistrationIds: this.chosen.map((p) => p.assetRegistrationId),
      assignTo: assignToOf(w.target),
      assignmentType: text(w.transfer),
      fromDate: w.from || null,
      toDate: w.openEnded ? null : w.to || null,
      isOpenEnded: w.openEnded,
      employee,
      department: w.target === 'Department' ? departmentRequest(this.lookups.departments.find((d) => d.name === w.assignee), w.assignee) : null,
      otherEntity:
        w.target === 'Other'
          ? {
              entityType: w.entityKind, entityId: null, entityName: w.entity, contactPerson: text(w.contact), contactPhone: text(w.phone), contactEmail: text(w.email),
              agreementReference: text(w.agreement), reason: text(w.reason), serviceType: text(w.serviceType), serviceRequestNumber: text(w.workOrder),
              serviceLocation: text(w.serviceLocation), expectedReturnDate: w.expectedReturn || null, returnCondition: text(w.returnCondition),
              responsiblePerson: w.responsible.trim() ? { id: null, code: null, name: w.responsible.trim(), designation: null } : null,
            }
          : null,
      location: w.target === 'Other' ? null : locationFrom(this.lookups, { site: w.site, building: w.building, floor: w.floor, room: w.room }),
      costCenter: text(w.costCenter),
      purpose: text(w.reason),
      approvalReference: text(w.approvalRef),
      remarks: text(w.remarks),
      notes: text(w.notes),
      attachments,
      notification: { notifyAssignees: w.notify, message: w.notify ? text(w.notifyMessage) : null, notifyManager: w.notifyManager, notifyDepartment: w.notifyDepartment },
      reminder: { isEnabled: w.reminder, reminderDate: w.reminder ? w.reminderDate || null : null, message: w.reminder ? text(w.reminderMessage) : null },
      customFields: { assignmentReference: text(w.reference), requestorId: requestor?.mdmId ?? null, requestorName: text(w.requestor), priority: text(w.priority), assignmentReason: text(w.customReason) },
      createdBy: this.api.user || null,
      clientId: null,
      tenantId: null,
    };
  }
}
