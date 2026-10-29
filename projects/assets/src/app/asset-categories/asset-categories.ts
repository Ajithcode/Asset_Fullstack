import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  AirVent,
  Archive,
  Armchair,
  BatteryCharging,
  BellRing,
  Box,
  Boxes,
  Car,
  Cctv,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  ChevronsUpDown,
  CircuitBoard,
  Cog,
  Container,
  Cpu,
  Drill,
  EllipsisVertical,
  ExternalLink,
  Eye,
  Fan,
  FileStack,
  FileText,
  Flame,
  Folder,
  FolderOpen,
  Forklift,
  Gauge,
  Hammer,
  ImageUp,
  Inbox,
  KeyRound,
  Laptop,
  ListTree,
  Monitor,
  Network,
  Package,
  Plus,
  Power,
  PowerOff,
  Printer,
  Ruler,
  Search,
  Server,
  ShieldCheck,
  Snowflake,
  SquarePen,
  ToggleRight,
  Trash2,
  Truck,
  Wind,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';

type CategoryStatus = 'Active' | 'Inactive';
type DetailTab = 'subcategories' | 'fields' | 'attributes' | 'assets';
type FieldType = 'Text' | 'Number' | 'Date' | 'Dropdown' | 'Yes / No';

interface CustomField {
  name: string;
  type: FieldType;
  required: boolean;
}

interface Attribute {
  name: string;
  unit: string;
  example: string;
}

interface CategoryNode {
  id: number;
  name: string;
  code: string;
  description: string;
  status: CategoryStatus;
  icon: keyof AssetCategories['categoryIcons'];
  /** Uploaded image as a data URL; the icon is shown when absent. */
  image?: string;
  /** Assets registered directly against this category (leaf categories only, in practice). */
  ownAssets: number;
  customFields: CustomField[];
  attributes: Attribute[];
  children: CategoryNode[];
}

/** A field or attribute row, tagged with the category it was defined on. */
interface Inherited<T> {
  item: T;
  from: string;
  inherited: boolean;
}

interface CategoryForm {
  name: string;
  code: string;
  parentId: number | null;
  description: string;
  status: CategoryStatus;
}

let nextId = 1;

function node(
  name: string,
  code: string,
  icon: CategoryNode['icon'],
  description: string,
  opts: Partial<Pick<CategoryNode, 'ownAssets' | 'status' | 'customFields' | 'attributes' | 'children'>> = {},
): CategoryNode {
  return {
    id: nextId++,
    name,
    code,
    icon,
    description,
    status: opts.status ?? 'Active',
    ownAssets: opts.ownAssets ?? 0,
    customFields: opts.customFields ?? [],
    attributes: opts.attributes ?? [],
    children: opts.children ?? [],
  };
}

