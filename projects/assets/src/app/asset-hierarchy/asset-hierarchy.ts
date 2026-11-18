import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  BatteryCharging,
  BellRing,
  Boxes,
  Building,
  Building2,
  Check,
  ChevronDown,
  ChevronRight,
  ChevronsDownUp,
  ChevronsUpDown,
  CircuitBoard,
  Cog,
  Download,
  Droplets,
  Ellipsis,
  EllipsisVertical,
  Eye,
  Factory,
  Fan,
  Flame,
  Folder,
  FolderOpen,
  FolderPlus,
  Forklift,
  Gauge,
  Inbox,
  Layers,
  Link2,
  MoveRight,
  Network,
  Package,
  PackagePlus,
  Plus,
  Search,
  Server,
  SolarPanel,
  SquarePen,
  ToggleRight,
  Trash2,
  Warehouse,
  Workflow,
  X,
  Zap,
} from 'lucide-angular';
import { Observable, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import {
  AssetHierarchyItemDto,
  AssetHierarchyService,
  HierarchyItemInput,
  HierarchyModelOption,
  HierarchyTypeOption,
  toInput,
} from './asset-hierarchy.service';

/** Grouping levels plus the assets themselves. Assets can also parent other assets (sub-components). */
type NodeKind = 'root' | 'site' | 'building' | 'area' | 'floor' | 'system' | 'asset';
/** The API stores Active / Inactive; other values are shown as they come. */
type AssetStatus = string;

interface HierarchyNode {
  id: string;
  kind: NodeKind;
  name: string;
  /** Asset tag for assets, short code for grouping levels. */
  tag: string;
  type: string;
  model: string;
  manufacturer: string;
  status: AssetStatus;
  description: string;
  icon: keyof AssetHierarchy['assetIcons'];
  children: HierarchyNode[];
  /** The saved item; null for the "All Assets" root, which only exists on screen. */
  dto: AssetHierarchyItemDto | null;
}

interface NodeForm {
  kind: Exclude<NodeKind, 'root'>;
  name: string;
  tag: string;
  typeId: string;
  modelId: string;
  manufacturer: string;
  status: AssetStatus;
  parentId: string;
}

/** Id of the on-screen "All Assets" root; top-level items are saved with no parent. */
const ROOT_ID = '__root__';
const NODE_KINDS: NodeKind[] = ['site', 'building', 'area', 'floor', 'system', 'asset'];

type GroupKind = Exclude<NodeKind, 'root' | 'asset'>;

interface HierarchyNodeForm {
  name: string;
  kind: GroupKind;
  parentId: string;
  code: string;
  description: string;
  status: 'Active' | 'Inactive';
}

function rootNode(children: HierarchyNode[] = []): HierarchyNode {
  return { id: ROOT_ID, kind: 'root', name: 'All Assets', tag: 'ALL', type: '', model: '', manufacturer: '', status: 'Active', description: '', icon: 'generic', children, dto: null };
}

const KIND_LABELS: Record<NodeKind, string> = {
  root: 'Root',
  site: 'Site',
  building: 'Building',
  area: 'Area',
  floor: 'Floor',
  system: 'System',
  asset: 'Asset',
};

/** Children shown per row in the diagram before collapsing into "+N more". */
const DIAGRAM_ROW_LIMIT = 6;

@Component({
  selector: 'app-asset-hierarchy',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-hierarchy.html',
  styleUrl: './asset-hierarchy.css',
})
export class AssetHierarchy implements OnInit {
  readonly icons = {
    Check, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Download, Ellipsis, EllipsisVertical, Eye,
    FolderPlus, Inbox, Link2, MoveRight, Network, PackagePlus, Plus, Search, SquarePen, Trash2, Workflow, X,
  };

  readonly kindIcons = {
    root: Building2,
    site: Building2,
    building: Building,
    area: Factory,
    floor: Layers,
    system: Folder,
  };

  readonly assetIcons = {
    generic: Boxes,
    chiller: Cog,
    ahu: Fan,
    compressor: Gauge,
    pump: Droplets,
    panel: CircuitBoard,
    switchgear: ToggleRight,
    transformer: Zap,
    fire: Flame,
    alarm: BellRing,
    server: Server,
    ups: BatteryCharging,
    generator: Zap,
    solar: SolarPanel,
    conveyor: Package,
    forklift: Forklift,
    pallet: Warehouse,
  };

  readonly kindLabels = KIND_LABELS;
  readonly groupKinds: NodeForm['kind'][] = ['site', 'building', 'area', 'floor', 'system'];
  readonly statusOptions: AssetStatus[] = ['Active', 'Inactive'];
  readonly diagramRowLimit = DIAGRAM_ROW_LIMIT;

  root: HierarchyNode = rootNode();
  expanded = new Set<string>([ROOT_ID]);
  selectedId: string = ROOT_ID;
  treeSearch = '';

  /** True while the hierarchy is being fetched. */
  loading = false;
  /** Set when the hierarchy could not be loaded. */
  loadError = '';
  /** True while a create / update / move / delete request is running. */
  saving = false;
  /** Error from the add / edit form's save request. */
  formError = '';

  /** Asset type and model masters for the add / edit form. */
  assetTypes: HierarchyTypeOption[] = [];
  models: HierarchyModelOption[] = [];

  checkedChildren = new Set<string>();
  openMenu: string | null = null;

  // Add / edit modal
  formOpen = false;
  editingNode: HierarchyNode | null = null;
  form: NodeForm = this.emptyForm(ROOT_ID);
  formSubmitted = false;

  // "+" action menu and Create Hierarchy Node modal
  addMenuOpen = false;
  nodeFormOpen = false;
  nodeForm: HierarchyNodeForm = this.emptyNodeForm(ROOT_ID);
  nodeFormSubmitted = false;
  readonly nodeKinds: GroupKind[] = ['site', 'building', 'area', 'floor', 'system'];

  // Move modal
  moveTarget: HierarchyNode | null = null;
  moveParentId: string | null = null;

  // Assign-assets modal
  assignOpen = false;
  assignSearch = '';
  assignChecked = new Set<string>();

  deleteTarget: HierarchyNode | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  private readonly router = inject(Router);
  private readonly api = inject(AssetHierarchyService);

  ngOnInit(): void {
    this.reload(true);
    this.api.getAssetTypes().subscribe({ next: (types) => (this.assetTypes = types), error: () => (this.assetTypes = []) });
    this.api.getModels().subscribe({ next: (models) => (this.models = models), error: () => (this.models = []) });
  }

  // ===== Loading =====

  /** Fetches every item and rebuilds the tree, keeping the selection and open branches where they still exist. */
  reload(first = false): void {
    this.loading = true;
    this.loadError = '';
    this.api.getItems().subscribe({
      next: (items) => {
        this.root = this.buildTree(items);
        this.loading = false;
        if (!this.find(this.selectedId)) this.selectedId = ROOT_ID;
        if (first) {
          // Open the top two levels so the structure is visible straight away.
          this.root.children.forEach((c) => this.expanded.add(c.id));
        }
        this.lineage(this.selectedId).slice(0, -1).forEach((a) => this.expanded.add(a.id));
        [...this.checkedChildren].forEach((id) => {
          if (!this.find(id)) this.checkedChildren.delete(id);
        });
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load the asset hierarchy.');
      },
    });
  }

  private buildTree(items: AssetHierarchyItemDto[]): HierarchyNode {
    const nodes = new Map<string, HierarchyNode>();
    for (const dto of items) {
      const raw = (dto.itemKind ?? '').trim().toLowerCase();
      const kind: NodeKind = (NODE_KINDS as string[]).includes(raw) ? (raw as NodeKind) : 'asset';
      nodes.set(dto.id, {
        id: dto.id,
        kind,
        name: dto.name,
        tag: dto.assetTag ?? '',
        type: dto.assetTypeName ?? '',
        model: dto.modelName ?? '',
        manufacturer: dto.manufacturerName ?? '',
        status: dto.status || 'Active',
        description: '',
        icon: kind === 'asset' ? this.guessIcon(dto.assetTypeName ?? '') : 'generic',
        children: [],
        dto,
      });
    }
    const root = rootNode();
    for (const dto of items) {
      const node = nodes.get(dto.id)!;
      const parent = (dto.parentId && nodes.get(dto.parentId)) || root;
      parent.children.push(node);
    }
    return root;
  }

  /** Runs a change request, then reloads the tree and shows the message (or the API's error). */
  private run(request: Observable<unknown>, success: string, done?: () => void): void {
    this.saving = true;
    request.subscribe({
      next: () => {
        this.saving = false;
        done?.();
        this.showToast(success);
        this.reload();
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err));
        this.reload();
      },
    });
  }

  /** The saved fields of a node with a new parent, for a move. */
  private movedInput(n: HierarchyNode, dest: HierarchyNode): HierarchyItemInput {
    return { ...toInput(n.dto!), parentId: dest.id === ROOT_ID ? null : dest.id };
  }

  // ===== Tree helpers =====

  private walk(n: HierarchyNode, fn: (n: HierarchyNode, parent: HierarchyNode | null) => void, parent: HierarchyNode | null = null): void {
    fn(n, parent);
    n.children.forEach((c) => this.walk(c, fn, n));
  }

  private findBy(pred: (n: HierarchyNode) => boolean): HierarchyNode | null {
    let found: HierarchyNode | null = null;
    this.walk(this.root, (n) => {
      if (!found && pred(n)) found = n;
    });
    return found;
  }

  find(id: string | null): HierarchyNode | null {
    return id === null ? null : this.findBy((n) => n.id === id);
  }

  parentOf(id: string): HierarchyNode | null {
    let parent: HierarchyNode | null = null;
    this.walk(this.root, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  /** Root-first chain from "All Assets" down to the node itself. */
  lineage(id: string): HierarchyNode[] {
    const chain: HierarchyNode[] = [];
    let current = this.find(id);
    while (current) {
      chain.unshift(current);
      current = this.parentOf(current.id);
    }
    return chain;
  }

  private isInSubtree(candidate: HierarchyNode, ancestor: HierarchyNode): boolean {
    let inside = false;
    this.walk(ancestor, (n) => {
      if (n.id === candidate.id) inside = true;
    });
    return inside;
  }

  /** Number of assets at or below a node (grouping levels themselves are not counted). */
  assetCount(n: HierarchyNode): number {
    return (n.kind === 'asset' ? 1 : 0) + n.children.reduce((sum, c) => sum + this.assetCount(c), 0);
  }

  nodeIcon(n: HierarchyNode, open = false) {
    if (n.kind === 'asset') return this.assetIcons[n.icon];
    if (n.kind === 'system') return open ? FolderOpen : Folder;
    return this.kindIcons[n.kind];
  }

  get selected(): HierarchyNode {
    return this.find(this.selectedId) ?? this.root;
  }

  get selectedPath(): HierarchyNode[] {
    return this.lineage(this.selectedId);
  }

  /** "Bldg A - Floor 1" style location built from the building / area / floor ancestors. */
  locationOf(n: HierarchyNode): string {
    const parts = this.lineage(n.id)
      .slice(0, -1)
      .filter((a) => a.kind === 'building' || a.kind === 'area' || a.kind === 'floor')
      .map((a) => a.name.replace(/^Building\s+/, 'Bldg '));
    if (parts.length) return parts.join(' - ');
    const site = this.lineage(n.id).find((a) => a.kind === 'site');
    return site && site.id !== n.id ? site.name : '–';
  }

  // ===== Tree interaction =====

  isExpanded(n: HierarchyNode): boolean {
    return this.treeSearch.trim() ? true : this.expanded.has(n.id);
  }

  toggleExpand(n: HierarchyNode, event?: Event): void {
    event?.stopPropagation();
    if (this.expanded.has(n.id)) this.expanded.delete(n.id);
    else this.expanded.add(n.id);
  }

  select(n: HierarchyNode): void {
    this.selectedId = n.id;
    this.lineage(n.id).slice(0, -1).forEach((a) => this.expanded.add(a.id));
    this.checkedChildren.clear();
    this.openMenu = null;
  }

  expandAll(): void {
    this.openMenu = null;
    this.walk(this.root, (n) => {
      if (n.children.length) this.expanded.add(n.id);
    });
  }

  collapseAll(): void {
    this.openMenu = null;
    this.expanded.clear();
    this.expanded.add(this.root.id);
  }

  get allExpanded(): boolean {
    let all = true;
    this.walk(this.root, (n) => {
      if (n.children.length && !this.expanded.has(n.id)) all = false;
    });
    return all;
  }

  visibleChildren(n: HierarchyNode): HierarchyNode[] {
    const q = this.treeSearch.trim().toLowerCase();
    if (!q) return n.children;
    return n.children.filter((c) => this.subtreeMatches(c, q));
  }

  private subtreeMatches(n: HierarchyNode, q: string): boolean {
    return this.matchesQuery(n, q) || n.children.some((c) => this.subtreeMatches(c, q));
  }

  private matchesQuery(n: HierarchyNode, q: string): boolean {
    return [n.name, n.tag, n.type, n.model].some((v) => (v ?? '').toLowerCase().includes(q));
  }

  isMatch(n: HierarchyNode): boolean {
    const q = this.treeSearch.trim().toLowerCase();
    return !!q && this.matchesQuery(n, q);
  }

  get treeHasResults(): boolean {
    const q = this.treeSearch.trim().toLowerCase();
    return !q || this.subtreeMatches(this.root, q);
  }

  // ===== Diagram =====

  /** Top node of the diagram: the selection's site (the "All Assets" root is skipped unless selected). */
  get diagramRoot(): HierarchyNode {
    const path = this.selectedPath;
    return path.length > 1 ? path[1] : path[0];
  }

  /**
   * Which children to draw under a node in the diagram. Levels well above the selection show only the
   * branch leading to it; from the grandparent down, all children are shown so siblings are visible.
   */
  diagramChildren(n: HierarchyNode): HierarchyNode[] {
    const path = this.selectedPath;
    const index = path.findIndex((p) => p.id === n.id);
    if (index === -1) return [];
    if (index < path.length - 3) return [path[index + 1]];

    const children = n.children;
    if (children.length <= DIAGRAM_ROW_LIMIT) return children;
    // Keep the on-path child visible when trimming.
    const onPath = children.find((c) => path.some((p) => p.id === c.id));
    const head = children.slice(0, DIAGRAM_ROW_LIMIT - 1);
    return onPath && !head.includes(onPath) ? [...head.slice(0, -1), onPath] : head;
  }

  hiddenDiagramChildren(n: HierarchyNode): number {
    const shown = this.diagramChildren(n).length;
    const index = this.selectedPath.findIndex((p) => p.id === n.id);
    return index >= this.selectedPath.length - 3 ? Math.max(0, n.children.length - shown) : 0;
  }

  isOnPath(n: HierarchyNode): boolean {
    return this.selectedPath.some((p) => p.id === n.id);
  }

  // ===== Child assets table =====

  get children(): HierarchyNode[] {
    return this.selected.children;
  }

  get allChildrenChecked(): boolean {
    return this.children.length > 0 && this.children.every((c) => this.checkedChildren.has(c.id));
  }

  get someChildrenChecked(): boolean {
    return !this.allChildrenChecked && this.children.some((c) => this.checkedChildren.has(c.id));
  }

  toggleAllChildren(): void {
    const on = !this.allChildrenChecked;
    this.children.forEach((c) => (on ? this.checkedChildren.add(c.id) : this.checkedChildren.delete(c.id)));
  }

  toggleChild(id: string): void {
    if (this.checkedChildren.has(id)) this.checkedChildren.delete(id);
    else this.checkedChildren.add(id);
  }

  moveCheckedChildren(): void {
    const first = this.children.find((c) => this.checkedChildren.has(c.id));
    if (first) this.openMove(first);
  }

  statusClass(status: AssetStatus): string {
    return 'status--' + (status || 'active').toLowerCase().replace(/\s+/g, '-');
  }

  typeLabel(n: HierarchyNode): string {
    return n.kind === 'asset' ? n.type || '–' : KIND_LABELS[n.kind];
  }

  // ===== Menus =====

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.formOpen = false;
    this.addMenuOpen = false;
    this.nodeFormOpen = false;
    this.moveTarget = null;
    this.assignOpen = false;
    this.deleteTarget = null;
  }

  viewDetails(n: HierarchyNode): void {
    this.openMenu = null;
    if (n.kind === 'asset') {
      this.router.navigate(['/assets/list'], { queryParams: { tag: n.tag } });
    } else {
      this.select(n);
    }
  }

  // ===== Add / edit =====

  private emptyForm(parentId: string): NodeForm {
    return { kind: 'asset', name: '', tag: '', typeId: '', modelId: '', manufacturer: '', status: 'Active', parentId };
  }

  /** Parents a new node can be placed under, indented by depth. */
  get parentOptions(): { id: string; label: string }[] {
    const out: { id: string; label: string }[] = [];
    const visit = (n: HierarchyNode, depth: number) => {
      if (this.editingNode && this.isInSubtree(n, this.editingNode)) return;
      out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
      n.children.forEach((c) => visit(c, depth + 1));
    };
    visit(this.root, 0);
    return out;
  }

  /** Models of the asset type picked in the form (all models while no type is picked). */
  get modelOptions(): HierarchyModelOption[] {
    const typeId = this.form.typeId;
    return typeId ? this.models.filter((m) => m.assetTypeId === typeId) : this.models;
  }

  onTypeChange(typeId: string): void {
    this.form.typeId = typeId;
    const model = this.models.find((m) => m.id === this.form.modelId);
    if (model && model.assetTypeId !== typeId) this.form.modelId = '';
  }

  onModelChange(modelId: string): void {
    this.form.modelId = modelId;
    const model = this.models.find((m) => m.id === modelId);
    if (!model) return;
    // A model belongs to one asset type; fill it (and the manufacturer when empty) to match.
    if (!this.form.typeId) this.form.typeId = model.assetTypeId;
    if (!this.form.manufacturer.trim()) this.form.manufacturer = model.manufacturerName;
  }

  openAdd(parent: HierarchyNode = this.selected, kind: NodeForm['kind'] = 'asset'): void {
    this.openMenu = null;
    this.editingNode = null;
    this.form = { ...this.emptyForm(parent.id), kind };
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  openEdit(n: HierarchyNode = this.selected): void {
    this.openMenu = null;
    if (n.kind === 'root' || !n.dto) return;
    this.editingNode = n;
    this.form = {
      kind: n.kind,
      name: n.name,
      tag: n.tag,
      typeId: n.dto.assetTypeId ?? '',
      modelId: n.dto.modelId ?? '',
      manufacturer: n.manufacturer,
      status: n.status === 'Inactive' ? 'Inactive' : 'Active',
      parentId: this.parentOf(n.id)?.id ?? this.root.id,
    };
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  isTagTaken(tag: string): boolean {
    const t = tag.trim().toUpperCase();
    return !!t && !!this.findBy((n) => n !== this.editingNode && n.kind !== 'root' && n.tag.toUpperCase() === t);
  }

  get formValid(): boolean {
    const f = this.form;
    return !!f.name.trim() && !!f.tag.trim() && !this.isTagTaken(f.tag) && (f.kind !== 'asset' || !!f.typeId);
  }

  saveForm(): void {
    this.formSubmitted = true;
    if (!this.formValid || this.saving) return;
    const f = this.form;
    const isAsset = f.kind === 'asset';
    const manufacturer = isAsset ? f.manufacturer.trim() : '';
    const editing = this.editingNode;
    const input: HierarchyItemInput = {
      itemKind: f.kind,
      parentId: f.parentId === ROOT_ID ? null : f.parentId,
      name: f.name.trim(),
      assetTag: f.tag.trim().toUpperCase(),
      assetTypeId: isAsset ? f.typeId || null : null,
      modelId: isAsset ? f.modelId || null : null,
      // Keep the master link while the manufacturer name is unchanged.
      manufacturerId: editing?.dto && manufacturer && manufacturer === (editing.dto.manufacturerName ?? '') ? editing.dto.manufacturerId : null,
      manufacturerName: manufacturer || null,
      status: isAsset ? f.status : 'Active',
    };
    const parent = this.find(f.parentId) ?? this.root;
    const request = editing ? this.api.update(editing.id, input) : this.api.create(input);

    this.saving = true;
    this.formError = '';
    request.subscribe({
      next: (saved) => {
        this.saving = false;
        this.formOpen = false;
        this.expanded.add(parent.id);
        this.showToast(editing ? `${saved.name} updated.` : `${saved.name} added under ${parent.name}.`);
        this.reload();
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err);
      },
    });
  }

  private guessIcon(type: string): HierarchyNode['icon'] {
    const t = type.toLowerCase();
    const match = (Object.keys(this.assetIcons) as HierarchyNode['icon'][]).find((k) => k !== 'generic' && t.includes(k));
    return match ?? 'generic';
  }

  // ===== "+" action menu =====

  openAddMenu(): void {
    this.openMenu = null;
    this.addMenuOpen = true;
  }

  chooseCreateNode(): void {
    this.addMenuOpen = false;
    this.openCreateNode();
  }

  chooseAddExisting(): void {
    this.addMenuOpen = false;
    this.openAssign();
  }

  // ===== Create Hierarchy Node =====

  private emptyNodeForm(parentId: string): HierarchyNodeForm {
    return { name: '', kind: 'system', parentId, code: '', description: '', status: 'Active' };
  }

  /** Nearest non-asset node at or above `n` – hierarchy nodes can only sit under other hierarchy nodes. */
  private nearestGroup(n: HierarchyNode): HierarchyNode {
    return [...this.lineage(n.id)].reverse().find((a) => a.kind !== 'asset') ?? this.root;
  }

  openCreateNode(parent: HierarchyNode = this.selected): void {
    const p = this.nearestGroup(parent);
    this.nodeForm = { ...this.emptyNodeForm(p.id), kind: this.suggestKind(p) };
    this.nodeFormSubmitted = false;
    this.nodeFormOpen = true;
  }

  /** Sensible default level for a node created under `parent`. */
  private suggestKind(parent: HierarchyNode): GroupKind {
    switch (parent.kind) {
      case 'root': return 'site';
      case 'site': return 'building';
      case 'building': return 'floor';
      default: return 'system';
    }
  }

  /** Grouping nodes (no assets) a new hierarchy node can be placed under, indented by depth. */
  get nodeParentOptions(): { id: string; label: string }[] {
    const out: { id: string; label: string }[] = [];
    const visit = (n: HierarchyNode, depth: number) => {
      if (n.kind === 'asset') return;
      out.push({ id: n.id, label: `${'\u00a0\u00a0\u00a0'.repeat(depth)}${n.name}` });
      n.children.forEach((c) => visit(c, depth + 1));
    };
    visit(this.root, 0);
    return out;
  }

  /** "Head Office / Building A / Floor 1 / HVAC System" preview of where the new node lands. */
  get nodePreview(): string {
    const parent = this.find(this.nodeForm.parentId) ?? this.root;
    const names = this.lineage(parent.id).slice(1).map((a) => a.name);
    names.push(this.nodeForm.name.trim() || 'New Node');
    return names.join(' / ');
  }

  get nodeFormValid(): boolean {
    const f = this.nodeForm;
    return !!f.name.trim() && !!f.code.trim() && !this.isCodeTaken(f.code) && !!this.find(f.parentId);
  }

  isCodeTaken(code: string): boolean {
    const t = code.trim().toUpperCase();
    return !!t && !!this.findBy((n) => n.tag.toUpperCase() === t);
  }

  saveNode(): void {
    this.nodeFormSubmitted = true;
    if (!this.nodeFormValid || this.saving) return;
    const f = this.nodeForm;
    const parent = this.find(f.parentId) ?? this.root;
    const input: HierarchyItemInput = {
      itemKind: f.kind,
      parentId: parent.id === ROOT_ID ? null : parent.id,
      name: f.name.trim(),
      assetTag: f.code.trim().toUpperCase(),
      assetTypeId: null,
      modelId: null,
      manufacturerId: null,
      manufacturerName: null,
      status: f.status,
    };
    this.saving = true;
    this.api.create(input).subscribe({
      next: (saved) => {
        this.saving = false;
        this.nodeFormOpen = false;
        this.expanded.add(parent.id);
        this.selectedId = saved.id;
        this.showToast(`${saved.name} created under ${parent.name}.`);
        this.reload();
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err));
      },
    });
  }

  // ===== Move =====

  openMove(n: HierarchyNode): void {
    this.openMenu = null;
    this.moveTarget = n;
    this.moveParentId = this.parentOf(n.id)?.id ?? null;
  }

  /** Destinations for a move: anywhere outside the moved nodes' own subtrees. */
  get moveOptions(): { id: string; label: string }[] {
    const moving = this.movingNodes;
    const out: { id: string; label: string }[] = [];
    const visit = (n: HierarchyNode, depth: number) => {
      if (moving.some((m) => m.id === n.id)) return;
      out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
      n.children.forEach((c) => visit(c, depth + 1));
    };
    visit(this.root, 0);
    return out;
  }

  /** The move modal acts on all checked child rows when the target is one of them. */
  get movingNodes(): HierarchyNode[] {
    const t = this.moveTarget;
    if (!t) return [];
    const checked = this.children.filter((c) => this.checkedChildren.has(c.id));
    return checked.some((c) => c.id === t.id) ? checked : [t];
  }

  confirmMove(): void {
    const dest = this.find(this.moveParentId);
    if (!dest || this.saving) return;
    const nodes = this.movingNodes.filter((n) => this.canReparent(n, dest));
    this.moveTarget = null;
    this.checkedChildren.clear();
    if (!nodes.length) return;
    this.expanded.add(dest.id);
    this.run(
      forkJoin(nodes.map((n) => this.api.update(n.id, this.movedInput(n, dest)))),
      nodes.length === 1 ? `${nodes[0].name} moved to ${dest.name}.` : `${nodes.length} items moved to ${dest.name}.`,
    );
  }

  /** False when the node is already there or the move would put it under itself. */
  private canReparent(n: HierarchyNode, dest: HierarchyNode): boolean {
    const current = this.parentOf(n.id);
    return !!n.dto && !!current && current.id !== dest.id && !this.isInSubtree(dest, n);
  }

  // ===== Assign existing assets =====

  openAssign(): void {
    this.assignSearch = '';
    this.assignChecked.clear();
    this.assignOpen = true;
  }

  /** Assets elsewhere in the hierarchy that could be placed under the selection. */
  get assignCandidates(): { node: HierarchyNode; location: string }[] {
    const sel = this.selected;
    const ancestors = new Set(this.selectedPath.map((p) => p.id));
    const q = this.assignSearch.trim().toLowerCase();
    const out: { node: HierarchyNode; location: string }[] = [];
    this.walk(this.root, (n, parent) => {
      if (n.kind !== 'asset' || parent?.id === sel.id || ancestors.has(n.id) || this.isInSubtree(n, sel)) return;
      if (q && !this.matchesQuery(n, q)) return;
      out.push({ node: n, location: this.lineage(n.id).slice(1, -1).map((a) => a.name).join(' › ') });
    });
    return out;
  }

  toggleAssign(id: string): void {
    if (this.assignChecked.has(id)) this.assignChecked.delete(id);
    else this.assignChecked.add(id);
  }

  confirmAssign(): void {
    const sel = this.selected;
    // Never move a node underneath itself.
    const nodes = [...this.assignChecked]
      .map((id) => this.find(id))
      .filter((n): n is HierarchyNode => !!n && this.canReparent(n, sel));
    this.assignOpen = false;
    if (!nodes.length || this.saving) return;
    this.expanded.add(sel.id);
    this.run(
      forkJoin(nodes.map((n) => this.api.update(n.id, this.movedInput(n, sel)))),
      `${nodes.length} asset${nodes.length === 1 ? '' : 's'} assigned to ${sel.name}.`,
    );
  }

  // ===== Delete =====

  askDelete(n: HierarchyNode): void {
    this.openMenu = null;
    if (n.kind === 'root') return;
    if (n.children.length) {
      this.showToast(`${n.name} still has ${n.children.length} item${n.children.length === 1 ? '' : 's'} under it. Move or remove them first.`);
      return;
    }
    this.deleteTarget = n;
  }

  confirmDelete(): void {
    const n = this.deleteTarget;
    if (!n || this.saving) return;
    const parent = this.parentOf(n.id);
    this.deleteTarget = null;
    this.run(this.api.delete(n.id), `${n.name} removed from the hierarchy.`, () => {
      if (parent && this.selectedId === n.id) this.selectedId = parent.id;
      this.checkedChildren.delete(n.id);
    });
  }

  // ===== Export =====

  exportCsv(): void {
    this.openMenu = null;
    const rows: string[][] = [['Path', 'Level', 'Name', 'Tag / Code', 'Type', 'Model', 'Manufacturer', 'Status']];
    this.walk(this.root, (n) => {
      if (n.kind === 'root') return;
      const path = this.lineage(n.id).slice(1, -1).map((a) => a.name).join(' > ');
      rows.push([path, KIND_LABELS[n.kind], n.name, n.tag, n.type, n.model, n.manufacturer, n.kind === 'asset' ? n.status : '']);
    });
    const escape = (v: string) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);
    const blob = new Blob([rows.map((r) => r.map(escape).join(',')).join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'asset-hierarchy.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  trackById(_: number, n: HierarchyNode): string {
    return n.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
