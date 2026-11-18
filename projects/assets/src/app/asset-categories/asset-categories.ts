import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { Observable, finalize, forkJoin } from 'rxjs';
import {
  AssetCategory, AssetCategoryAttribute, AssetCategoryCustomField, AssetCategoryService, CATEGORY_FIELD_TYPES, CategoryFieldType,
  CategoryStatus,
} from '../services/asset-category/asset-category';
import { FilterSelect, FilterSelectOption } from '../shared/filter-select/filter-select';

type DetailTab = 'subcategories' | 'fields' | 'attributes' | 'assets';

interface CategoryNode {
  id: string;
  name: string;
  code: string;
  description: string;
  status: CategoryStatus;
  icon: keyof AssetCategories['categoryIcons'];
  /** Image URL (or data URL) saved on the category; the icon is shown when absent. */
  image?: string;
  /** Own and inherited custom fields / attributes, as the API returns them. */
  customFields: AssetCategoryCustomField[];
  attributes: AssetCategoryAttribute[];
  children: CategoryNode[];
  /** The record as last loaded, for PUTs that change one thing. */
  record: AssetCategory;
}

interface CategoryForm {
  name: string;
  code: string;
  parentId: string | null;
  description: string;
  status: CategoryStatus;
}

/** Matches the popup-out animation length in the stylesheet. */
const POPUP_CLOSE_MS = 180;

const EMPTY_FORM: CategoryForm = { name: '', code: '', parentId: null, description: '', status: 'Active' };

/** Keyword → icon, so categories from the API get a fitting icon. First match wins. */
const ICON_KEYWORDS: [RegExp, string][] = [
  [/generat|power/i, 'generator'], [/transformer/i, 'transformer'], [/\bups\b/i, 'ups'], [/switchgear/i, 'switchgear'],
  [/panel/i, 'panel'], [/chiller|hvac|cooling/i, 'chiller'], [/ahu|air handling/i, 'ahu'], [/split|\bac\b|air con/i, 'ac'],
  [/vrf/i, 'vrf'], [/forklift|material handling/i, 'forklift'], [/conveyor/i, 'conveyor'], [/crane/i, 'crane'],
  [/pallet/i, 'pallet'], [/motor/i, 'motor'], [/compressor/i, 'compressor'], [/pump/i, 'pump'], [/laptop|notebook/i, 'laptop'],
  [/desktop|monitor|workstation/i, 'desktop'], [/server/i, 'server'], [/network|switch|router/i, 'network'],
  [/printer|scanner/i, 'printer'], [/\bit\b|computer|cpu/i, 'it'], [/cctv|camera/i, 'cctv'], [/alarm/i, 'alarm'],
  [/access/i, 'access'], [/fire/i, 'fire'], [/security|safety/i, 'security'], [/cabinet|storage/i, 'cabinet'],
  [/furniture|chair|desk/i, 'furniture'], [/truck|van/i, 'truck'], [/vehicle|car|fleet/i, 'vehicle'], [/drill|power tool/i, 'drill'],
  [/measur|instrument|gauge|meter/i, 'gauge'], [/tool/i, 'tools'], [/machin|equipment/i, 'machinery'],
];

function toStatus(status: string | null | undefined): CategoryStatus {
  return status?.trim().toLowerCase() === 'inactive' ? 'Inactive' : 'Active';
}


