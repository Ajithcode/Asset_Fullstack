import { Component, HostListener } from '@angular/core';
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
  Forklift,
  Gauge,
  Inbox,
  Layers,
  Link2,
  MoveRight,
  Network,
  Package,
  Plus,
  Search,
  Server,
  SolarPanel,
  SquarePen,
  ToggleRight,
  Trash2,
  Warehouse,
  X,
  Zap,
} from 'lucide-angular';

/** Grouping levels plus the assets themselves. Assets can also parent other assets (sub-components). */
type NodeKind = 'root' | 'site' | 'building' | 'area' | 'floor' | 'system' | 'asset';
type AssetStatus = 'Active' | 'Under Maintenance' | 'In Repair' | 'Inactive';

interface HierarchyNode {
  id: number;
  kind: NodeKind;
  name: string;
  /** Asset tag for assets, short code for grouping levels. */
  tag: string;
  type: string;
  model: string;
  manufacturer: string;
  status: AssetStatus;
  icon: keyof AssetHierarchy['assetIcons'];
  children: HierarchyNode[];
}

interface NodeForm {
  kind: Exclude<NodeKind, 'root'>;
  name: string;
  tag: string;
  type: string;
  model: string;
  manufacturer: string;
  status: AssetStatus;
  parentId: number;
}

let nextId = 1;

function group(kind: NodeKind, name: string, tag: string, children: HierarchyNode[] = []): HierarchyNode {
  return { id: nextId++, kind, name, tag, type: '', model: '', manufacturer: '', status: 'Active', icon: 'generic', children };
}

function asset(
  name: string,
  tag: string,
  type: string,
  model: string,
  manufacturer: string,
  icon: HierarchyNode['icon'],
  status: AssetStatus = 'Active',
  children: HierarchyNode[] = [],
): HierarchyNode {
  return { id: nextId++, kind: 'asset', name, tag, type, model, manufacturer, status, icon, children };
}