function buildMockTree(): CategoryNode[] {
  return [
    node('Machinery & Equipment', 'ME', 'machinery', 'All types of machinery, plant and equipment used for operations and support functions.', {
      customFields: [
        { name: 'Operating Hours', type: 'Number', required: false },
        { name: 'Commissioning Date', type: 'Date', required: true },
        { name: 'Service Contract', type: 'Yes / No', required: false },
      ],
      attributes: [
        { name: 'Rated Power', unit: 'kW', example: '250' },
        { name: 'Voltage', unit: 'V', example: '415' },
        { name: 'Weight', unit: 'kg', example: '1,850' },
        { name: 'Noise Level', unit: 'dB', example: '72' },
        { name: 'Efficiency', unit: '%', example: '94' },
      ],
      children: [
        node('Power Generation', 'PG', 'generator', 'Equipment that generates, converts or distributes electrical power.', {
          customFields: [{ name: 'Fuel Type', type: 'Dropdown', required: true }],
          children: [
            node('Generator', 'GEN', 'generator', 'Diesel / Gas / Portable Generators', { ownAssets: 56, attributes: [{ name: 'Fuel Tank Capacity', unit: 'L', example: '500' }] }),
            node('Transformer', 'TRF', 'transformer', 'Power Transformers and Distribution Transformers', { ownAssets: 24 }),
            node('UPS', 'UPS', 'ups', 'Uninterruptible Power Supply', { ownAssets: 32, attributes: [{ name: 'Backup Time', unit: 'min', example: '30' }] }),
            node('Switchgear', 'SWG', 'switchgear', 'LV / MV Switchgear', { ownAssets: 18 }),
            node('Control Panel', 'CP', 'panel', 'Electrical Control Panels', { ownAssets: 40 }),
          ],
        }),
        node('HVAC', 'HVAC', 'chiller', 'Heating, ventilation and air conditioning equipment.', {
          attributes: [{ name: 'Cooling Capacity', unit: 'TR', example: '5' }],
          children: [
            node('Chiller', 'CHL', 'chiller', 'Air / Water Cooled Chillers', { ownAssets: 12 }),
            node('AHU', 'AHU', 'ahu', 'Air Handling Units', { ownAssets: 20 }),
            node('Split AC', 'SAC', 'ac', 'Wall / Ceiling Split Air Conditioners', { ownAssets: 64 }),
            node('VRF System', 'VRF', 'vrf', 'Variable Refrigerant Flow Systems', { ownAssets: 8, status: 'Inactive' }),
          ],
        }),
        node('Material Handling', 'MH', 'forklift', 'Equipment used to move, store and control materials.', {
          customFields: [{ name: 'Load Test Certificate', type: 'Text', required: true }],
          children: [
            node('Forklift', 'FLT', 'forklift', 'Electric / Diesel Forklifts', { ownAssets: 14 }),
            node('Conveyor', 'CNV', 'conveyor', 'Belt and Roller Conveyors', { ownAssets: 6 }),
            node('Crane', 'CRN', 'crane', 'Overhead and Gantry Cranes', { ownAssets: 3 }),
            node('Pallet Jack', 'PJK', 'pallet', 'Manual / Electric Pallet Jacks', { ownAssets: 22 }),
          ],
        }),
        node('Motor', 'MTR', 'motor', 'Electric Motors', { ownAssets: 28, status: 'Inactive' }),
        node('Compressor', 'CMP', 'compressor', 'Air Compressors', { ownAssets: 14 }),
        node('Pump', 'PMP', 'pump', 'Water / Fuel / Process Pumps', { ownAssets: 36 }),
      ],
    }),
    node('IT Equipment', 'IT', 'it', 'Computing, networking and peripheral hardware.', {
      customFields: [
        { name: 'Asset Owner Email', type: 'Text', required: true },
        { name: 'OS Version', type: 'Text', required: false },
      ],
      attributes: [{ name: 'RAM', unit: 'GB', example: '16' }, { name: 'Storage', unit: 'GB', example: '512' }],
      children: [
        node('Laptop', 'LAP', 'laptop', 'Business Laptops and Notebooks', { ownAssets: 210 }),
        node('Desktop', 'DSK', 'desktop', 'Desktop Computers and Workstations', { ownAssets: 96 }),
        node('Server', 'SRV', 'server', 'Rack and Tower Servers', { ownAssets: 18 }),
        node('Network Switch', 'NSW', 'network', 'Managed / Unmanaged Switches', { ownAssets: 34 }),
        node('Printer', 'PRN', 'printer', 'Printers, Scanners and MFPs', { ownAssets: 27 }),
      ],
    }),
    node('Security & Safety', 'SS', 'security', 'Surveillance, access control and fire safety systems.', {
      children: [
        node('CCTV', 'CCTV', 'cctv', 'Dome / Bullet / PTZ Cameras', { ownAssets: 142 }),
        node('Fire Alarm', 'FAL', 'alarm', 'Fire Alarm Panels and Detectors', { ownAssets: 58 }),
        node('Access Control', 'ACC', 'access', 'Card Readers and Door Controllers', { ownAssets: 44 }),
        node('Fire Pump', 'FP', 'fire', 'Fire Fighting Pumps', { ownAssets: 6 }),
      ],
    }),
    node('Furniture & Fixtures', 'FF', 'furniture', 'Office furniture, fittings and fixtures.', {
      children: [
        node('Desk', 'DSK-F', 'furniture', 'Office Desks and Workstations', { ownAssets: 320 }),
        node('Chair', 'CHR', 'furniture', 'Office and Visitor Chairs', { ownAssets: 410 }),
        node('Cabinet', 'CAB', 'cabinet', 'Filing and Storage Cabinets', { ownAssets: 85 }),
      ],
    }),
    node('Vehicles & Fleet', 'VF', 'vehicle', 'Company-owned and leased vehicles.', {
      customFields: [
        { name: 'Registration Number', type: 'Text', required: true },
        { name: 'Insurance Expiry', type: 'Date', required: true },
      ],
      children: [
        node('Car', 'CAR', 'vehicle', 'Passenger Cars and SUVs', { ownAssets: 22 }),
        node('Truck', 'TRK', 'truck', 'Light and Heavy Trucks', { ownAssets: 9 }),
        node('Van', 'VAN', 'truck', 'Delivery and Utility Vans', { ownAssets: 7 }),
      ],
    }),
    node('Tools & Instruments', 'TI', 'tools', 'Hand tools, power tools and measuring instruments.', {
      children: [
        node('Power Tools', 'PWT', 'drill', 'Drills, Grinders and Saws', { ownAssets: 48 }),
        node('Measuring Instruments', 'MSI', 'gauge', 'Multimeters, Gauges and Meters', { ownAssets: 31, attributes: [{ name: 'Calibration Interval', unit: 'months', example: '12' }] }),
      ],
    }),
    node('Others', 'OTH', 'other', 'Assets that do not fit any other category.', { ownAssets: 15 }),
  ];
}