@Component({
  selector: 'app-asset-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FilterSelect],
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

  readonly fieldTypes: CategoryFieldType[] = CATEGORY_FIELD_TYPES;
  /** How the API's field types are shown. */
  readonly fieldTypeLabels: Record<CategoryFieldType, string> = {
    Text: 'Text', Number: 'Number', Date: 'Date', Dropdown: 'Dropdown', Checkbox: 'Yes / No',
  };
  readonly fieldTypeChoices: FilterSelectOption[] = CATEGORY_FIELD_TYPES.map((t) => ({ value: t, label: this.fieldTypeLabels[t] }));
  readonly statusChoices: CategoryStatus[] = ['Active', 'Inactive'];
  readonly pageSizeOptions = [5, 10, 20];

  private readonly api = inject(AssetCategoryService);
  private readonly destroyRef = inject(DestroyRef);

  tree: CategoryNode[] = [];
  loading = false;
  loadError = '';
  saving = false;
  expanded = new Set<string>();
  selectedId: string | null = null;
  treeSearch = '';

  // Details form (read-only until "Edit")

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
  /** True while a popup plays its close animation. */
  modalClosing = false;
  deleteClosing = false;
  private modalCloseTimer?: ReturnType<typeof setTimeout>;
  private deleteCloseTimer?: ReturnType<typeof setTimeout>;
  modalForm: CategoryForm = { ...EMPTY_FORM };
  modalSubmitted = false;

  // Add custom field / attribute inline rows
  newField: { name: string; type: CategoryFieldType; required: boolean } = { name: '', type: 'Text', required: false };
  newAttribute = { name: '', unit: '', example: '' };

  deleteTarget: CategoryNode | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    this.loadCategories();
  }

  // ===== Loading =====

  /**
   * GET /api/asset-categories and build the tree from parentCategoryId.
   * Keeps the selection and open branches where they still exist; `selectId` picks a category after a save.
   */
  loadCategories(selectId?: string): void {
    this.loading = true;
    this.loadError = '';
    this.api
      .getAll()
      .pipe(finalize(() => (this.loading = false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => {
          const firstLoad = !this.tree.length;
          this.tree = this.buildTree(rows ?? []);
          if (selectId) this.selectedId = selectId;
          if (!this.find(this.selectedId)) this.selectedId = this.tree[0]?.id ?? null;
          if (firstLoad && this.tree[0]) {
            // Open the first branch, as in the design.
            this.expanded.add(this.tree[0].id);
            this.tree[0].children.filter((c) => c.children.length).forEach((c) => this.expanded.add(c.id));
          }
          if (this.selectedId) this.lineage(this.selectedId).slice(0, -1).forEach((a) => this.expanded.add(a.id));
          [...this.expanded].forEach((id) => !this.find(id) && this.expanded.delete(id));
          [...this.selectedRows].forEach((id) => !this.find(id) && this.selectedRows.delete(id));
        },
        error: (err) => (this.loadError = this.api.errorMessage(err, 'Could not load asset categories.')),
      });
  }

  private buildTree(rows: AssetCategory[]): CategoryNode[] {
    const nodes = new Map<string, CategoryNode>();
    for (const r of rows) nodes.set(r.id, this.toNode(r));
    const roots: CategoryNode[] = [];
    for (const r of rows) {
      const n = nodes.get(r.id)!;
      const parent = r.parentCategoryId ? nodes.get(r.parentCategoryId) : undefined;
      // A missing parent (e.g. deleted) leaves the category at the top level rather than hiding it.
      (parent ? parent.children : roots).push(n);
    }
    const byName = (list: CategoryNode[]) => {
      list.sort((a, b) => a.name.localeCompare(b.name));
      list.forEach((n) => byName(n.children));
    };
    byName(roots);
    return roots;
  }

  private toNode(r: AssetCategory): CategoryNode {
    const iconFor = (text: string) => ICON_KEYWORDS.find(([re]) => re.test(text))?.[1] as CategoryNode['icon'] | undefined;
    return {
      id: r.id,
      name: r.name,
      code: r.code,
      description: r.description ?? '',
      status: toStatus(r.status),
      icon: iconFor(r.name) ?? iconFor(r.parentCategoryName ?? '') ?? 'other',
      image: r.imageUrl || undefined,
      customFields: r.customFields ?? [],
      attributes: r.attributes ?? [],
      children: [],
      record: r,
    };
  }

  /** Runs a write, then reloads; shows the API's message on failure. */
  private write<T>(call: Observable<T>, success: (result: T) => string, selectId?: string, after?: (result: T) => void): void {
    this.saving = true;
    call.pipe(finalize(() => (this.saving = false)), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        after?.(result);
        this.showToast(success(result));
        this.loadCategories(selectId);
      },
      error: (err) => this.showToast(this.api.errorMessage(err, 'Something went wrong. Please try again.')),
    });
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
    if (id === null) return null;
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
  private lineage(id: string | null): CategoryNode[] {
    const chain: CategoryNode[] = [];
    let current = this.find(id);
    while (current) {
      chain.unshift(current);
      current = this.parentOf(current.id);
    }
    return chain;
  }

  /** The selected category; null only while nothing is loaded. */
  get selected(): CategoryNode | null {
    return this.find(this.selectedId) ?? this.tree[0] ?? null;
  }

  /** Top-level (parent) categories only, for the subcategory popup's "Parent Category" picker. */
  get parentOptions(): FilterSelectOption[] {
    return this.tree.filter((n) => n.id !== this.modalEditId).map((n) => ({ value: n.id, label: n.name }));
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
    this.selectedId = n.id;
    this.lineage(n.id).slice(0, -1).forEach((a) => this.expanded.add(a.id));
    this.activeTab = 'subcategories';
    this.subSearch = '';
    this.subStatus = '';
    this.page = 1;
    this.selectedRows.clear();
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

  // ===== Validation =====

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

  // ===== Delete =====

  askDelete(n: CategoryNode | null): void {
    this.openMenuId = null;
    if (!n) return;
    clearTimeout(this.deleteCloseTimer);
    this.deleteClosing = false;
    this.deleteTarget = n;
  }

  /** DELETE /api/asset-categories/{id}; the API refuses while sub-categories, asset types or custom fields remain. */
  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target || this.saving) return;
    const parent = this.parentOf(target.id);
    const wasSelected = this.lineage(this.selectedId).some((a) => a.id === target.id);
    this.write(
      this.api.delete(target.id),
      () => `${target.name} deleted.`,
      wasSelected ? parent?.id : undefined,
      () => {
        this.closeDelete();
        this.selectedRows.delete(target.id);
        if (wasSelected) this.selectedId = parent?.id ?? null;
      },
    );
  }

  // ===== Add category modal =====

  openAddModal(parentId: string | null): void {
    // The picker lists top-level categories only, so start on the top-level ancestor of the chosen one.
    const topLevel = parentId !== null ? this.lineage(parentId)[0]?.id ?? null : null;
    this.modalForm = { ...EMPTY_FORM, parentId: topLevel };
    // Opened as "Add Subcategory": shows the parent picker. Top-level "Add Category" has none.
    this.modalIsSub = parentId !== null;
    this.modalEditId = null;
    this.modalSubmitted = false;
    this.modalCodeTouched = false;
    this.cancelModalClose();
    this.modalOpen = true;
  }

  /** Pencil icon / "Edit" menu item: same popup, filled with the category, saved with PUT. */
  openEditModal(n: CategoryNode, event?: Event): void {
    event?.stopPropagation();
    this.openMenuId = null;
    const parentId = this.parentOf(n.id)?.id ?? null;
    this.modalForm = { name: n.name, code: n.code, parentId, description: n.description, status: n.status };
    this.modalIsSub = parentId !== null;
    this.modalEditId = n.id;
    this.modalSubmitted = false;
    this.modalCodeTouched = true; // keep the saved code; don't re-suggest it from the name
    this.cancelModalClose();
    this.modalOpen = true;
  }

  get modalEditNode(): CategoryNode | null {
    return this.find(this.modalEditId);
  }

  /** Delete button in the edit popup. */
  deleteFromModal(): void {
    const n = this.modalEditNode;
    if (!n) return;
    this.closeModal();
    this.askDelete(n);
  }

  /** Plays the close animation, then removes the popup. */
  closeModal(): void {
    if (!this.modalOpen || this.modalClosing) return;
    this.modalClosing = true;
    this.modalCloseTimer = setTimeout(() => {
      this.modalOpen = false;
      this.modalClosing = false;
    }, POPUP_CLOSE_MS);
  }

  private cancelModalClose(): void {
    clearTimeout(this.modalCloseTimer);
    this.modalClosing = false;
  }

  /** Same for the delete confirmation. */
  closeDelete(): void {
    if (!this.deleteTarget || this.deleteClosing) return;
    this.deleteClosing = true;
    this.deleteCloseTimer = setTimeout(() => {
      this.deleteTarget = null;
      this.deleteClosing = false;
    }, POPUP_CLOSE_MS);
  }

  /** Suggests a code from the name until the user types their own. */
  onModalNameChange(): void {
    if (this.modalCodeTouched) return;
    const words = this.modalForm.name.trim().split(/\s+/).filter(Boolean);
    this.modalForm.code = (words.length > 1 ? words.map((w) => w[0]).join('') : (words[0] ?? '').slice(0, 3)).toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  modalCodeTouched = false;
  modalIsSub = false;
  /** Category being edited in the popup; null when adding. */
  modalEditId: string | null = null;

  /** POST /api/asset-categories (add) or PUT /api/asset-categories/{id} (edit). */
  saveModal(): void {
    this.modalSubmitted = true;
    if (this.validate(this.modalForm, this.modalEditId) || this.saving) return;
    const f = this.modalForm;
    const parent = this.find(f.parentId);
    const values = {
      parentCategoryId: f.parentId,
      name: f.name.trim(),
      code: f.code.trim().toUpperCase(),
      description: f.description.trim() || null,
      status: f.status,
    };

    const editing = this.modalEditNode;
    if (editing) {
      this.write(
        this.api.update(editing.id, { ...this.api.toUpdateRequest(editing.record), ...values }),
        (saved) => `${saved.name} updated.`,
        undefined,
        () => {
          this.closeModal();
          if (parent) this.expanded.add(parent.id);
        },
      );
      return;
    }

    this.write(
      this.api.create(values),
      (created) => `${created.name} added${parent ? ` under ${parent.name}` : ''}.`,
      undefined,
      (created) => {
        this.closeModal();
        this.modalCodeTouched = false;
        if (parent) this.expanded.add(parent.id);
        // Stay on the parent so the new subcategory shows in its table; a new top-level category gets selected.
        if (!parent) this.selectedId = created.id;
      },
    );
  }

  // ===== Subcategories tab =====

  get filteredSubs(): CategoryNode[] {
    const q = this.subSearch.trim().toLowerCase();
    let rows = (this.selected?.children ?? []).filter(
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

  /** PUT each checked subcategory with the new status. */
  setStatusForSelected(status: CategoryStatus): void {
    const rows = (this.selected?.children ?? []).filter((c) => this.selectedRows.has(c.id) && c.status !== status);
    if (!rows.length || this.saving) {
      this.selectedRows.clear();
      return;
    }
    this.write(
      forkJoin(rows.map((c) => this.api.update(c.id, { ...this.api.toUpdateRequest(c.record), status }))),
      (saved) => `${saved.length} subcategor${saved.length === 1 ? 'y' : 'ies'} marked ${status.toLowerCase()}.`,
      undefined,
      () => this.selectedRows.clear(),
    );
  }

  toggleStatus(n: CategoryNode): void {
    this.openMenuId = null;
    const status: CategoryStatus = n.status === 'Active' ? 'Inactive' : 'Active';
    this.write(
      this.api.update(n.id, { ...this.api.toUpdateRequest(n.record), status }),
      (saved) => `${saved.name} is now ${status.toLowerCase()}.`,
    );
  }

  editSub(n: CategoryNode): void {
    this.openEditModal(n);
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
    this.closeModal();
    this.closeDelete();
  }

  // ===== Custom fields & attributes (the API lists inherited ones first) =====

  get fieldRows(): AssetCategoryCustomField[] {
    return this.selected?.customFields ?? [];
  }

  get attributeRows(): AssetCategoryAttribute[] {
    return this.selected?.attributes ?? [];
  }

  /** POST /api/asset-categories/{id}/custom-fields */
  addField(): void {
    const n = this.selected;
    const name = this.newField.name.trim();
    if (!n || !name || this.saving) return;
    if (this.fieldRows.some((r) => r.fieldName.toLowerCase() === name.toLowerCase())) {
      this.showToast(`A field named "${name}" already exists.`);
      return;
    }
    this.write(
      this.api.addCustomField(n.id, { fieldName: name, type: this.newField.type, required: this.newField.required }),
      (f) => `Field "${f.fieldName}" added.`,
      n.id,
      () => (this.newField = { name: '', type: 'Text', required: false }),
    );
  }

  /** DELETE /api/asset-categories/{id}/custom-fields/{fieldId} */
  removeField(f: AssetCategoryCustomField): void {
    if (this.saving) return;
    this.write(this.api.deleteCustomField(f.definedOnCategoryId, f.id), () => `Field "${f.fieldName}" removed.`);
  }

  /** POST /api/asset-categories/{id}/attributes */
  addAttribute(): void {
    const n = this.selected;
    const name = this.newAttribute.name.trim();
    if (!n || !name || this.saving) return;
    if (this.attributeRows.some((r) => r.attribute.toLowerCase() === name.toLowerCase())) {
      this.showToast(`An attribute named "${name}" already exists.`);
      return;
    }
    this.write(
      this.api.addAttribute(n.id, {
        attribute: name,
        unit: this.newAttribute.unit.trim() || null,
        exampleValue: this.newAttribute.example.trim() || null,
      }),
      (a) => `Attribute "${a.attribute}" added.`,
      n.id,
      () => (this.newAttribute = { name: '', unit: '', example: '' }),
    );
  }

  /** DELETE /api/asset-categories/{id}/attributes/{attributeId} */
  removeAttribute(a: AssetCategoryAttribute): void {
    if (this.saving) return;
    this.write(this.api.deleteAttribute(a.definedOnCategoryId, a.id), () => `Attribute "${a.attribute}" removed.`);
  }

  // ===== Related assets =====

  openAssetList(): void {
    if (this.selected) this.router.navigate(['/assets/list'], { queryParams: { category: this.selected.code } });
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