function buildMockTree(): HierarchyNode {
  return group('root', 'All Assets', 'ALL', [
    group('site', 'Head Office', 'HO', [
      group('building', 'Building A', 'HO-BA', [
        group('floor', 'Floor 1', 'HO-BA-F1', [
          group('system', 'HVAC System', 'HO-BA-F1-HVAC', [
            asset('Chiller-01', 'CH-001', 'Chiller', 'TR-350', 'Trane', 'chiller', 'Active', [
              asset('Compressor CH1-A', 'CMP-101', 'Compressor', 'ZR-190', 'Copeland', 'compressor'),
              asset('Chilled Water Pump', 'CHWP-01', 'Pump', 'NBG-125', 'Grundfos', 'pump'),
              asset('Chiller Control Panel', 'CP-CH1', 'Control Panel', 'TS-8', 'Rittal', 'panel'),
            ]),
            asset('Chiller-02', 'CH-002', 'Chiller', 'TR-350', 'Trane', 'chiller', 'Under Maintenance'),
            asset('AHU-01', 'AHU-001', 'AHU', 'AH-200', 'Daikin', 'ahu'),
          ]),
          group('system', 'Electrical System', 'HO-BA-F1-ELEC', [
            asset('Main Switchgear', 'SWG-001', 'Switchgear', 'MNS-3', 'ABB', 'switchgear'),
            asset('Transformer-01', 'TRF-001', 'Transformer', 'TX-500', 'ABB', 'transformer'),
          ]),
          group('system', 'Fire System', 'HO-BA-F1-FIRE', [
            asset('Fire Pump-01', 'FP-001', 'Fire Pump', 'FP-750', 'Grundfos', 'fire'),
            asset('Fire Alarm Panel', 'FAP-001', 'Fire Alarm Panel', 'NFS2-3030', 'Honeywell', 'alarm'),
          ]),
        ]),
        group('floor', 'Floor 2', 'HO-BA-F2', [
          group('system', 'IT Room', 'HO-BA-F2-IT', [
            asset('Server-01', 'SRV-001', 'Server', 'R750', 'Dell', 'server'),
            asset('Core Switch-01', 'NSW-001', 'Network Switch', 'C9300', 'Cisco', 'server'),
            asset('UPS-01', 'UPS-001', 'UPS', 'SRT10K', 'APC', 'ups'),
          ]),
        ]),
      ]),
      group('building', 'Building B', 'HO-BB', [
        group('floor', 'Ground Floor', 'HO-BB-GF', [
          asset('Generator-01', 'GEN-001', 'Generator', 'C250D5', 'Caterpillar', 'generator'),
          asset('UPS-02', 'UPS-002', 'UPS', 'SRT10K', 'APC', 'ups', 'In Repair'),
        ]),
        group('floor', 'Roof', 'HO-BB-RF', [
          asset('Solar Array-01', 'SOL-001', 'Solar Panel', 'JKM550', 'Jinko Solar', 'solar'),
          asset('Chiller-03', 'CH-003', 'Chiller', 'TR-500', 'Trane', 'chiller'),
        ]),
      ]),
    ]),
    group('site', 'Warehouse', 'WH', [
      group('area', 'Production Line 1', 'WH-PL1', [
        asset('Conveyor-01', 'CNV-001', 'Conveyor', 'RM-8000', 'Interroll', 'conveyor'),
        asset('Forklift-01', 'FLT-001', 'Forklift', '8FBE20', 'Toyota', 'forklift'),
        asset('Forklift-02', 'FLT-002', 'Forklift', '8FBE20', 'Toyota', 'forklift', 'Under Maintenance'),
      ]),
      group('area', 'Production Line 2', 'WH-PL2', [
        asset('Conveyor-02', 'CNV-002', 'Conveyor', 'RM-8000', 'Interroll', 'conveyor'),
        asset('Pallet Jack-01', 'PJK-001', 'Pallet Jack', 'EJE 120', 'Jungheinrich', 'pallet'),
      ]),
      group('area', 'Utilities', 'WH-UTL', [
        asset('Air Compressor-01', 'CMP-001', 'Compressor', 'GA 37', 'Atlas Copco', 'compressor'),
        asset('Generator-02', 'GEN-002', 'Generator', 'QSX15', 'Cummins', 'generator'),
      ]),
    ]),
    group('site', 'Remote Sites', 'RS', [
      group('area', 'Plant 1', 'RS-P1', [
        asset('Generator-03', 'GEN-003', 'Generator', 'C250D5', 'Caterpillar', 'generator'),
        asset('Transformer-02', 'TRF-002', 'Transformer', 'TX-1000', 'Siemens', 'transformer'),
      ]),
      group('area', 'Plant 2', 'RS-P2', [
        group('system', 'Pump Station', 'RS-P2-PS', [
          asset('Pump-01', 'PMP-001', 'Pump', 'CR-64', 'Grundfos', 'pump'),
          asset('Pump-02', 'PMP-002', 'Pump', 'CR-64', 'Grundfos', 'pump', 'Inactive'),
        ]),
      ]),
    ]),
  ]);
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
export class AssetHierarchy {
  readonly icons = {
    Check, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Download, Ellipsis, EllipsisVertical, Eye,
    Inbox, Link2, MoveRight, Network, Plus, Search, SquarePen, Trash2, X,
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
  readonly statusOptions: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Inactive'];
  readonly diagramRowLimit = DIAGRAM_ROW_LIMIT;

  root: HierarchyNode = buildMockTree();
  expanded = new Set<number>();
  selectedId: number;
  treeSearch = '';

  checkedChildren = new Set<number>();
  openMenu: string | null = null;

  // Add / edit modal
  formOpen = false;
  editingNode: HierarchyNode | null = null;
  form: NodeForm = this.emptyForm(0);
  formSubmitted = false;

  // Move modal
  moveTarget: HierarchyNode | null = null;
  moveParentId: number | null = null;

  // Assign-assets modal
  assignOpen = false;
  assignSearch = '';
  assignChecked = new Set<number>();

  deleteTarget: HierarchyNode | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    const chiller = this.findBy((n) => n.tag === 'CH-001') ?? this.root;
    this.selectedId = chiller.id;
    // Match the design: open the path to Chiller-01 plus the neighbouring branches.
    this.lineage(chiller.id).slice(0, -1).forEach((n) => this.expanded.add(n.id));
    ['HO-BA-F1-ELEC', 'WH', 'RS'].forEach((tag) => {
      const n = this.findBy((x) => x.tag === tag);
      if (n) this.expanded.add(n.id);
    });
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

  find(id: number | null): HierarchyNode | null {
    return id === null ? null : this.findBy((n) => n.id === id);
  }

  parentOf(id: number): HierarchyNode | null {
    let parent: HierarchyNode | null = null;
    this.walk(this.root, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  /** Root-first chain from "All Assets" down to the node itself. */
  lineage(id: number): HierarchyNode[] {
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
    return [n.name, n.tag, n.type, n.model].some((v) => v.toLowerCase().includes(q));
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

  toggleChild(id: number): void {
    if (this.checkedChildren.has(id)) this.checkedChildren.delete(id);
    else this.checkedChildren.add(id);
  }

  moveCheckedChildren(): void {
    const first = this.children.find((c) => this.checkedChildren.has(c.id));
    if (first) this.openMove(first);
  }

  statusClass(status: AssetStatus): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
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

  private emptyForm(parentId: number): NodeForm {
    return { kind: 'asset', name: '', tag: '', type: '', model: '', manufacturer: '', status: 'Active', parentId };
  }

  /** Parents a new node can be placed under, indented by depth. */
  get parentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (n: HierarchyNode, depth: number) => {
      if (this.editingNode && this.isInSubtree(n, this.editingNode)) return;
      out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
      n.children.forEach((c) => visit(c, depth + 1));
    };
    visit(this.root, 0);
    return out;
  }

  openAdd(parent: HierarchyNode = this.selected, kind: NodeForm['kind'] = 'asset'): void {
    this.openMenu = null;
    this.editingNode = null;
    this.form = { ...this.emptyForm(parent.id), kind };
    this.formSubmitted = false;
    this.formOpen = true;
  }

  openEdit(n: HierarchyNode = this.selected): void {
    this.openMenu = null;
    if (n.kind === 'root') return;
    this.editingNode = n;
    this.form = {
      kind: n.kind,
      name: n.name,
      tag: n.tag,
      type: n.type,
      model: n.model,
      manufacturer: n.manufacturer,
      status: n.status,
      parentId: this.parentOf(n.id)?.id ?? this.root.id,
    };
    this.formSubmitted = false;
    this.formOpen = true;
  }

  isTagTaken(tag: string): boolean {
    const t = tag.trim().toUpperCase();
    return !!t && !!this.findBy((n) => n !== this.editingNode && n.tag.toUpperCase() === t);
  }

  get formValid(): boolean {
    const f = this.form;
    return !!f.name.trim() && !!f.tag.trim() && !this.isTagTaken(f.tag) && (f.kind !== 'asset' || !!f.type.trim());
  }

  saveForm(): void {
    this.formSubmitted = true;
    if (!this.formValid) return;
    const f = this.form;
    const isAsset = f.kind === 'asset';
    const values = {
      kind: f.kind,
      name: f.name.trim(),
      tag: f.tag.trim().toUpperCase(),
      type: isAsset ? f.type.trim() : '',
      model: isAsset ? f.model.trim() : '',
      manufacturer: isAsset ? f.manufacturer.trim() : '',
      status: isAsset ? f.status : ('Active' as AssetStatus),
    };
    const parent = this.find(f.parentId) ?? this.root;

    if (this.editingNode) {
      const n = this.editingNode;
      const kindChanged = n.kind !== values.kind;
      Object.assign(n, values);
      if (kindChanged) n.icon = n.kind === 'asset' ? this.guessIcon(n.type) : 'generic';
      this.reparent(n, parent);
      this.showToast(`${n.name} updated.`);
    } else {
      const created: HierarchyNode = { id: nextId++, ...values, icon: this.guessIcon(values.type), children: [] };
      parent.children.push(created);
      this.expanded.add(parent.id);
      this.showToast(`${created.name} added under ${parent.name}.`);
    }
    this.formOpen = false;
  }

  private guessIcon(type: string): HierarchyNode['icon'] {
    const t = type.toLowerCase();
    const match = (Object.keys(this.assetIcons) as HierarchyNode['icon'][]).find((k) => k !== 'generic' && t.includes(k));
    return match ?? 'generic';
  }

  // ===== Move =====

  openMove(n: HierarchyNode): void {
    this.openMenu = null;
    this.moveTarget = n;
    this.moveParentId = this.parentOf(n.id)?.id ?? null;
  }

  /** Destinations for a move: anywhere outside the moved nodes' own subtrees. */
  get moveOptions(): { id: number; label: string }[] {
    const moving = this.movingNodes;
    const out: { id: number; label: string }[] = [];
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
    if (!dest) return;
    const nodes = this.movingNodes;
    nodes.forEach((n) => this.reparent(n, dest));
    this.expanded.add(dest.id);
    this.checkedChildren.clear();
    this.moveTarget = null;
    this.showToast(nodes.length === 1 ? `${nodes[0].name} moved to ${dest.name}.` : `${nodes.length} items moved to ${dest.name}.`);
  }

  private reparent(n: HierarchyNode, dest: HierarchyNode): void {
    const current = this.parentOf(n.id);
    if (!current || current.id === dest.id || this.isInSubtree(dest, n)) return;
    current.children.splice(current.children.indexOf(n), 1);
    dest.children.push(n);
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

  toggleAssign(id: number): void {
    if (this.assignChecked.has(id)) this.assignChecked.delete(id);
    else this.assignChecked.add(id);
  }

  confirmAssign(): void {
    const sel = this.selected;
    const nodes = [...this.assignChecked].map((id) => this.find(id)).filter((n): n is HierarchyNode => !!n);
    // Never move a node underneath itself.
    nodes.filter((n) => !this.isInSubtree(sel, n)).forEach((n) => this.reparent(n, sel));
    this.expanded.add(sel.id);
    this.assignOpen = false;
    this.showToast(`${nodes.length} asset${nodes.length === 1 ? '' : 's'} assigned to ${sel.name}.`);
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
    if (!n) return;
    const parent = this.parentOf(n.id);
    if (parent) {
      parent.children.splice(parent.children.indexOf(n), 1);
      if (this.selectedId === n.id) this.selectedId = parent.id;
    }
    this.checkedChildren.delete(n.id);
    this.deleteTarget = null;
    this.showToast(`${n.name} removed from the hierarchy.`);
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

  trackById(_: number, n: HierarchyNode): number {
    return n.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
