import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
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
import { apiErrorMessage } from '../services/asset-api';
import {
  AssetCategoriesService,
  AppCategory,
  CategoryAttribute,
  CategoryCustomField,
  CategoryFormValue,
  CategoryStatus,
  FieldType,
} from './asset-categories.service';

type DetailTab = 'subcategories' | 'fields' | 'attributes' | 'assets';
type CustomField = CategoryCustomField;
type Attribute = CategoryAttribute;

interface CategoryNode {
  id: string;
  parentId: string | null;
  name: string;
  code: string;
  description: string;
  status: CategoryStatus;
  icon: keyof AssetCategories['categoryIcons'];
  /** Image (URL or data URL) stored on the category; the icon is shown when absent. */
  image?: string;
  /** Assets registered directly against this category (from the asset registrations API). */
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
  parentId: string | null;
  description: string;
  status: CategoryStatus;
}

/** Icon picked from keywords in the category name (the API stores no icon). */
const ICON_KEYWORDS: [RegExp, CategoryNode['icon']][] = [
  [/machin|equipment|plant/i, 'machinery'],
  [/generat|power gen/i, 'generator'],
  [/transformer/i, 'transformer'],
  [/\bups\b|battery/i, 'ups'],
  [/switchgear|switch gear/i, 'switchgear'],
  [/panel|electric/i, 'panel'],
  [/chiller|hvac|cool|refrig/i, 'chiller'],
  [/\bahu\b|air handling|fan/i, 'ahu'],
  [/\bac\b|air condition/i, 'ac'],
  [/vrf|ventilat/i, 'vrf'],
  [/forklift|material handling/i, 'forklift'],
  [/conveyor/i, 'conveyor'],
  [/crane|container/i, 'crane'],
  [/pallet/i, 'pallet'],
  [/motor/i, 'motor'],
  [/compressor/i, 'compressor'],
  [/pump/i, 'pump'],
  [/laptop|notebook/i, 'laptop'],
  [/desktop|monitor|workstation/i, 'desktop'],
  [/server/i, 'server'],
  [/network|router|switch/i, 'network'],
  [/printer|scanner/i, 'printer'],
  [/\bit\b|computer|hardware/i, 'it'],
  [/cctv|camera|surveillance/i, 'cctv'],
  [/alarm|detector/i, 'alarm'],
  [/access|door|card/i, 'access'],
  [/fire/i, 'fire'],
  [/security|safety/i, 'security'],
  [/cabinet|storage|filing/i, 'cabinet'],
  [/furniture|fixture|chair|desk|table/i, 'furniture'],
  [/truck|van|lorry/i, 'truck'],
  [/vehicle|fleet|car\b/i, 'vehicle'],
  [/drill|power tool/i, 'drill'],
  [/measur|instrument|gauge|meter/i, 'gauge'],
  [/tool/i, 'tools'],
];

function iconFor(name: string): CategoryNode['icon'] {
  return ICON_KEYWORDS.find(([re]) => re.test(name))?.[1] ?? 'other';
}

