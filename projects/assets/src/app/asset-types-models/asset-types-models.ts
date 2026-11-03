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
  Boxes,
  Car,
  Cctv,
  Check,
  ChevronLeft,
  ChevronRight,
  CircuitBoard,
  Cog,
  Container,
  Drill,
  EllipsisVertical,
  ExternalLink,
  Eye,
  Fan,
  FileStack,
  Flame,
  Forklift,
  Hexagon,
  ImageUp,
  Inbox,
  KeyRound,
  Laptop,
  Layers,
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
  Snowflake,
  SquarePen,
  ToggleRight,
  Trash2,
  Truck,
  Wind,
  X,
  Zap,
} from 'lucide-angular';
import { Observable, finalize, forkJoin } from 'rxjs';
import {
  AssetModelRecord, AssetTypeRecord, AssetTypes, TypeModelStatus, toStatus,
} from '../services/asset-types/asset-types';
import { AssetCategory, AssetCategoryService } from '../services/asset-category/asset-category';
import { AssetDropdownService, DropdownOption } from '../services/asset-dropdowns/asset-dropdown';
import { FilterSelect } from '../shared/filter-select/filter-select';

type Status = TypeModelStatus;

interface AssetType {
  id: string;
  name: string;
  code: string;
  categoryId: string;
  category: string;
  description: string;
  status: Status;
  icon: keyof AssetTypesModels['typeIcons'];
  image?: string;
  modelCount: number;
  /** The record as last loaded, for PUTs that change one thing. */
  record: AssetTypeRecord;
}

interface AssetModel {
  id: string;
  name: string;
  code: string;
  manufacturer: string;
  status: Status;
  record: AssetModelRecord;
}

interface TypeForm {
  name: string;
  code: string;
  categoryId: string;
  description: string;
  status: Status;
}

interface ModelForm {
  name: string;
  code: string;
  manufacturerId: string;
  status: Status;
}

/**
 * Client-side pagination state for one table. `page` is the requested page; reads clamp it to the
 * row count so deleting or filtering rows never leaves the table on an empty page.
 */
class Pager {
  page = 1;
  size = 10;

  totalPages(count: number): number {
    return Math.max(1, Math.ceil(count / this.size));
  }

  current(count: number): number {
    return Math.min(this.page, this.totalPages(count));
  }

  pages(count: number): number[] {
    return Array.from({ length: this.totalPages(count) }, (_, i) => i + 1);
  }

  slice<T>(rows: T[]): T[] {
    const start = (this.current(rows.length) - 1) * this.size;
    return rows.slice(start, start + this.size);
  }

  start(count: number): number {
    return count ? (this.current(count) - 1) * this.size + 1 : 0;
  }

  end(count: number): number {
    return Math.min(this.current(count) * this.size, count);
  }

  go(p: number, count: number): void {
    if (p >= 1 && p <= this.totalPages(count)) this.page = p;
  }
}

const EMPTY_TYPE: TypeForm = { name: '', code: '', categoryId: '', description: '', status: 'Active' };
const EMPTY_MODEL: ModelForm = { name: '', code: '', manufacturerId: '', status: 'Active' };
/** Matches the popup-out animation length in the stylesheet. */
const POPUP_CLOSE_MS = 180;

/** Keyword → icon, so types from the API get a fitting icon. First match wins. */
const ICON_KEYWORDS: [RegExp, string][] = [
  [/generat/i, 'generator'], [/transformer/i, 'transformer'], [/\bups\b/i, 'ups'], [/switchgear/i, 'switchgear'],
  [/panel/i, 'panel'], [/chiller/i, 'chiller'], [/ahu|air handling/i, 'ahu'], [/split|\bac\b|air con/i, 'ac'], [/vrf/i, 'vrf'],
  [/cctv|camera/i, 'cctv'], [/laptop|notebook/i, 'laptop'], [/desktop|workstation|monitor/i, 'desktop'], [/server/i, 'server'],
  [/network|switch|router/i, 'network'], [/printer|scanner/i, 'printer'], [/alarm/i, 'alarm'], [/access/i, 'access'],
  [/fire/i, 'fire'], [/forklift/i, 'forklift'], [/conveyor/i, 'conveyor'], [/crane/i, 'crane'], [/pallet/i, 'pallet'],
  [/truck|van/i, 'truck'], [/car|vehicle/i, 'car'], [/drill/i, 'drill'], [/meter|gauge|instrument/i, 'meter'],
  [/cabinet/i, 'cabinet'], [/furniture|chair|desk/i, 'furniture'],
];

