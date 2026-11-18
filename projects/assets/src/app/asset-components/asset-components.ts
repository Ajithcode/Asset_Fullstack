import { Component, HostListener, inject } from '@angular/core';
import { Observable, concat, forkJoin, last, map, of, switchMap } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Battery,
  Box,
  Cable,
  ChevronDown,
  ArrowLeft,
  ChevronLeft,
  ChevronRight,
  CircleGauge,
  CirclePlus,
  CircuitBoard,
  Cog,
  Component as ComponentIcon,
  Cpu,
  Cylinder,
  Droplets,
  EllipsisVertical,
  Eye,
  Fan,
  FileText,
  Fuel,
  Gauge,
  History,
  Inbox,
  Paperclip,
  Plus,
  Puzzle,
  Search,
  Settings,
  Snowflake,
  SquarePen,
  Thermometer,
  Trash2,
  Waves,
  Wind,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import { ComponentMaintenance, type MaintenanceData } from './component-maintenance/component-maintenance';
import { ComponentServiceHistory, type ServiceRecord } from './component-service-history/component-service-history';
import { ComponentDocuments, type ComponentDocument } from './component-documents/component-documents';
import { ComponentAttachments, type ComponentAttachment } from './component-attachments/component-attachments';
import { ComponentOverview } from './component-overview/component-overview';
import { AssetComponentsService, isoDate, type AssetComponentDto, type AssetListItemDto, type SaveAssetComponentRequest } from './asset-components.service';
import { apiErrorMessage } from '../services/asset-api';

type ComponentStatus = 'Active' | 'Under Maintenance' | 'Needs Replacement' | 'Inactive';
type LifeUnit = 'hrs' | 'months';
type AssetTab = 'overview' | 'components' | 'maintenance' | 'service' | 'documents' | 'attachments';

interface TabMeta {
  title: string;
  subtitle: string;
  scope: 'asset' | 'component';
}

export interface AssetComponent {
  /** Client-side id (stable per API component for the session). */
  id: number;
  /** AssetComponent id in the Asset API. */
  apiId?: string;
  /** Registration id (e.g. AR000001) of the asset the component belongs to. */
  assetRegistrationId?: string;
  /** API id of the parent component; null for top-level components. */
  parentApiId?: string | null;
  name: string;
  tag: string;
  model: string;
  manufacturer: string;
  partNumber: string;
  serialNumber: string;
  /** ISO dates (yyyy-mm-dd); empty when unknown. */
  installedDate: string;
  expectedReplacement: string;
  /** Life used so far and the rated life, for the life-cycle bar. 0 total = not tracked. */
  lifeUsed: number;
  lifeTotal: number;
  lifeUnit: LifeUnit;
  status: ComponentStatus;
  description: string;
  icon: keyof AssetComponents['componentIcons'];
  children: AssetComponent[];
  /** Maintenance plan and history, created on first visit to the Maintenance tab. */
  maintenance?: MaintenanceData;
  /** Service log, created on first visit to the Service History tab. */
  serviceHistory?: ServiceRecord[];
  /** Documents, created on first visit to the Documents tab. */
  documents?: ComponentDocument[];
  /** Attachments, created on first visit to the Attachments tab. */
  attachments?: ComponentAttachment[];
}

interface AssetRecord {
  /** Registration id (e.g. AR000001) used by the components API. */
  assetRegistrationId: string;
  name: string;
  tag: string;
  type: string;
  model: string;
  manufacturer: string;
  location: string;
  area: string;
  custodian: string;
  status: string;
  icon: keyof AssetComponents['componentIcons'];
  components: AssetComponent[];
}

type ComponentForm = Omit<AssetComponent, 'id' | 'children' | 'icon'> & { parentId: number | null };

/** Stable numeric ids for API components, so the selection survives reloads. */
const NUMERIC_IDS = new Map<string, number>();
let nextId = 1;

