import { Component, ElementRef, HostListener, OnDestroy, ViewChild, inject } from '@angular/core';
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
import {
  ASSIGN_DRAFT, ASSIGNMENTS, Assignment, AssignmentType, AssetKind, CURRENT_ROLE, CURRENT_USER, DEPARTMENT_MANAGERS, DepartmentManager, EMPLOYEES, ENTITIES, ENTITY_KINDS,
  EntityKind, LOCATION_DETAILS, LocationDetails, PoolAsset, STORE_LOCATION, TARGET_LABELS, UNASSIGNED_ASSETS, assetFacts, assigneeChoices, assigneeDetails, isoDate, stamp,
  BUILDINGS, COST_CENTERS, Employee, FLOORS, ROOMS, SITES, SITE_TREE, SiteNode,
} from '../asset-assignment-data';

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
  file: File;
}

/** The Assign Asset wizard state. */
interface Wizard {
  step: number;
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

/** Pool asset with its current assignment and the facts shown when it's scanned or picked. */
interface PoolRow extends PoolAsset {
  current?: Assignment;
  brand: string;
  model: string;
  category: string;
  location: string;
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
export class AssignAsset implements OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowLeftRight, ArrowRight, Barcode, Bell, Building2, CalendarClock, CalendarDays, Camera, ChevronDown, ChevronLeft, ChevronRight, EllipsisVertical, ListFilter, Check, CircleCheck, CloudUpload, Download, FileSpreadsheet, FileText, Hexagon,
    ImageIcon, Info, Keyboard, LayoutGrid, Layers, Mail, MapPin, Paperclip, Pencil, Phone, Plus, ScanLine, Search, Settings, UserRound, Wrench, X, Zap,
  };
  readonly kindIcons: Record<AssetKind, typeof Laptop> = {
    laptop: Laptop, phone: Smartphone, scanner: ScanBarcode, projector: Projector, forklift: Forklift, tool: Wrench, card: CreditCard, monitor: Monitor, tablet: Tablet, camera: Cctv,
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
  readonly costCenters = COST_CENTERS;
  readonly sites = SITES;
  readonly buildings = BUILDINGS;
  readonly floors = FLOORS;
  readonly rooms = ROOMS;
  readonly siteTree = SITE_TREE;
  readonly serviceTypes = ['AMC Support', 'Warranty Repair', 'Calibration', 'Upgrade', 'Inspection', 'Installation'];
  readonly returnConditions = ['Same as Assigned', 'Serviced / Repaired', 'Upgraded', 'Not Applicable'];
  readonly priorities = ['Low', 'Normal', 'High', 'Urgent'];
  readonly requestors = [CURRENT_USER, ...EMPLOYEES.map((e) => e.name)];
  readonly acceptAttr = ACCEPTED_FILES.map((x) => '.' + x).join(',');
  readonly remarksMax = 500;
  readonly assigneeChoices = assigneeChoices;

  /** DEMO MODE: shared in-memory store, so assignments made here show in the list. */
  readonly assignments: Assignment[] = ASSIGNMENTS;
  readonly wizard: Wizard;
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

  constructor() {
    // ?type=Department preselects the target, ?assets=IT-000456,IT-000457 the assets
    const query = inject(ActivatedRoute).snapshot.queryParamMap;
    const type = query.get('type') as AssignmentType | null;
    const tags = (query.get('assets') ?? '').split(',').filter((t) => this.pool.some((p) => p.tag === t));
    const draft = ASSIGN_DRAFT.value as Wizard | undefined;
    if (draft && !type && !tags.length) {
      this.wizard = { ...draft, files: [...draft.files] };
      ASSIGN_DRAFT.value = undefined;
    } else if ((!type || type === 'Employee') && !tags.length) {
      this.wizard = this.demoWizard();
    } else {
      this.wizard = this.newWizard(type && this.assignmentTypes.includes(type) ? type : 'Employee', tags);
    }
  }

  /** DEMO MODE: opening the page directly prefills a laptop assignment to Arjun Mehta. */
  private demoWizard(): Wizard {
    const tag = 'IT-000457';
    const w: Wizard = {
      ...this.newWizard('Employee', []),
      tags: this.pool.some((p) => p.tag === tag) ? [tag] : [],
      reason: 'New Joiner',
      approvalRef: 'HR-2025-0012',
      from: '2025-01-01',
      openEnded: true,
      remarks: 'Assigned to Arjun Mehta for IT operations. Includes laptop, charger and carrying case.',
    };
    this.fillSiteFrom(w, 'Arjun Mehta');
    return w;
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }

  private newWizard(target: AssignmentType, tags: string[]): Wizard {
    const year = new Date().getFullYear();
    return {
      step: tags.length ? 2 : 1, tags, selectMode: tags.length > 1 ? 'multiple' : 'single', assetSearch: '', target, assignee: '', entityKind: 'Vendor / Supplier', entity: '',
      contact: '', phone: '', email: '', agreement: '', approvalRef: '', costCenter: '', site: '', building: '', floor: '', room: '', reason: '', transfer: TRANSFER_TYPES[target][0], from: isoDate(new Date()),
      to: '', openEnded: target !== 'Other', serviceType: '', workOrder: '', serviceLocation: '', expectedReturn: '', returnCondition: 'Same as Assigned', responsible: '',
      remarks: '', notes: '', files: [], notify: true, notifyMessage: 'Your asset(s) have been assigned to you. Please check the details in Asset360.', notifyManager: true,
      notifyDepartment: false, reminder: false, reminderDate: '', reminderMessage: '', reference: `ASSIGN-${year}-${String(this.assignments.length + 1).padStart(3, '0')}`,
      requestor: CURRENT_USER, priority: 'Normal', customReason: '',
    };
  }

  // ===== Asset selection =====

  /** Every known asset: those in the assignment list plus unassigned stock. */
  get pool(): PoolRow[] {
    const rows = new Map<string, PoolRow>();
    const row = (a: PoolAsset, current?: Assignment): PoolRow => ({
      assetName: a.assetName, assetType: a.assetType, kind: a.kind, tag: a.tag, serial: a.serial, current, ...assetFacts(a), location: current?.location || STORE_LOCATION,
    });
    for (const a of this.assignments) {
      const current = a.status === 'Returned' ? undefined : a;
      const known = rows.get(a.tag);
      if (!known || (!known.current && current)) rows.set(a.tag, row(a, current));
    }
    for (const u of UNASSIGNED_ASSETS) if (!rows.has(u.tag)) rows.set(u.tag, row(u));
    return [...rows.values()];
  }

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
    if (!p) {
      this.found = null;
      this.scanError = `No asset found for "${code.trim()}".`;
      return;
    }
    this.scanError = '';
    this.found = p;
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

  get employee(): Employee | undefined {
    const w = this.wizard;
    return w.target === 'Employee' ? EMPLOYEES.find((e) => e.name === w.assignee) : undefined;
  }

  /** Manager shown next to the employee or department. */
  get manager(): DepartmentManager | undefined {
    return this.reviewDepartment?.manager;
  }

  profileOpen = false;

  /** Picking an employee or department also fills in where the assets will sit. */
  onAssignee(name: string): void {
    const w = this.wizard;
    w.assignee = name;
    this.fillSiteFrom(w, name);
  }

  private fillSiteFrom(w: Wizard, name: string): void {
    w.assignee = name;
    const where = w.target === 'Employee' ? EMPLOYEES.find((e) => e.name === name)?.location : w.target === 'Department' ? DEPARTMENT_MANAGERS[name]?.location : undefined;
    const d = where ? LOCATION_DETAILS[where] : undefined;
    if (!d) return;
    w.costCenter = d.costCenter;
    w.site = d.site;
    w.building = d.building;
    w.floor = d.floor;
    w.room = d.room;
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
    const name = w.target === 'Department' ? w.assignee : w.target === 'Employee' ? EMPLOYEES.find((e) => e.name === w.assignee)?.department : '';
    return name ? { name, manager: DEPARTMENT_MANAGERS[name] } : null;
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

  /** Keeps the wizard as it is and returns to the list; the page reopens with it next time. */
  saveDraft(): void {
    this.stopCamera();
    ASSIGN_DRAFT.value = { ...this.wizard, files: [...this.wizard.files] };
    this.router.navigate(['/assets/assignment'], { state: { toast: 'Assignment saved as draft.' } });
  }

  /** Creates the assignments; assets already assigned elsewhere are handed over (their old assignment ends). */
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
    this.saving = true;
    const now = stamp(new Date());
    const target = assigneeDetails(w.target, this.wizardAssignee);
    if (w.target === 'Other') target.assigneeSub = w.entityKind;
    const extra = [
      { label: 'Assignment Type', value: w.transfer },
      { label: 'Purpose', value: w.reason },
      { label: 'Approval Reference', value: w.approvalRef },
      { label: 'Cost Center', value: w.costCenter },
      { label: 'Site', value: [w.site, w.building, w.floor, w.room].filter(Boolean).join(' · ') },
      { label: 'Reference', value: w.reference },
      { label: 'Requestor', value: w.requestor },
      { label: 'Priority', value: w.priority },
      { label: 'Assignment Reason', value: w.customReason },
      { label: 'Notifications', value: this.notifyLabel },
      { label: 'Reminder', value: w.reminder ? `${w.reminderDate}${w.reminderMessage ? ` · ${w.reminderMessage}` : ''}` : '' },
      { label: 'Documents', value: w.files.map((f) => f.name).join(', ') },
    ];
    if (w.target === 'Other') {
      extra.push(
        { label: w.entityKind, value: w.entity },
        { label: 'Contact Person', value: w.contact },
        { label: 'Phone', value: w.phone },
        { label: 'Email', value: w.email },
        { label: 'Agreement / PO', value: w.agreement },
        { label: 'Service Type', value: w.serviceType },
        { label: 'Service Request', value: w.workOrder },
        { label: 'Service Location', value: w.serviceLocation },
        { label: 'Expected Return', value: w.expectedReturn },
        { label: 'Return Condition', value: w.returnCondition },
        { label: 'Responsible', value: w.responsible },
      );
    }
    const kept = extra.filter((r) => r.value);
    let id = Math.max(0, ...this.assignments.map((a) => a.id));
    const created: Assignment[] = [];
    for (const p of this.chosen) {
      if (p.current) {
        p.current.history.push({ at: now, action: 'Assignment ended', by: CURRENT_USER, note: `Handed over to ${target.assignee}` });
        p.current.status = 'Returned';
        p.current.to = isoDate(new Date());
      }
      created.push({
        id: ++id, assetName: p.assetName, assetType: p.assetType, kind: p.kind, tag: p.tag, serial: p.serial, type: w.target, ...target, from: w.from, to: w.openEnded ? '' : w.to,
        status: 'Assigned', assignedBy: CURRENT_USER, assignedByRole: CURRENT_ROLE, assignedOn: now, remarks: [w.remarks.trim(), w.notes.trim()].filter(Boolean).join('\n'),
        extra: kept, history: [{ at: now, action: 'Assigned', by: CURRENT_USER, note: `Assigned to ${target.assignee}${p.current ? ` (from ${p.current.assignee})` : ''}` }],
      });
    }
    this.assignments.unshift(...created);
    this.stopCamera();
    const n = created.length;
    this.router.navigate(['/assets/assignment'], { state: { toast: `${n} asset${n === 1 ? '' : 's'} assigned to ${target.assignee}.` } });
  }
}