/** Shown while nothing is loaded so the details pane can render. */
const PLACEHOLDER: CategoryNode = {
  id: '',
  parentId: null,
  name: '',
  code: '',
  description: '',
  status: 'Active',
  icon: 'other',
  ownAssets: 0,
  customFields: [],
  attributes: [],
  children: [],
};

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

  tree: CategoryNode[] = [];
  expanded = new Set<string>();
  selectedId = '';
  treeSearch = '';

  /** True while the category list is being fetched. */
  loading = false;
  /** Set when the category list could not be loaded. */
  loadError = '';
  /** True while a create/update/delete request is in flight (guards double submits). */
  saving = false;

  /** Registered asset counts keyed by lower-cased category id / name / code. */
  private assetCounts = new Map<string, number>();

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
  selectedRows = new Set<string>();
  openMenuId: string | null = null;

  // Add-category modal
  modalOpen = false;
  modalForm: CategoryForm = { ...EMPTY_FORM };
  modalSubmitted = false;

  // Add custom field / attribute inline rows
  newField: { name: string; type: FieldType; required: boolean } = { name: '', type: 'Text', required: false };
  newAttribute: { name: string; unit: string; example: string } = { name: '', unit: '', example: '' };

  deleteTarget: CategoryNode | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private readonly router: Router,
    private readonly api: AssetCategoriesService,
  ) {
    this.load(true);
    this.api.getAssetCountsByCategory().subscribe({
      next: (counts) => {
        this.assetCounts = counts;
        this.walk(this.tree, (n) => (n.ownAssets = this.ownAssetsOf(n)));
      },
      // Asset counts are informational; the page works without them.
      error: () => {},
    });
  }

  // ===== Loading =====

  /** Fetches every category and rebuilds the tree, keeping the current selection when it still exists. */
  load(initial = false): void {
    this.loading = true;
    this.loadError = '';
    this.api.getAll().subscribe({
      next: (list) => {
        this.loading = false;
        this.tree = this.buildTree(list);
        if (initial || !this.find(this.selectedId)) {
          const first = this.tree[0];
          this.selectedId = first?.id ?? '';
          if (initial && first) {
            // Open the first branch fully, as in the design.
            this.expanded.add(first.id);
            first.children.filter((c) => c.children.length).forEach((c) => this.expanded.add(c.id));
          }
          this.editing = false;
        }
        if (!this.editing) this.loadDetailForm();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load asset categories.');
      },
    });
  }

  private buildTree(list: AppCategory[]): CategoryNode[] {
    const nodes = new Map<string, CategoryNode>();
    for (const c of list) {
      nodes.set(c.id, {
        id: c.id,
        parentId: c.parentId,
        name: c.name,
        code: c.code,
        description: c.description,
        status: c.status,
        icon: iconFor(c.name),
        image: c.imageUrl ?? undefined,
        ownAssets: 0,
        customFields: c.customFields,
        attributes: c.attributes,
        children: [],
      });
    }
    const roots: CategoryNode[] = [];
    for (const n of nodes.values()) {
      n.ownAssets = this.ownAssetsOf(n);
      const parent = n.parentId ? nodes.get(n.parentId) : undefined;
      if (parent) {
        parent.children.push(n);
      } else {
        roots.push(n);
      }
    }
    // Sub-categories inherit their parent's icon when their own name gives no hint.
    const inheritIcons = (items: CategoryNode[], parentIcon: CategoryNode['icon'] | null) => {
      for (const n of items) {
        if (n.icon === 'other' && parentIcon) n.icon = parentIcon;
        inheritIcons(n.children, n.icon);
      }
    };
    inheritIcons(roots, null);
    return roots;
  }

  private ownAssetsOf(n: CategoryNode): number {
    const keys = new Set([n.id, n.name, n.code].map((k) => k.trim().toLowerCase()));
    let total = 0;
    keys.forEach((k) => (total += this.assetCounts.get(k) ?? 0));
    return total;
  }

  private toRequest(n: CategoryNode, overrides: Partial<CategoryFormValue> = {}): CategoryFormValue {
    return {
      parentId: n.parentId,
      name: n.name,
      code: n.code,
      description: n.description,
      status: n.status,
      imageUrl: n.image ?? null,
      ...overrides,
    };
  }

  // ===== Tree helpers =====

  private walk(nodes: CategoryNode[], fn: (n: CategoryNode, parent: CategoryNode | null) => void, parent: CategoryNode | null = null): void {
    for (const n of nodes) {
      fn(n, parent);
      this.walk(n.children, fn, n);
    }
  }

  private find(id: string | null): CategoryNode | null {
    let found: CategoryNode | null = null;
    if (!id) return null;
    this.walk(this.tree, (n) => {
      if (n.id === id) found = n;
    });
    return found;
  }

  private parentOf(id: string): CategoryNode | null {
    let parent: CategoryNode | null = null;
    this.walk(this.tree, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  /** Root-first chain of ancestors, including the node itself. */
  private lineage(id: string): CategoryNode[] {
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
    return this.find(this.selectedId) ?? this.tree[0] ?? PLACEHOLDER;
  }

  /** False while no category exists / is loaded. */
  get hasSelection(): boolean {
    return !!this.selected.id;
  }

  get selectedPath(): string {
    return this.lineage(this.selectedId).map((n) => n.name).join(' › ');
  }

  /** Every category as a flat, indented list for the "Parent Category" picker. */
  get parentOptions(): { id: string; label: string }[] {
    const out: { id: string; label: string }[] = [];
    const visit = (nodes: CategoryNode[], depth: number) => {
      for (const n of nodes) {
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
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
      parentId: n.parentId,
      description: n.description,
      status: n.status,
    };
    this.submitted = false;
  }

  startEdit(): void {
    if (!this.hasSelection) return;
    this.editing = true;
  }

  cancelEdit(): void {
    this.editing = false;
    this.loadDetailForm();
  }

  saveDetails(): void {
    if (!this.hasSelection || this.saving) return;
    this.submitted = true;
    const error = this.validate(this.detailForm, this.selectedId);
    if (error) {
      this.showToast(error);
      return;
    }
    const n = this.selected;
    this.saving = true;
    this.api
      .update(
        n.id,
        this.toRequest(n, {
          name: this.detailForm.name.trim(),
          code: this.detailForm.code.trim().toUpperCase(),
          description: this.detailForm.description.trim(),
          status: this.detailForm.status,
        }),
      )
      .subscribe({
        next: (saved) => {
          this.saving = false;
          this.editing = false;
          this.showToast(`${saved.name} updated.`);
          this.load();
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'Could not save the category.'));
        },
      });
  }

  isCodeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    let taken = false;
    this.walk(this.tree, (n) => {
      if (n.id !== exceptId && n.code.toUpperCase() === c) taken = true;
    });
    return !!c && taken;
  }

  private validate(form: CategoryForm, exceptId: string | null): string | null {
    if (!form.name.trim()) return 'Category name is required.';
    if (!form.code.trim()) return 'Category code is required.';
    if (this.isCodeTaken(form.code, exceptId)) return `Code "${form.code.trim().toUpperCase()}" is already used by another category.`;
    return null;
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !this.hasSelection) return;
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
    reader.onload = () => this.saveImage(target, reader.result as string, 'Category image updated.');
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    if (this.hasSelection) this.saveImage(this.selected, null, 'Category image removed.');
  }

  /** The image is stored on the category itself (PUT with imageUrl). */
  private saveImage(target: CategoryNode, image: string | null, message: string): void {
    this.api.update(target.id, this.toRequest(target, { imageUrl: image })).subscribe({
      next: (saved) => {
        target.image = saved.imageUrl ?? undefined;
        this.showToast(message);
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not update the category image.')),
    });
  }

  // ===== Delete =====

  askDelete(n: CategoryNode): void {
    this.openMenuId = null;
    if (!n.id) return;
    const assets = this.assetCount(n);
    if (assets > 0) {
      this.showToast(`${n.name} has ${assets} asset${assets === 1 ? '' : 's'} assigned. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = n;
  }

  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target || this.saving) return;
    this.saving = true;
    this.api.delete(target.id).subscribe({
      next: () => {
        this.saving = false;
        this.selectedRows.delete(target.id);
        if (this.lineage(this.selectedId).some((n) => n.id === target.id)) {
          // The selected category (or one of its ancestors) was removed.
          this.selectedId = target.parentId ?? '';
          this.editing = false;
        }
        this.deleteTarget = null;
        this.showToast(`${target.name} deleted.`);
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.deleteTarget = null;
        this.showToast(apiErrorMessage(err, `Could not delete ${target.name}.`));
      },
    });
  }

  // ===== Add category modal =====

  openAddModal(parentId: string | null): void {
    this.modalForm = { ...EMPTY_FORM, parentId: parentId || null };
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
    if (error || this.saving) return;

    const parent = this.find(this.modalForm.parentId);
    this.saving = true;
    this.api
      .create({
        parentId: parent?.id ?? null,
        name: this.modalForm.name.trim(),
        code: this.modalForm.code.trim().toUpperCase(),
        description: this.modalForm.description.trim(),
        status: this.modalForm.status,
      })
      .subscribe({
        next: (created) => {
          this.saving = false;
          if (parent) this.expanded.add(parent.id);
          this.modalOpen = false;
          this.modalCodeTouched = false;
          this.showToast(`${created.name} added${parent ? ` under ${parent.name}` : ''}.`);
          this.load();
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'Could not add the category.'));
        },
      });
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

  toggleRow(id: string): void {
    if (this.selectedRows.has(id)) this.selectedRows.delete(id);
    else this.selectedRows.add(id);
  }

  setStatusForSelected(status: CategoryStatus): void {
    const rows = this.selected.children.filter((c) => this.selectedRows.has(c.id));
    if (!rows.length) return;
    forkJoin(rows.map((c) => this.api.update(c.id, this.toRequest(c, { status })))).subscribe({
      next: () => {
        rows.forEach((c) => (c.status = status));
        this.selectedRows.clear();
        this.showToast(`${rows.length} subcategor${rows.length === 1 ? 'y' : 'ies'} marked ${status.toLowerCase()}.`);
      },
      error: (err) => {
        this.showToast(apiErrorMessage(err, 'Could not update the status of every selected subcategory.'));
        this.load();
      },
    });
  }

  toggleStatus(n: CategoryNode): void {
    this.openMenuId = null;
    const status: CategoryStatus = n.status === 'Active' ? 'Inactive' : 'Active';
    this.api.update(n.id, this.toRequest(n, { status })).subscribe({
      next: (saved) => {
        n.status = saved.status;
        if (n.id === this.selectedId && !this.editing) this.loadDetailForm();
        this.showToast(`${n.name} is now ${n.status.toLowerCase()}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not change the status of ${n.name}.`)),
    });
  }

  editSub(n: CategoryNode): void {
    this.openMenuId = null;
    this.selectCategory(n);
    this.editing = true;
  }

  toggleMenu(id: string, event: MouseEvent): void {
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
    if (!name || !this.hasSelection || this.saving) return;
    if (this.fieldRows.some((r) => r.item.name.toLowerCase() === name.toLowerCase())) {
      this.showToast(`A field named "${name}" already exists.`);
      return;
    }
    const target = this.selected;
    this.saving = true;
    this.api.addCustomField(target.id, { ...this.newField, name }).subscribe({
      next: (field) => {
        this.saving = false;
        target.customFields.push(field);
        this.newField = { name: '', type: 'Text', required: false };
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not add the custom field.'));
      },
    });
  }

  removeField(f: CustomField): void {
    const target = this.selected;
    this.api.deleteCustomField(target.id, f.id).subscribe({
      next: () => {
        const list = target.customFields;
        const i = list.indexOf(f);
        if (i >= 0) list.splice(i, 1);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not remove ${f.name}.`)),
    });
  }

  addAttribute(): void {
    const name = this.newAttribute.name.trim();
    if (!name || !this.hasSelection || this.saving) return;
    if (this.attributeRows.some((r) => r.item.name.toLowerCase() === name.toLowerCase())) {
      this.showToast(`An attribute named "${name}" already exists.`);
      return;
    }
    const target = this.selected;
    this.saving = true;
    this.api.addAttribute(target.id, { name, unit: this.newAttribute.unit.trim(), example: this.newAttribute.example.trim() }).subscribe({
      next: (attribute) => {
        this.saving = false;
        target.attributes.push(attribute);
        this.newAttribute = { name: '', unit: '', example: '' };
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not add the attribute.'));
      },
    });
  }

  removeAttribute(a: Attribute): void {
    const target = this.selected;
    this.api.deleteAttribute(target.id, a.id).subscribe({
      next: () => {
        const list = target.attributes;
        const i = list.indexOf(a);
        if (i >= 0) list.splice(i, 1);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not remove ${a.name}.`)),
    });
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

  trackById(_: number, n: CategoryNode): string {
    return n.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