function numericIdOf(apiId: string): number {
  let id = NUMERIC_IDS.get(apiId);
  if (id === undefined) {
    id = nextId++;
    NUMERIC_IDS.set(apiId, id);
  }
  return id;
}

/** Best guess at an icon from a component's name. */
export function guessComponentIcon(name: string): AssetComponent['icon'] {
  const n = name.toLowerCase();
  if (n.includes('fuel')) return 'fuel';
  if (n.includes('oil') || n.includes('pump')) return 'oil';
  if (n.includes('filter')) return 'filter';
  if (n.includes('battery')) return 'battery';
  if (n.includes('turbo')) return 'turbo';
  if (n.includes('fan')) return 'fan';
  if (n.includes('radiator')) return 'radiator';
  if (n.includes('cooling') || n.includes('chiller')) return 'cooling';
  if (n.includes('engine') || n.includes('compressor') || n.includes('motor')) return 'engine';
  if (n.includes('alternator')) return 'alternator';
  if (n.includes('panel')) return 'panel';
  if (n.includes('module') || n.includes('controller')) return 'cpu';
  if (n.includes('control')) return 'control';
  if (n.includes('avr') || n.includes('board') || n.includes('regulator')) return 'board';
  if (n.includes('enclosure') || n.includes('housing') || n.includes('canopy')) return 'enclosure';
  if (n.includes('cable') || n.includes('wiring')) return 'cable';
  if (n.includes('generator')) return 'generator';
  return 'generic';
}

const STATUSES: ComponentStatus[] = ['Active', 'Under Maintenance', 'Needs Replacement', 'Inactive'];

function toStatus(value: string | null): ComponentStatus {
  return STATUSES.find((s) => s.toLowerCase() === (value ?? '').trim().toLowerCase()) ?? 'Active';
}

function toLifeUnit(value: string | null): LifeUnit {
  return (value ?? '').toLowerCase().includes('month') ? 'months' : 'hrs';
}

const LIFE_UNIT_API: Record<LifeUnit, string> = { hrs: 'Running hours', months: 'Months' };

/** Builds the component tree of an asset from the API's flat list. */
function buildTree(dtos: AssetComponentDto[], previous: AssetComponent[]): AssetComponent[] {
  // Keep the in-session maintenance / service data of components that are still there.
  const old = new Map<string, AssetComponent>();
  const visit = (list: AssetComponent[]) => list.forEach((c) => (c.apiId && old.set(c.apiId, c), visit(c.children)));
  visit(previous);

  const nodes = new Map<string, AssetComponent>();
  for (const d of dtos) {
    const before = old.get(d.id);
    nodes.set(d.id, {
      id: numericIdOf(d.id),
      apiId: d.id,
      assetRegistrationId: d.assetRegistrationId,
      parentApiId: d.parentComponentId,
      name: d.componentName,
      tag: d.componentTag,
      model: d.model ?? '',
      manufacturer: d.manufacturer ?? '',
      partNumber: d.partNumber ?? '',
      serialNumber: d.serialNumber ?? '',
      installedDate: isoDate(d.installedDate),
      expectedReplacement: isoDate(d.expectedReplacementDate),
      lifeUsed: Number(d.lifecycleUsed) || 0,
      lifeTotal: Number(d.lifecycleRatedLife) || 0,
      lifeUnit: toLifeUnit(d.lifecycleUnit),
      status: toStatus(d.status),
      description: d.description ?? '',
      icon: guessComponentIcon(d.componentName),
      children: [],
      maintenance: before?.maintenance,
      serviceHistory: before?.serviceHistory,
    });
  }
  const roots: AssetComponent[] = [];
  for (const node of nodes.values()) {
    const parent = node.parentApiId ? nodes.get(node.parentApiId) : undefined;
    if (parent && parent !== node) parent.children.push(node);
    else roots.push(node);
  }
  const byName = (a: AssetComponent, b: AssetComponent) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  const sortAll = (list: AssetComponent[]) => (list.sort(byName), list.forEach((c) => sortAll(c.children)));
  sortAll(roots);
  return roots;
}