function iconFor(text: string): AssetType['icon'] | undefined {
  return ICON_KEYWORDS.find(([re]) => re.test(text))?.[1] as AssetType['icon'] | undefined;
}

@Component({
  selector: 'app-asset-types-models',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FilterSelect],
  templateUrl: './asset-types-models.html',
  styleUrl: './asset-types-models.css',
})
export class AssetTypesModels {
  readonly icons = {
    Check, ChevronLeft, ChevronRight, EllipsisVertical, ExternalLink, Eye, FileStack, Hexagon, ImageUp, Inbox,
    Layers, Plus, Power, PowerOff, Search, SquarePen, Trash2, X,
  };

  readonly typeIcons = {
    generator: Zap,
    transformer: Cog,
    ups: BatteryCharging,
    switchgear: ToggleRight,
    panel: CircuitBoard,
    chiller: Snowflake,
    ahu: Fan,
    ac: AirVent,
    vrf: Wind,
    cctv: Cctv,
    laptop: Laptop,
    desktop: Monitor,
    server: Server,
    network: Network,
    printer: Printer,
    alarm: BellRing,
    access: KeyRound,
    fire: Flame,
    forklift: Forklift,
    conveyor: Package,
    crane: Container,
    pallet: Boxes,
    car: Car,
    truck: Truck,
    drill: Drill,
    meter: Ruler,
    cabinet: Archive,
    furniture: Armchair,
  };

  readonly pageSizeOptions = [5, 10, 20];
  readonly statusChoices: Status[] = ['Active', 'Inactive'];

  private readonly api = inject(AssetTypes);
  private readonly categoryApi = inject(AssetCategoryService);
  private readonly dropdowns = inject(AssetDropdownService);
  private readonly destroyRef = inject(DestroyRef);

  types: AssetType[] = [];
  models: AssetModel[] = [];
  selectedTypeId: string | null = null;
  loading = false;
  loadError = '';
  modelsLoading = false;
  saving = false;

  /** Category choices (Parent › Child) and manufacturers for the popups. */
  categories: { value: string; label: string }[] = [];
  manufacturers: DropdownOption[] = [];

  // Types table
  typeSearch = '';
  typeCategory = '';
  typePager = new Pager();
  checkedTypes = new Set<string>();

  // Models table
  modelSearch = '';
  modelStatus: '' | Status = '';
  modelPager = new Pager();
  checkedModels = new Set<string>();

  // Add / edit type popup
  typeModalOpen = false;
  typeModalClosing = false;
  editingTypeId: string | null = null;
  typeModal: TypeForm = { ...EMPTY_TYPE };
  typeModalSubmitted = false;
  private typeCodeTouched = false;

  // Add / edit model popup
  modelModalOpen = false;
  modelModalClosing = false;
  editingModel: AssetModel | null = null;
  modelModal: ModelForm = { ...EMPTY_MODEL };
  modelModalSubmitted = false;

  openMenu: string | null = null;
  deleteTarget: { kind: 'type'; item: AssetType } | { kind: 'model'; item: AssetModel } | null = null;
  deleteClosing = false;