const EMPTY_FORM: CategoryForm = { name: '', code: '', parentId: null, description: '', status: 'Active' };
const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

@Component({
  selector: 'app-asset-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-categories.html',
  styleUrl: './asset-categories.css',
})
export class AssetCategories {
  readonly icons = {
    Boxes, Check, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, ChevronsUpDown, EllipsisVertical, ExternalLink,
    Eye, FileStack, FileText, Folder, FolderOpen, ImageUp, Inbox, ListTree, Plus, Power, PowerOff, Search,
    SquarePen, Trash2, X,
  };

  readonly categoryIcons = {
    machinery: Wrench,
    generator: Zap,
    transformer: Cog,
    ups: BatteryCharging,
    switchgear: ToggleRight,
    panel: CircuitBoard,
    chiller: Snowflake,
    ahu: Fan,
    ac: AirVent,
    vrf: Wind,
    forklift: Forklift,
    conveyor: Package,
    crane: Container,
    pallet: Boxes,
    motor: Cog,
    compressor: Gauge,
    pump: Cog,
    it: Cpu,
    laptop: Laptop,
    desktop: Monitor,
    server: Server,
    network: Network,
    printer: Printer,
    security: ShieldCheck,
    cctv: Cctv,
    alarm: BellRing,
    access: KeyRound,
    fire: Flame,
    furniture: Armchair,
    cabinet: Archive,
    vehicle: Car,
    truck: Truck,
    tools: Hammer,
    drill: Drill,
    gauge: Ruler,
    other: Box,
  };

  readonly fieldTypes: FieldType[] = ['Text', 'Number', 'Date', 'Dropdown', 'Yes / No'];
  readonly pageSizeOptions = [10, 25, 50];

  tree: CategoryNode[] = buildMockTree();
  expanded = new Set<number>();
  selectedId: number;
  treeSearch = '';

  // Details form (read-only until "Edit")
  editing = false;
  detailForm: CategoryForm = { ...EMPTY_FORM };
  submitted = false;

  activeTab: DetailTab = 'subcategories';

  // Subcategories table
  subSearch = '';
  subStatus: '' | CategoryStatus = '';
  subSortDir: 'asc' | 'desc' | null = null;
  page = 1;
  pageSize = 10;
  selectedRows = new Set<number>();
  openMenuId: number | null = null;

  // Add-category modal
  modalOpen = false;
  modalForm: CategoryForm = { ...EMPTY_FORM };
  modalSubmitted = false;

  // Add custom field / attribute inline rows
  newField: CustomField = { name: '', type: 'Text', required: false };
  newAttribute: Attribute = { name: '', unit: '', example: '' };