function toAssetRecord(dto: AssetListItemDto): AssetRecord {
  const location = dto.locationPath || [dto.siteName, dto.buildingName].filter(Boolean).join(' - ');
  const area = [dto.floorName, dto.zoneName, dto.roomName].filter(Boolean).join(' / ');
  return {
    assetRegistrationId: dto.assetRegistrationId,
    name: dto.assetName || dto.assetTag || dto.assetRegistrationId,
    tag: dto.assetTag || dto.assetRegistrationId,
    type: dto.assetType || dto.category || '',
    model: '',
    manufacturer: dto.manufacturer ?? '',
    location,
    area,
    custodian: dto.custodianName ?? '',
    status: dto.status || 'Active',
    icon: guessComponentIcon(`${dto.assetType ?? ''} ${dto.category ?? ''} ${dto.assetName ?? ''}`),
    components: [],
  };
}

const EMPTY_ASSET: AssetRecord = {
  assetRegistrationId: '',
  name: '',
  tag: '',
  type: '',
  model: '',
  manufacturer: '',
  location: '',
  area: '',
  custodian: '',
  status: 'Active',
  icon: 'generic',
  components: [],
};

/**
 * Component trees loaded from the API, by asset tag, shared with the asset detail / maintenance tabs
 * (componentTreeOf / componentsOf). Filled whenever an asset's components are loaded.
 */
const TREE_CACHE = new Map<string, AssetComponent[]>();

/** The asset's top-level components (assemblies hold their parts in `children`); empty until loaded from the API. */
export function componentTreeOf(tag: string): AssetComponent[] {
  return TREE_CACHE.get(tag) ?? [];
}

/** The asset's components, assemblies followed by their parts; empty until loaded from the API. */
export function componentsOf(tag: string): AssetComponent[] {
  const flat = (list: AssetComponent[]): AssetComponent[] => list.flatMap((c) => [c, ...flat(c.children)]);
  return flat(componentTreeOf(tag));
}

/**
 * Loads an asset's components from the API by asset tag and fills the shared cache read by
 * componentTreeOf / componentsOf. Emits the top-level components (empty when the asset is not found).
 */
export function loadComponentTree(api: AssetComponentsService, tag: string): Observable<AssetComponent[]> {
  return api.listAssets(tag).pipe(
    map((assets) => assets.find((a) => a.assetTag === tag) ?? assets.find((a) => a.assetRegistrationId === tag) ?? null),
    switchMap((asset) => (asset ? api.listComponents(asset.assetRegistrationId) : of<AssetComponentDto[]>([]))),
    map((dtos) => {
      const tree = buildTree(dtos, componentTreeOf(tag));
      TREE_CACHE.set(tag, tree);
      return tree;
    }),
  );
}

/** Icon of each component kind (AssetComponent['icon']). */
export const COMPONENT_ICONS = {
  generator: Zap,
  engine: Settings,
  filter: Cylinder,
  fuel: Fuel,
  oil: Droplets,
  turbo: Fan,
  alternator: CircleGauge,
  board: CircuitBoard,
  cooling: Snowflake,
  radiator: Waves,
  fan: Wind,
  control: Gauge,
  panel: Thermometer,
  cpu: Cpu,
  battery: Battery,
  enclosure: Box,
  cable: Cable,
  generic: Cog,
};

const EMPTY_FORM: ComponentForm = {
  name: '',
  tag: '',
  model: '',
  manufacturer: '',
  partNumber: '',
  serialNumber: '',
  installedDate: '',
  expectedReplacement: '',
  lifeUsed: 0,
  lifeTotal: 0,
  lifeUnit: 'hrs',
  status: 'Active',
  description: '',
  parentId: null,
};

const NOW = new Date();
const TODAY_ISO = `${NOW.getFullYear()}-${String(NOW.getMonth() + 1).padStart(2, '0')}-${String(NOW.getDate()).padStart(2, '0')}`;