  private readonly closeTimers: Record<'type' | 'model' | 'delete', ReturnType<typeof setTimeout> | undefined> = {
    type: undefined, model: undefined, delete: undefined,
  };

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    this.loadTypes();
    this.loadChoices();
  }

  // ===== Loading =====

  /** GET /api/asset-category-types; keeps the selection where it still exists. */
  loadTypes(selectId?: string): void {
    this.loading = true;
    this.loadError = '';
    this.api
      .getTypes()
      .pipe(finalize(() => (this.loading = false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => {
          this.types = (rows ?? []).map((r) => this.toType(r)).sort((a, b) => a.name.localeCompare(b.name));
          if (selectId) this.selectedTypeId = selectId;
          if (!this.selectedType) this.selectedTypeId = this.types[0]?.id ?? null;
          [...this.checkedTypes].forEach((id) => !this.types.some((t) => t.id === id) && this.checkedTypes.delete(id));
          this.loadModels();
        },
        error: (err) => (this.loadError = this.api.errorMessage(err, 'Could not load asset types.')),
      });
  }

  /** GET /api/asset-models?assetTypeId=… for the selected type. */
  loadModels(): void {
    const typeId = this.selectedTypeId;
    if (!typeId) {
      this.models = [];
      return;
    }
    this.modelsLoading = true;
    this.api
      .getModels({ assetTypeId: typeId })
      .pipe(finalize(() => (this.modelsLoading = false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rows) => {
          if (typeId !== this.selectedTypeId) return; // a newer selection is loading
          this.models = (rows ?? []).map((r) => this.toModel(r));
          [...this.checkedModels].forEach((id) => !this.models.some((m) => m.id === id) && this.checkedModels.delete(id));
        },
        error: (err) => this.showToast(this.api.errorMessage(err, 'Could not load models.')),
      });
  }

  /** Categories (asset categories API) and manufacturers (Master Data) for the popups. */
  private loadChoices(): void {
    forkJoin({ categories: this.categoryApi.getAll(), manufacturers: this.dropdowns.getManufacturers() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ categories, manufacturers }) => {
          this.categories = this.categoryChoices(categories ?? []);
          this.manufacturers = manufacturers;
        },
        error: () => this.showToast('Could not load categories.'),
      });
  }

  /** Every category as "Parent › Child", sorted by that path. */
  private categoryChoices(rows: AssetCategory[]): { value: string; label: string }[] {
    const byId = new Map(rows.map((r) => [r.id, r]));
    const path = (r: AssetCategory): string => {
      const names: string[] = [];
      const seen = new Set<string>();
      let current: AssetCategory | undefined = r;
      while (current && !seen.has(current.id)) {
        seen.add(current.id);
        names.unshift(current.name);
        current = current.parentCategoryId ? byId.get(current.parentCategoryId) : undefined;
      }
      return names.join(' › ');
    };
    return rows.map((r) => ({ value: r.id, label: path(r) })).sort((a, b) => a.label.localeCompare(b.label));
  }

  private toType(r: AssetTypeRecord): AssetType {
    return {
      id: r.id,
      name: r.typeName,
      code: r.typeCode,
      categoryId: r.categoryId,
      category: r.categoryName ?? '–',
      description: r.description ?? '',
      status: toStatus(r.status),
      icon: iconFor(r.typeName) ?? iconFor(r.categoryName ?? '') ?? 'pallet',
      image: r.imageUrl || undefined,
      modelCount: r.modelCount ?? 0,
      record: r,
    };
  }

  private toModel(r: AssetModelRecord): AssetModel {
    return { id: r.id, name: r.modelName, code: r.modelCode, manufacturer: r.manufacturerName, status: toStatus(r.status), record: r };
  }

  /** Runs a write, shows the result, then reloads types (and the selected type's models). */
  private write<T>(call: Observable<T>, success: (result: T) => string, after?: (result: T) => void, selectId?: string): void {
    this.saving = true;
    call.pipe(finalize(() => (this.saving = false)), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        after?.(result);
        this.showToast(success(result));
        this.loadTypes(selectId);
      },
      error: (err) => this.showToast(this.api.errorMessage(err, 'Something went wrong. Please try again.')),
    });
  }

  // ===== Lookups =====

  /** Categories used by at least one type, for the table filter. */
  get categoryOptions(): string[] {
    return [...new Set(this.types.map((t) => t.category))].sort((a, b) => a.localeCompare(b));
  }

  get selectedType(): AssetType | null {
    return this.types.find((t) => t.id === this.selectedTypeId) ?? null;
  }

  // ===== Types table =====

  get filteredTypes(): AssetType[] {
    const q = this.typeSearch.trim().toLowerCase();
    return this.types.filter(
      (t) =>
        (!q || [t.name, t.code, t.category, t.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeCategory || t.category === this.typeCategory),
    );
  }

  get pageTypes(): AssetType[] {
    return this.typePager.slice(this.filteredTypes);
  }

  onTypeFilterChange(): void {
    this.typePager.page = 1;
  }

  selectType(t: AssetType): void {
    if (t.id === this.selectedTypeId) return;
    this.selectedTypeId = t.id;
    this.models = [];
    this.modelSearch = '';
    this.modelStatus = '';
    this.modelPager.page = 1;
    this.checkedModels.clear();
    this.loadModels();
  }

  get allTypesChecked(): boolean {
    return this.pageTypes.length > 0 && this.pageTypes.every((t) => this.checkedTypes.has(t.id));
  }

  get someTypesChecked(): boolean {
    return !this.allTypesChecked && this.pageTypes.some((t) => this.checkedTypes.has(t.id));
  }

  toggleAllTypes(): void {
    const on = !this.allTypesChecked;
    this.pageTypes.forEach((t) => (on ? this.checkedTypes.add(t.id) : this.checkedTypes.delete(t.id)));
  }

  toggleChecked(set: Set<string>, id: string): void {
    if (set.has(id)) set.delete(id);
    else set.add(id);
  }

  /** PUT each checked type with the new status. */
  setTypeStatusForChecked(status: Status): void {
    const rows = this.types.filter((t) => this.checkedTypes.has(t.id) && t.status !== status);
    if (!rows.length || this.saving) {
      this.checkedTypes.clear();
      return;
    }
    this.write(
      forkJoin(rows.map((t) => this.api.updateType(t.id, { ...this.api.toTypeRequest(t.record), status }))),
      (saved) => `${saved.length} asset type${saved.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`,
      () => this.checkedTypes.clear(),
    );
  }

  toggleTypeStatus(t: AssetType): void {
    this.openMenu = null;
    const status: Status = t.status === 'Active' ? 'Inactive' : 'Active';
    this.write(
      this.api.updateType(t.id, { ...this.api.toTypeRequest(t.record), status }),
      (saved) => `${saved.typeName} is now ${status.toLowerCase()}.`,
    );
  }

  isTypeCodeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return !!c && this.types.some((t) => t.id !== exceptId && t.code.toUpperCase() === c);
  }

  private validateType(f: TypeForm, exceptId: string | null): string | null {
    if (!f.name.trim()) return 'Type name is required.';
    if (!f.code.trim()) return 'Type code is required.';
    if (!f.categoryId) return 'Category is required.';
    if (this.isTypeCodeTaken(f.code, exceptId)) return `Code "${f.code.trim().toUpperCase()}" is already used by another type.`;
    return null;
  }

  // ===== Add / edit type popup =====

  openTypeModal(t?: AssetType): void {
    this.openMenu = null;
    this.editingTypeId = t?.id ?? null;
    this.typeModal = t
      ? { name: t.name, code: t.code, categoryId: t.categoryId, description: t.description, status: t.status }
      : { ...EMPTY_TYPE, categoryId: this.categories.find((c) => c.label.endsWith(this.typeCategory) && this.typeCategory)?.value ?? '' };
    this.typeModalSubmitted = false;
    this.typeCodeTouched = !!t; // editing keeps the saved code
    this.cancelClose('type');
    this.typeModalOpen = true;
  }

  get editingType(): AssetType | null {
    return this.types.find((t) => t.id === this.editingTypeId) ?? null;
  }

  /** Suggests a code from the name until the user types their own. */
  onTypeNameChange(): void {
    if (this.typeCodeTouched) return;
    const words = this.typeModal.name.trim().split(/\s+/).filter(Boolean);
    this.typeModal.code = (words.length > 1 ? words.map((w) => w[0]).join('') : (words[0] ?? '').slice(0, 3))
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '');
  }

  markTypeCodeTouched(): void {
    this.typeCodeTouched = true;
  }

  /** POST (add) or PUT (edit) /api/asset-category-types. */
  saveTypeModal(): void {
    this.typeModalSubmitted = true;
    if (this.validateType(this.typeModal, this.editingTypeId) || this.saving) return;
    const f = this.typeModal;
    const values = {
      categoryId: f.categoryId,
      typeName: f.name.trim(),
      typeCode: f.code.trim().toUpperCase(),
      description: f.description.trim() || null,
      status: f.status,
    };
    const editing = this.editingType;
    if (editing) {
      this.write(
        this.api.updateType(editing.id, { ...this.api.toTypeRequest(editing.record), ...values }),
        (saved) => `${saved.typeName} updated.`,
        () => this.closeTypeModal(),
      );
      return;
    }
    this.write(
      this.api.createType(values),
      (created) => `${created.typeName} added.`,
      () => {
        this.closeTypeModal();
        this.typeSearch = '';
        this.typeCategory = '';
      },
      undefined,
    );
  }

  closeTypeModal(): void {
    this.close('type', () => (this.typeModalOpen = false));
  }

  /** Delete button in the edit-type popup. */
  deleteTypeFromModal(): void {
    const t = this.editingType;
    if (!t) return;
    this.closeTypeModal();
    this.askDeleteType(t);
  }

  // ===== Models table =====

  get filteredModels(): AssetModel[] {
    const q = this.modelSearch.trim().toLowerCase();
    return this.models.filter(
      (m) =>
        (!q || [m.name, m.code, m.manufacturer].some((v) => v.toLowerCase().includes(q))) &&
        (!this.modelStatus || m.status === this.modelStatus),
    );
  }

  get pageModels(): AssetModel[] {
    return this.modelPager.slice(this.filteredModels);
  }

  onModelFilterChange(): void {
    this.modelPager.page = 1;
  }

  get allModelsChecked(): boolean {
    return this.pageModels.length > 0 && this.pageModels.every((m) => this.checkedModels.has(m.id));
  }

  get someModelsChecked(): boolean {
    return !this.allModelsChecked && this.pageModels.some((m) => this.checkedModels.has(m.id));
  }

  toggleAllModels(): void {
    const on = !this.allModelsChecked;
    this.pageModels.forEach((m) => (on ? this.checkedModels.add(m.id) : this.checkedModels.delete(m.id)));
  }

  /** PUT each checked model with the new status. */
  setModelStatusForChecked(status: Status): void {
    const rows = this.models.filter((m) => this.checkedModels.has(m.id) && m.status !== status);
    if (!rows.length || this.saving) {
      this.checkedModels.clear();
      return;
    }
    this.write(
      forkJoin(rows.map((m) => this.api.updateModel(m.id, { ...this.api.toModelRequest(m.record), status }))),
      (saved) => `${saved.length} model${saved.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`,
      () => this.checkedModels.clear(),
    );
  }

  toggleModelStatus(m: AssetModel): void {
    this.openMenu = null;
    const status: Status = m.status === 'Active' ? 'Inactive' : 'Active';
    this.write(
      this.api.updateModel(m.id, { ...this.api.toModelRequest(m.record), status }),
      (saved) => `${saved.modelName} is now ${status.toLowerCase()}.`,
    );
  }

  viewModelAssets(m: AssetModel): void {
    this.openMenu = null;
    this.router.navigate(['/assets/list'], { queryParams: { type: this.selectedType?.code, model: m.code } });
  }

  // ===== Add / edit model popup =====

  openModelModal(m?: AssetModel): void {
    this.openMenu = null;
    this.editingModel = m ?? null;
    this.modelModal = m
      ? {
          name: m.name,
          code: m.code,
          // Older records may only carry the name; match it to a manufacturer when possible.
          manufacturerId: m.record.manufacturerId ?? this.manufacturers.find((o) => o.label === m.manufacturer)?.value ?? '',
          status: m.status,
        }
      : { ...EMPTY_MODEL };
    this.modelModalSubmitted = false;
    this.cancelClose('model');
    this.modelModalOpen = true;
  }

  isModelCodeTaken(code: string): boolean {
    const c = code.trim().toUpperCase();
    return !!c && this.models.some((m) => m.id !== this.editingModel?.id && m.code.toUpperCase() === c);
  }

  /** POST (add) or PUT (edit) /api/asset-models. */
  saveModelModal(): void {
    this.modelModalSubmitted = true;
    const f = this.modelModal;
    const t = this.selectedType;
    const manufacturer = this.manufacturers.find((o) => o.value === f.manufacturerId);
    if (!t || !f.name.trim() || !f.code.trim() || !manufacturer || this.isModelCodeTaken(f.code) || this.saving) return;

    const request = {
      assetTypeId: t.id,
      modelName: f.name.trim(),
      modelCode: f.code.trim().toUpperCase(),
      manufacturerId: manufacturer.value,
      manufacturerName: manufacturer.label,
      status: f.status,
    };
    const editing = this.editingModel;
    this.write(
      editing ? this.api.updateModel(editing.id, request) : this.api.createModel(request),
      (saved) => (editing ? `${saved.modelName} updated.` : `${saved.modelName} added to ${t.name}.`),
      () => this.closeModelModal(),
    );
  }

  closeModelModal(): void {
    this.close('model', () => (this.modelModalOpen = false));
  }

  // ===== Delete =====

  askDeleteType(t: AssetType): void {
    this.openMenu = null;
    this.cancelClose('delete');
    this.deleteTarget = { kind: 'type', item: t };
  }

  askDeleteModel(m: AssetModel): void {
    this.openMenu = null;
    this.cancelClose('delete');
    this.deleteTarget = { kind: 'model', item: m };
  }

  /** DELETE the type (refused while it has models or field layouts) or the model. */
  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target || this.saving) return;
    const call = target.kind === 'type' ? this.api.deleteType(target.item.id) : this.api.deleteModel(target.item.id);
    this.write(call, () => `${target.item.name} deleted.`, () => {
      this.closeDelete();
      if (target.kind === 'type') this.checkedTypes.delete(target.item.id);
      else this.checkedModels.delete(target.item.id);
    });
  }

  closeDelete(): void {
    this.close('delete', () => (this.deleteTarget = null));
  }

  // ===== Popup close animation =====

  /** Plays the popup-out animation, then removes the popup. */
  private close(which: 'type' | 'model' | 'delete', remove: () => void): void {
    const open = which === 'type' ? this.typeModalOpen : which === 'model' ? this.modelModalOpen : !!this.deleteTarget;
    if (!open || this.isClosing(which)) return;
    this.setClosing(which, true);
    this.closeTimers[which] = setTimeout(() => {
      remove();
      this.setClosing(which, false);
    }, POPUP_CLOSE_MS);
  }

  private cancelClose(which: 'type' | 'model' | 'delete'): void {
    clearTimeout(this.closeTimers[which]);
    this.setClosing(which, false);
  }

  private isClosing(which: 'type' | 'model' | 'delete'): boolean {
    return which === 'type' ? this.typeModalClosing : which === 'model' ? this.modelModalClosing : this.deleteClosing;
  }

  private setClosing(which: 'type' | 'model' | 'delete', value: boolean): void {
    if (which === 'type') this.typeModalClosing = value;
    else if (which === 'model') this.modelModalClosing = value;
    else this.deleteClosing = value;
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
    this.closeTypeModal();
    this.closeModelModal();
    this.closeDelete();
  }

  trackById(_: number, row: { id: string }): string {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