  deleteTarget: CategoryNode | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    const first = this.tree[0];
    this.selectedId = first.id;
    // Open the first branch fully, as in the design.
    this.expanded.add(first.id);
    first.children.filter((c) => c.children.length).forEach((c) => this.expanded.add(c.id));
    this.loadDetailForm();
  }

  // ===== Tree helpers =====

  private walk(nodes: CategoryNode[], fn: (n: CategoryNode, parent: CategoryNode | null) => void, parent: CategoryNode | null = null): void {
    for (const n of nodes) {
      fn(n, parent);
      this.walk(n.children, fn, n);
    }
  }

  private find(id: number | null): CategoryNode | null {
    let found: CategoryNode | null = null;
    if (id === null) return null;
    this.walk(this.tree, (n) => {
      if (n.id === id) found = n;
    });
    return found;
  }

  private parentOf(id: number): CategoryNode | null {
    let parent: CategoryNode | null = null;
    this.walk(this.tree, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  /** Root-first chain of ancestors, including the node itself. */
  private lineage(id: number): CategoryNode[] {
    const chain: CategoryNode[] = [];
    let current = this.find(id);
    while (current) {
      chain.unshift(current);
      current = this.parentOf(current.id);
    }
    return chain;
  }

  assetCount(n: CategoryNode): number {
    return n.ownAssets + n.children.reduce((sum, c) => sum + this.assetCount(c), 0);
  }

  get selected(): CategoryNode {
    return this.find(this.selectedId) ?? this.tree[0];
  }

  get selectedPath(): string {
    return this.lineage(this.selectedId).map((n) => n.name).join(' › ');
  }

  /** Every category as a flat, indented list for the "Parent Category" picker. */
  get parentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (nodes: CategoryNode[], depth: number) => {
      for (const n of nodes) {
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
        visit(n.children, depth + 1);
      }
    };
    visit(this.tree, 0);
    return out;
  }

  // ===== Tree interaction =====

  isExpanded(n: CategoryNode): boolean {
    return this.treeSearch.trim() ? true : this.expanded.has(n.id);
  }

  toggleExpand(n: CategoryNode, event?: Event): void {
    event?.stopPropagation();
    if (this.expanded.has(n.id)) this.expanded.delete(n.id);
    else this.expanded.add(n.id);
  }

  selectCategory(n: CategoryNode): void {
    if (this.editing && n.id !== this.selectedId && !confirm('Discard unsaved changes to this category?')) return;
    this.selectedId = n.id;
    this.lineage(n.id).slice(0, -1).forEach((a) => this.expanded.add(a.id));
    this.editing = false;
    this.activeTab = 'subcategories';
    this.subSearch = '';
    this.subStatus = '';
    this.page = 1;
    this.selectedRows.clear();
    this.loadDetailForm();
  }

  /** Nodes to render while searching: matches plus the ancestors needed to reach them. */
  visibleChildren(nodes: CategoryNode[]): CategoryNode[] {
    const q = this.treeSearch.trim().toLowerCase();
    if (!q) return nodes;
    return nodes.filter((n) => this.subtreeMatches(n, q));
  }

  private subtreeMatches(n: CategoryNode, q: string): boolean {
    return n.name.toLowerCase().includes(q) || n.code.toLowerCase().includes(q) || n.children.some((c) => this.subtreeMatches(c, q));
  }

  isMatch(n: CategoryNode): boolean {
    const q = this.treeSearch.trim().toLowerCase();
    return !!q && (n.name.toLowerCase().includes(q) || n.code.toLowerCase().includes(q));
  }

  get treeHasResults(): boolean {
    return this.visibleChildren(this.tree).length > 0;
  }

  // ===== Category details =====

  private loadDetailForm(): void {
    const n = this.selected;
    this.detailForm = {
      name: n.name,
      code: n.code,
      parentId: this.parentOf(n.id)?.id ?? null,
      description: n.description,
      status: n.status,
    };
    this.submitted = false;
  }

  startEdit(): void {
    this.editing = true;
  }

  cancelEdit(): void {
    this.editing = false;
    this.loadDetailForm();
  }

  saveDetails(): void {
    this.submitted = true;
    const error = this.validate(this.detailForm, this.selectedId);
    if (error) {
      this.showToast(error);
      return;
    }
    const n = this.selected;
    n.name = this.detailForm.name.trim();
    n.code = this.detailForm.code.trim().toUpperCase();
    n.description = this.detailForm.description.trim();
    n.status = this.detailForm.status;
    this.editing = false;
    this.loadDetailForm();
    this.showToast(`${n.name} updated.`);
  }

  isCodeTaken(code: string, exceptId: number | null): boolean {
    const c = code.trim().toUpperCase();
    let taken = false;
    this.walk(this.tree, (n) => {
      if (n.id !== exceptId && n.code.toUpperCase() === c) taken = true;
    });
    return !!c && taken;
  }

  private validate(form: CategoryForm, exceptId: number | null): string | null {
    if (!form.name.trim()) return 'Category name is required.';
    if (!form.code.trim()) return 'Category code is required.';
    if (this.isCodeTaken(form.code, exceptId)) return `Code "${form.code.trim().toUpperCase()}" is already used by another category.`;
    return null;
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.showToast('Please choose a JPG or PNG image.');
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      this.showToast('Image must be 2 MB or smaller.');
      return;
    }
    const target = this.selected;
    const reader = new FileReader();
    reader.onload = () => {
      target.image = reader.result as string;
      this.showToast('Category image updated.');
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    this.selected.image = undefined;
  }

  // ===== Delete =====

  askDelete(n: CategoryNode): void {
    this.openMenuId = null;
    const assets = this.assetCount(n);
    if (assets > 0) {
      this.showToast(`${n.name} has ${assets} asset${assets === 1 ? '' : 's'} assigned. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = n;
  }

  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target) return;
    const parent = this.parentOf(target.id);
    const siblings = parent ? parent.children : this.tree;
    siblings.splice(siblings.indexOf(target), 1);
    this.selectedRows.delete(target.id);

    if (this.lineage(this.selectedId).length === 0) {
      // The selected category (or one of its ancestors) was removed.
      this.selectedId = (parent ?? this.tree[0]).id;
      this.editing = false;
      this.loadDetailForm();
    }
    this.deleteTarget = null;
    this.showToast(`${target.name} deleted.`);
  }

  // ===== Add category modal =====

  openAddModal(parentId: number | null): void {
    this.modalForm = { ...EMPTY_FORM, parentId };
    this.modalSubmitted = false;
    this.modalCodeTouched = false;
    this.modalOpen = true;
  }

  closeModal(): void {
    this.modalOpen = false;
  }

  /** Suggests a code from the name until the user types their own. */
  onModalNameChange(): void {
    if (this.modalCodeTouched) return;
    const words = this.modalForm.name.trim().split(/\s+/).filter(Boolean);
    this.modalForm.code = (words.length > 1 ? words.map((w) => w[0]).join('') : (words[0] ?? '').slice(0, 3)).toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  modalCodeTouched = false;

  saveModal(): void {
    this.modalSubmitted = true;
    const error = this.validate(this.modalForm, null);
    if (error) return;

    const parent = this.find(this.modalForm.parentId);
    const created = node(this.modalForm.name.trim(), this.modalForm.code.trim().toUpperCase(), parent?.icon ?? 'other', this.modalForm.description.trim(), {
      status: this.modalForm.status,
    });
    if (parent) {
      parent.children.push(created);
      this.expanded.add(parent.id);
    } else {
      this.tree.push(created);
    }
    this.modalOpen = false;
    this.modalCodeTouched = false;
    this.showToast(`${created.name} added${parent ? ` under ${parent.name}` : ''}.`);
  }

  // ===== Subcategories tab =====

  get filteredSubs(): CategoryNode[] {
    const q = this.subSearch.trim().toLowerCase();
    let rows = this.selected.children.filter(
      (c) =>
        (!q || [c.name, c.code, c.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.subStatus || c.status === this.subStatus),
    );
    if (this.subSortDir) {
      const dir = this.subSortDir === 'asc' ? 1 : -1;
      rows = [...rows].sort((a, b) => a.name.localeCompare(b.name) * dir);
    }
    return rows;
  }

  get pageSubs(): CategoryNode[] {
    const start = (this.page - 1) * this.pageSize;
    return this.filteredSubs.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredSubs.length / this.pageSize));
  }

  get rangeStart(): number {
    return this.filteredSubs.length ? (this.page - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.page * this.pageSize, this.filteredSubs.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onSubFilterChange(): void {
    this.page = 1;
  }

  toggleSubSort(): void {
    this.subSortDir = this.subSortDir === null ? 'asc' : this.subSortDir === 'asc' ? 'desc' : null;
  }

  get subSortIcon() {
    if (!this.subSortDir) return this.icons.ChevronsUpDown;
    return this.subSortDir === 'asc' ? this.icons.ChevronUp : this.icons.ChevronDown;
  }

  get allOnPageSelected(): boolean {
    return this.pageSubs.length > 0 && this.pageSubs.every((r) => this.selectedRows.has(r.id));
  }

  get someOnPageSelected(): boolean {
    return !this.allOnPageSelected && this.pageSubs.some((r) => this.selectedRows.has(r.id));
  }

  toggleAllOnPage(): void {
    const selectAll = !this.allOnPageSelected;
    for (const r of this.pageSubs) {
      if (selectAll) this.selectedRows.add(r.id);
      else this.selectedRows.delete(r.id);
    }
  }

  toggleRow(id: number): void {
    if (this.selectedRows.has(id)) this.selectedRows.delete(id);
    else this.selectedRows.add(id);
  }

  setStatusForSelected(status: CategoryStatus): void {
    const rows = this.selected.children.filter((c) => this.selectedRows.has(c.id));
    rows.forEach((c) => (c.status = status));
    this.selectedRows.clear();
    this.showToast(`${rows.length} subcategor${rows.length === 1 ? 'y' : 'ies'} marked ${status.toLowerCase()}.`);
  }

  toggleStatus(n: CategoryNode): void {
    this.openMenuId = null;
    n.status = n.status === 'Active' ? 'Inactive' : 'Active';
    this.showToast(`${n.name} is now ${n.status.toLowerCase()}.`);
  }

  editSub(n: CategoryNode): void {
    this.openMenuId = null;
    this.selectCategory(n);
    this.editing = true;
  }

  toggleMenu(id: number, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenuId = this.openMenuId === id ? null : id;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenuId = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenuId = null;
    this.modalOpen = false;
    this.deleteTarget = null;
  }

  // ===== Custom fields & attributes (own + inherited from parents) =====

  private collect<T>(pick: (n: CategoryNode) => T[]): Inherited<T>[] {
    return this.lineage(this.selectedId).flatMap((n) =>
      pick(n).map((item) => ({ item, from: n.name, inherited: n.id !== this.selectedId })),
    );
  }

  get fieldRows(): Inherited<CustomField>[] {
    return this.collect((n) => n.customFields);
  }

  get attributeRows(): Inherited<Attribute>[] {
    return this.collect((n) => n.attributes);
  }

  addField(): void {
    const name = this.newField.name.trim();
    if (!name) return;
    if (this.fieldRows.some((r) => r.item.name.toLowerCase() === name.toLowerCase())) {
      this.showToast(`A field named "${name}" already exists.`);
      return;
    }
    this.selected.customFields.push({ ...this.newField, name });
    this.newField = { name: '', type: 'Text', required: false };
  }

  removeField(f: CustomField): void {
    const list = this.selected.customFields;
    list.splice(list.indexOf(f), 1);
  }

  addAttribute(): void {
    const name = this.newAttribute.name.trim();
    if (!name) return;
    if (this.attributeRows.some((r) => r.item.name.toLowerCase() === name.toLowerCase())) {
      this.showToast(`An attribute named "${name}" already exists.`);
      return;
    }
    this.selected.attributes.push({ name, unit: this.newAttribute.unit.trim(), example: this.newAttribute.example.trim() });
    this.newAttribute = { name: '', unit: '', example: '' };
  }

  removeAttribute(a: Attribute): void {
    const list = this.selected.attributes;
    list.splice(list.indexOf(a), 1);
  }

  // ===== Related assets =====

  /** Asset counts broken down by the leaf categories under the selection. */
  get assetBreakdown(): { node: CategoryNode; path: string; count: number }[] {
    const rows: { node: CategoryNode; path: string; count: number }[] = [];
    const visit = (n: CategoryNode, path: string[]) => {
      if (n.ownAssets) rows.push({ node: n, path: path.join(' › '), count: n.ownAssets });
      n.children.forEach((c) => visit(c, [...path, c.name]));
    };
    visit(this.selected, []);
    return rows.sort((a, b) => b.count - a.count);
  }

  openAssetList(): void {
    this.router.navigate(['/assets/list'], { queryParams: { category: this.selected.code } });
  }

  // ===== Misc =====

  trackById(_: number, n: CategoryNode): number {
    return n.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