@Component({
  selector: 'app-asset-components',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, ComponentMaintenance, ComponentServiceHistory, ComponentDocuments, ComponentAttachments, ComponentOverview],
  templateUrl: './asset-components.html',
  styleUrl: './asset-components.css',
})
export class AssetComponents {
  readonly icons = {
    ArrowLeft, ChevronDown, ChevronLeft, ChevronRight, CirclePlus, EllipsisVertical, Eye, FileText, History, Inbox, Paperclip,
    Plus, Puzzle, Search, SquarePen, Trash2, Wrench, X, ComponentIcon,
  };

  readonly componentIcons = COMPONENT_ICONS;

  readonly tabs: { key: AssetTab; label: string; icon: typeof CirclePlus }[] = [
    { key: 'overview', label: 'Overview', icon: CirclePlus },
    { key: 'components', label: 'Components', icon: Puzzle },
    { key: 'maintenance', label: 'Maintenance', icon: Wrench },
    { key: 'service', label: 'Service History', icon: History },
    { key: 'documents', label: 'Documents', icon: FileText },
    { key: 'attachments', label: 'Attachments', icon: Paperclip },
  ];

  readonly statusOptions: ComponentStatus[] = ['Active', 'Under Maintenance', 'Needs Replacement', 'Inactive'];
  readonly pageSizeOptions = [10, 25, 50];

  private readonly api = inject(AssetComponentsService);

  assets: AssetRecord[] = [];
  assetTag = '';
  activeTab: AssetTab = 'components';

  expanded = new Set<number>();
  /** Tree node whose children fill the table; null means the asset itself (top-level components). */
  groupId: number | null = null;
  /** Component shown in the details panel. */
  detailId: number | null = null;
  /** Component the component-level tabs (Maintenance, Service History, Documents, Attachments) are showing. */
  focusId: number | null = null;

  search = '';
  statusFilter: '' | ComponentStatus = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  openMenu: number | null = null;

  formOpen = false;
  editing: AssetComponent | null = null;
  form: ComponentForm = { ...EMPTY_FORM };
  formSubmitted = false;

  deleteTarget: AssetComponent | null = null;

  /** Loading the asset list or the selected asset's components. */
  loading = false;
  /** Error from loading; shown inline above the page content. */
  loadError = '';
  /** A create / update / delete / status change is in flight. */
  saving = false;
  /** Error from the last save, shown in the add / edit form. */
  formError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private readonly router: Router,
    route: ActivatedRoute,
  ) {
    this.loadAssets(route.snapshot.queryParamMap.get('tag'));
  }

  // ===== Asset =====

  get asset(): AssetRecord {
    return this.assets.find((a) => a.tag === this.assetTag) ?? this.assets[0] ?? EMPTY_ASSET;
  }

  onAssetChange(): void {
    this.router.navigate([], { queryParams: { tag: this.assetTag }, replaceUrl: true });
    this.loadComponents(true);
  }

  /** Loads the asset picker, then the requested asset's components (the first asset when none is requested). */
  loadAssets(requested: string | null = this.assetTag || null): void {
    this.loading = true;
    this.loadError = '';
    this.api
      .listAssets()
      .pipe(
        switchMap((list) =>
          // The requested asset may be outside the first page of the picker; look it up by tag.
          requested && !list.some((a) => a.assetTag === requested || a.assetRegistrationId === requested)
            ? this.api.listAssets(requested).pipe(map((found) => [...found.filter((a) => a.assetTag === requested || a.assetRegistrationId === requested), ...list]))
            : of(list),
        ),
      )
      .subscribe({
        next: (list: AssetListItemDto[]) => {
          this.assets = list.map(toAssetRecord);
          const match = this.assets.find((a) => a.tag === requested || a.assetRegistrationId === requested);
          this.assetTag = match?.tag ?? this.assets[0]?.tag ?? '';
          if (!this.assets.length) {
            this.loading = false;
            this.resetSelection();
            return;
          }
          this.loadComponents(true);
        },
        error: (err) => {
          this.loading = false;
          this.loadError = apiErrorMessage(err, 'Could not load assets.');
        },
      });
  }

  /**
   * Loads the selected asset's components from the API.
   * @param reset true when switching assets: opens the first assembly instead of keeping the current selection.
   */
  loadComponents(reset = false, done?: () => void): void {
    const asset = this.asset;
    if (!asset.assetRegistrationId) return;
    this.loading = true;
    this.loadError = '';
    if (!asset.model) {
      this.api.getAssetBasics(asset.assetRegistrationId).subscribe({
        next: (b) => {
          asset.model = b.model;
          asset.manufacturer ||= b.manufacturer;
        },
        error: () => undefined,
      });
    }
    this.api.listComponents(asset.assetRegistrationId).subscribe({
      next: (dtos) => {
        asset.components = buildTree(dtos, asset.components);
        TREE_CACHE.set(asset.tag, asset.components);
        this.loading = false;
        if (reset) {
          this.resetSelection();
        } else {
          // Drop selections that no longer exist.
          if (this.find(this.groupId) === null) this.groupId = null;
          if (this.find(this.detailId) === null) this.detailId = null;
          if (this.find(this.focusId) === null) this.focusId = this.detailId ?? this.asset.components[0]?.id ?? null;
          [...this.checked].forEach((id) => this.find(id) === null && this.checked.delete(id));
        }
        done?.();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, `Could not load the components of ${asset.tag}.`);
      },
    });
  }

  retryLoad(): void {
    if (this.assets.length) this.loadComponents(true);
    else this.loadAssets();
  }

  /** Opens the first assembly and shows its first part, as in the design. */
  private resetSelection(): void {
    this.expanded.clear();
    this.checked.clear();
    this.search = '';
    this.statusFilter = '';
    this.page = 1;
    const firstGroup = this.asset.components.find((c) => c.children.length);
    this.asset.components.filter((c) => c.children.length).forEach((c) => {
      if (c === firstGroup || c.icon === 'control') this.expanded.add(c.id);
    });
    this.groupId = firstGroup?.id ?? null;
    this.detailId = firstGroup?.children[0]?.id ?? this.asset.components[0]?.id ?? null;
    this.focusId = firstGroup?.id ?? this.detailId;
  }

  // ===== Tabs =====

  /**
   * Header text for every tab except Components. `scope` says whether the tab summarises the whole
   * asset or works on one component at a time (those show the component picker in the summary card).
   */
  private readonly tabMetas: Partial<Record<AssetTab, TabMeta>> = {
    overview: { title: 'Overview', subtitle: 'View component structure, health, and key details for the selected asset', scope: 'asset' },
    maintenance: { title: 'Maintenance', subtitle: 'Manage maintenance activities, schedules and history for asset components', scope: 'component' },
    service: { title: 'Service History', subtitle: 'View and manage service history for asset components', scope: 'component' },
    documents: { title: 'Documents', subtitle: 'Manage documents related to asset components', scope: 'component' },
    attachments: { title: 'Attachments', subtitle: 'Manage files, images and other attachments for asset components', scope: 'component' },
  };

  get tabMeta(): TabMeta | null {
    return this.tabMetas[this.activeTab] ?? null;
  }

  get isComponentScoped(): boolean {
    return this.tabMeta?.scope === 'component';
  }

  openTab(tab: AssetTab, focus?: AssetComponent | null): void {
    if (focus) {
      this.focusId = focus.id;
    } else if (this.tabMetas[tab]?.scope === 'component' && this.tabMetas[this.activeTab]?.scope !== 'component') {
      // Component-level tabs follow whatever component was being looked at on the Components tab.
      this.focusId = this.detailId ?? this.groupId ?? this.asset.components[0]?.id ?? null;
    }
    this.openMenu = null;
    this.activeTab = tab;
  }

  /** From the Overview: show a component on the Components tab. */
  showComponent(c: AssetComponent): void {
    this.selectNode(c);
    this.activeTab = 'components';
  }

  /** Bound for child components, so their trees use the same icons as this page. */
  readonly iconForFn = (c: AssetComponent) => this.iconFor(c);

  get focusComponent(): AssetComponent | null {
    return this.find(this.focusId) ?? this.asset.components[0] ?? null;
  }

  /** Every component on the asset, indented by depth, for the Maintenance tab's component picker. */
  get componentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (nodes: AssetComponent[], depth: number) => {
      for (const n of nodes) {
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name} (${n.tag})` });
        visit(n.children, depth + 1);
      }
    };
    visit(this.asset.components, 0);
    return out;
  }

  /** Opens the Components tab with the maintenance component selected. */
  viewComponentDetails(): void {
    const c = this.focusComponent;
    if (c) this.selectNode(c);
    this.activeTab = 'components';
  }

  viewAssetDetails(): void {
    this.router.navigate(['/assets/list'], { queryParams: { tag: this.asset.tag } });
  }

  // ===== Tree helpers =====

  private walk(nodes: AssetComponent[], fn: (n: AssetComponent, parent: AssetComponent | null) => void, parent: AssetComponent | null = null): void {
    for (const n of nodes) {
      fn(n, parent);
      this.walk(n.children, fn, n);
    }
  }

  find(id: number | null): AssetComponent | null {
    if (id === null) return null;
    let found: AssetComponent | null = null;
    this.walk(this.asset.components, (n) => {
      if (n.id === id) found = n;
    });
    return found;
  }

  parentOf(id: number): AssetComponent | null {
    let parent: AssetComponent | null = null;
    this.walk(this.asset.components, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  private isInSubtree(candidate: AssetComponent, ancestor: AssetComponent): boolean {
    let inside = false;
    this.walk([ancestor], (n) => {
      if (n.id === candidate.id) inside = true;
    });
    return inside;
  }

  get totalComponents(): number {
    let count = 0;
    this.walk(this.asset.components, () => count++);
    return count;
  }

  // ===== Tree interaction =====

  toggleExpand(n: AssetComponent, event?: Event): void {
    event?.stopPropagation();
    if (this.expanded.has(n.id)) this.expanded.delete(n.id);
    else this.expanded.add(n.id);
  }

  /** Assemblies open in the table; single parts are shown in the table of their parent. */
  selectNode(n: AssetComponent | null): void {
    this.openMenu = null;
    this.checked.clear();
    this.search = '';
    this.statusFilter = '';
    this.page = 1;
    if (!n) {
      this.groupId = null;
      this.detailId = null;
      return;
    }
    if (n.children.length) {
      this.groupId = n.id;
      this.expanded.add(n.id);
    } else {
      this.groupId = this.parentOf(n.id)?.id ?? null;
    }
    this.detailId = n.id;
  }

  get group(): AssetComponent | null {
    return this.find(this.groupId);
  }

  get detail(): AssetComponent | null {
    return this.find(this.detailId);
  }

  // ===== Table =====

  get groupChildren(): AssetComponent[] {
    return this.group ? this.group.children : this.asset.components;
  }

  get filtered(): AssetComponent[] {
    const q = this.search.trim().toLowerCase();
    return this.groupChildren.filter(
      (c) =>
        (!q || [c.name, c.tag, c.model, c.manufacturer, c.partNumber, c.serialNumber].some((v) => v.toLowerCase().includes(q))) &&
        (!this.statusFilter || c.status === this.statusFilter),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): AssetComponent[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filtered.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((r) => this.checked.has(r.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((r) => (on ? this.checked.add(r.id) : this.checked.delete(r.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  setStatusForChecked(status: ComponentStatus): void {
    const rows = this.groupChildren.filter((c) => this.checked.has(c.id) && c.apiId);
    if (!rows.length || this.saving) return;
    this.saving = true;
    forkJoin(rows.map((c) => this.api.updateComponent(this.asset.assetRegistrationId, c.apiId!, this.requestOf(c, { status })))).subscribe({
      next: () => {
        this.saving = false;
        this.checked.clear();
        this.loadComponents();
        this.showToast(`${rows.length} component${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
      },
      error: (err) => {
        this.saving = false;
        this.loadComponents();
        this.showToast(apiErrorMessage(err, 'Could not change the status.'));
      },
    });
  }

  setStatus(c: AssetComponent, status: ComponentStatus): void {
    this.openMenu = null;
    if (!c.apiId || this.saving) return;
    this.saving = true;
    this.api.updateComponent(this.asset.assetRegistrationId, c.apiId, this.requestOf(c, { status })).subscribe({
      next: () => {
        this.saving = false;
        c.status = status;
        this.showToast(`${c.name} marked ${status.toLowerCase()}.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, `Could not change the status of ${c.name}.`));
      },
    });
  }

  /** Save request for an existing component, with optional changes. */
  private requestOf(c: AssetComponent, changes: Partial<AssetComponent> = {}): SaveAssetComponentRequest {
    const v = { ...c, ...changes };
    return {
      parentComponentId: this.parentOf(c.id)?.apiId ?? null,
      componentName: v.name,
      componentTag: v.tag,
      status: v.status,
      model: v.model || null,
      manufacturer: v.manufacturer || null,
      partNumber: v.partNumber || null,
      serialNumber: v.serialNumber || null,
      installedDate: v.installedDate || null,
      expectedReplacementDate: v.expectedReplacement || null,
      lifecycleUsed: v.lifeUsed,
      lifecycleRatedLife: v.lifeTotal,
      lifecycleUnit: LIFE_UNIT_API[v.lifeUnit],
      description: v.description || null,
      createdBy: null,
      clientId: '',
      tenantId: '',
    };
  }

  // ===== Display helpers =====

  iconFor(c: AssetComponent) {
    return this.componentIcons[c.icon];
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  lifePercent(c: AssetComponent): number {
    return c.lifeTotal ? Math.min(100, Math.round((c.lifeUsed / c.lifeTotal) * 100)) : 0;
  }

  lifeLevel(c: AssetComponent): 'ok' | 'warn' | 'bad' {
    const p = this.lifePercent(c);
    return p >= 90 ? 'bad' : p >= 70 ? 'warn' : 'ok';
  }

  isReplacementDue(c: AssetComponent): boolean {
    return !!c.expectedReplacement && c.expectedReplacement <= TODAY_ISO;
  }

  // ===== Menus =====

  toggleMenu(id: number, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === id ? null : id;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.formOpen = false;
    this.deleteTarget = null;
  }

  // ===== Add / edit =====

  /** Every component as an indented list for the "Parent Component" picker. */
  get parentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (nodes: AssetComponent[], depth: number) => {
      for (const n of nodes) {
        if (this.editing && this.isInSubtree(n, this.editing)) continue;
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
        visit(n.children, depth + 1);
      }
    };
    visit(this.asset.components, 0);
    return out;
  }

  openAdd(): void {
    this.openMenu = null;
    this.editing = null;
    this.form = { ...EMPTY_FORM, parentId: this.groupId, manufacturer: this.asset.manufacturer, installedDate: TODAY_ISO };
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  openEdit(c: AssetComponent | null = this.detail): void {
    this.openMenu = null;
    if (!c) return;
    this.editing = c;
    this.form = {
      name: c.name,
      tag: c.tag,
      model: c.model,
      manufacturer: c.manufacturer,
      partNumber: c.partNumber,
      serialNumber: c.serialNumber,
      installedDate: c.installedDate,
      expectedReplacement: c.expectedReplacement,
      lifeUsed: c.lifeUsed,
      lifeTotal: c.lifeTotal,
      lifeUnit: c.lifeUnit,
      status: c.status,
      description: c.description,
      parentId: this.parentOf(c.id)?.id ?? null,
    };
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  isTagTaken(tag: string): boolean {
    const t = tag.trim().toUpperCase();
    if (!t) return false;
    let taken = false;
    this.walk(this.asset.components, (n) => {
      if (n !== this.editing && n.tag.toUpperCase() === t) taken = true;
    });
    return taken;
  }

  get lifeInvalid(): boolean {
    return this.form.lifeUsed < 0 || this.form.lifeTotal < 0 || (this.form.lifeTotal > 0 && this.form.lifeUsed > this.form.lifeTotal);
  }

  get datesInvalid(): boolean {
    return !!this.form.installedDate && !!this.form.expectedReplacement && this.form.expectedReplacement < this.form.installedDate;
  }

  saveForm(): void {
    this.formSubmitted = true;
    this.formError = '';
    const f = this.form;
    if (this.saving || !f.name.trim() || !f.tag.trim() || this.isTagTaken(f.tag) || this.lifeInvalid || this.datesInvalid) return;

    const parent = this.find(f.parentId);
    const body: SaveAssetComponentRequest = {
      parentComponentId: parent?.apiId ?? null,
      componentName: f.name.trim(),
      componentTag: f.tag.trim().toUpperCase(),
      status: f.status,
      model: f.model.trim() || null,
      manufacturer: f.manufacturer.trim() || null,
      partNumber: f.partNumber.trim() || null,
      serialNumber: f.serialNumber.trim() || null,
      installedDate: f.installedDate || null,
      expectedReplacementDate: f.expectedReplacement || null,
      lifecycleUsed: Number(f.lifeUsed) || 0,
      lifecycleRatedLife: Number(f.lifeTotal) || 0,
      lifecycleUnit: LIFE_UNIT_API[f.lifeUnit],
      description: f.description.trim() || null,
      createdBy: null,
      clientId: '',
      tenantId: '',
    };
    const editing = this.editing;
    const assetId = this.asset.assetRegistrationId;
    const request$ = editing?.apiId ? this.api.updateComponent(assetId, editing.apiId, body) : this.api.createComponent(assetId, body);

    this.saving = true;
    request$.subscribe({
      next: (saved) => {
        this.saving = false;
        this.formOpen = false;
        const savedId = numericIdOf(saved.id);
        this.loadComponents(false, () => {
          if (parent) this.expanded.add(parent.id);
          this.groupId = parent?.id ?? null;
          if (!editing) this.detailId = savedId;
        });
        this.showToast(editing ? `${body.componentName} updated.` : `${body.componentName} added${parent ? ` to ${parent.name}` : ''}.`);
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, editing ? 'Could not save the component.' : 'Could not add the component.');
      },
    });
  }

  // ===== Remove =====

  askDelete(c: AssetComponent): void {
    this.openMenu = null;
    this.deleteTarget = c;
  }

  /** Removes the component and its sub-components (the API only deletes leaves, so children go first). */
  confirmDelete(): void {
    const c = this.deleteTarget;
    if (!c || this.saving) return;
    const order: AssetComponent[] = [];
    const visit = (n: AssetComponent) => {
      n.children.forEach(visit);
      order.push(n);
    };
    visit(c);
    const assetId = this.asset.assetRegistrationId;
    const parent = this.parentOf(c.id);
    this.saving = true;
    concat(...order.filter((n) => n.apiId).map((n) => this.api.deleteComponent(assetId, n.apiId!)))
      .pipe(last(undefined, undefined))
      .subscribe({
        next: () => {
          this.saving = false;
          this.deleteTarget = null;
          this.checked.delete(c.id);
          // Fall back to the parent if the removed branch held the current selection.
          this.loadComponents(false, () => {
            if (this.groupId === null && parent && this.find(parent.id)) this.groupId = parent.id;
          });
          this.showToast(`${c.name} removed.`);
        },
        error: (err) => {
          this.saving = false;
          this.deleteTarget = null;
          this.loadComponents();
          this.showToast(apiErrorMessage(err, `Could not remove ${c.name}.`));
        },
      });
  }

  countDescendants(c: AssetComponent): number {
    let count = -1;
    this.walk([c], () => count++);
    return count;
  }

  trackById(_: number, c: AssetComponent): number {
    return c.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
