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
import { apiErrorMessage } from '../services/asset-api';
import {
  AppAssetModel,
  AppAssetType,
  AssetTypeFormValue,
  AssetTypesModelsService,
  CategoryOption,
  Status,
} from './asset-types-models.service';

interface AssetModel {
  id: string;
  name: string;
  code: string;
  manufacturerId: string | null;
  manufacturer: string;
  /** Registered assets of this model; the asset list API does not expose a model, so this stays 0. */
  assetCount: number;
  status: Status;
}

interface AssetType {
  id: string;
  name: string;
  code: string;
  categoryId: string;
  /** Category name, for display and search. */
  category: string;
  description: string;
  status: Status;
  icon: keyof AssetTypesModels['typeIcons'];
  /** Image (URL or data URL) stored on the type; the icon is shown when absent. */
  image?: string;
  models: AssetModel[];
}

/** A model with the asset type it belongs to, as listed on the Models tab. */
interface ModelRow {
  m: AssetModel;
  t: AssetType;
}

type PageTab = 'types' | 'models';

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
  manufacturer: string;
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

/** Icon picked from keywords in the type name (the API stores no icon). */
const ICON_KEYWORDS: [RegExp, AssetType['icon']][] = [
  [/generat/i, 'generator'],
  [/transformer/i, 'transformer'],
  [/\bups\b|battery/i, 'ups'],
  [/switchgear|switch gear/i, 'switchgear'],
  [/panel/i, 'panel'],
  [/chiller|cool|refrig/i, 'chiller'],
  [/\bahu\b|air handling|fan/i, 'ahu'],
  [/\bac\b|air condition/i, 'ac'],
  [/vrf|ventilat/i, 'vrf'],
  [/cctv|camera/i, 'cctv'],
  [/laptop|notebook/i, 'laptop'],
  [/desktop|monitor|workstation/i, 'desktop'],
  [/server/i, 'server'],
  [/network|router|switch/i, 'network'],
  [/printer|scanner/i, 'printer'],
  [/alarm|detector/i, 'alarm'],
  [/access|door|card/i, 'access'],
  [/fire/i, 'fire'],
  [/forklift/i, 'forklift'],
  [/conveyor/i, 'conveyor'],
  [/crane/i, 'crane'],
  [/truck|van|lorry/i, 'truck'],
  [/car\b|vehicle|suv/i, 'car'],
  [/drill|tool/i, 'drill'],
  [/meter|gauge|measur|instrument/i, 'meter'],
  [/cabinet|storage|filing/i, 'cabinet'],
  [/furniture|chair|desk|table/i, 'furniture'],
];

function iconFor(name: string): AssetType['icon'] {
  return ICON_KEYWORDS.find(([re]) => re.test(name))?.[1] ?? 'pallet';
}

function toModel(m: AppAssetModel): AssetModel {
  return { id: m.id, name: m.name, code: m.code, manufacturerId: m.manufacturerId, manufacturer: m.manufacturer, assetCount: 0, status: m.status };
}

const EMPTY_TYPE: TypeForm = { name: '', code: '', categoryId: '', description: '', status: 'Active' };
const EMPTY_MODEL: ModelForm = { name: '', code: '', manufacturer: '', status: 'Active' };
const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

@Component({
  selector: 'app-asset-types-models',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
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

  readonly pageSizeOptions = [10, 25, 50];

  types: AssetType[] = [];
  /** Every asset category, for the category picker and filter. */
  categories: CategoryOption[] = [];
  selectedTypeId = '';
  /** Asset Types tab (types table + details) or Models tab (models of every type). */
  tab: PageTab = 'types';

  /** True while types, models and categories are being fetched. */
  loading = false;
  /** Set when the page data could not be loaded. */
  loadError = '';
  /** True while a create/update/delete request is in flight (guards double submits). */
  saving = false;

  // Types table
  typeSearch = '';
  /** Category filter (category id), '' for all categories. */
  typeCategory = '';
  typePager = new Pager();
  checkedTypes = new Set<string>();

  // Type details (read-only until "Edit")
  editing = false;
  detailForm: TypeForm = { ...EMPTY_TYPE };
  detailSubmitted = false;

  // Models table
  modelSearch = '';
  /** Asset type filter (type id), '' for all types. */
  modelType: string = '';
  modelStatus: '' | Status = '';
  modelPager = new Pager();
  checkedModels = new Set<string>();

  // Add-type modal
  typeModalOpen = false;
  typeModal: TypeForm = { ...EMPTY_TYPE };
  typeModalSubmitted = false;
  private typeCodeTouched = false;

  // Add/edit-model modal
  modelModalOpen = false;
  editingModel: AssetModel | null = null;
  modelModal: ModelForm = { ...EMPTY_MODEL };
  /** Asset type the model in the modal belongs to. */
  modelModalTypeId: string | null = null;
  modelModalSubmitted = false;

  openMenu: string | null = null;
  deleteTarget: { kind: 'type'; item: AssetType } | { kind: 'model'; item: AssetModel } | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private readonly router: Router,
    private readonly api: AssetTypesModelsService,
  ) {
    this.load();
  }

  // ===== Loading =====

  /** Fetches categories, types and models; keeps the current selection when it still exists. */
  load(): void {
    this.loading = true;
    this.loadError = '';
    forkJoin({ categories: this.api.getCategories(), types: this.api.getTypes(), models: this.api.getModels() }).subscribe({
      next: ({ categories, types, models }) => {
        this.loading = false;
        this.categories = categories;
        this.types = this.buildTypes(types, models);
        if (!this.selectedType) {
          this.selectedTypeId = this.types[0]?.id ?? '';
          this.editing = false;
        }
        if (this.modelType && !this.types.some((t) => t.id === this.modelType)) this.modelType = '';
        if (!this.editing) this.loadDetailForm();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load asset types and models.');
      },
    });
  }

  private buildTypes(types: AppAssetType[], models: AppAssetModel[]): AssetType[] {
    const byType = new Map<string, AssetModel[]>();
    for (const m of models) {
      const list = byType.get(m.assetTypeId) ?? [];
      list.push(toModel(m));
      byType.set(m.assetTypeId, list);
    }
    return types.map((t) => this.toType(t, byType.get(t.id) ?? []));
  }

  private toType(t: AppAssetType, models: AssetModel[]): AssetType {
    return {
      id: t.id,
      name: t.name,
      code: t.code,
      categoryId: t.categoryId,
      category: t.categoryName || this.categoryName(t.categoryId),
      description: t.description,
      status: t.status,
      icon: iconFor(t.name),
      image: t.imageUrl ?? undefined,
      models,
    };
  }

  private typeRequest(t: AssetType, overrides: Partial<AssetTypeFormValue> = {}): AssetTypeFormValue {
    return {
      categoryId: t.categoryId,
      name: t.name,
      code: t.code,
      description: t.description,
      status: t.status,
      imageUrl: t.image ?? null,
      ...overrides,
    };
  }

  private modelRequest(m: AssetModel, typeId: string, status: Status = m.status) {
    return { assetTypeId: typeId, name: m.name, code: m.code, manufacturerId: m.manufacturerId, manufacturer: m.manufacturer, status };
  }

  // ===== Lookups =====

  /** Categories for the pickers, labelled with their parent path ("Parent › Child"). */
  get categoryOptions(): { id: string; label: string }[] {
    const byId = new Map(this.categories.map((c) => [c.id, c]));
    const label = (c: CategoryOption): string => {
      const names: string[] = [];
      const seen = new Set<string>();
      let current: CategoryOption | undefined = c;
      while (current && !seen.has(current.id)) {
        seen.add(current.id);
        names.unshift(current.name);
        current = current.parentId ? byId.get(current.parentId) : undefined;
      }
      return names.join(' › ');
    };
    return this.categories.map((c) => ({ id: c.id, label: label(c) })).sort((a, b) => a.label.localeCompare(b.label));
  }

  categoryName(id: string): string {
    return this.categories.find((c) => c.id === id)?.name ?? '';
  }

  get selectedType(): AssetType | null {
    return this.types.find((t) => t.id === this.selectedTypeId) ?? null;
  }

  get manufacturerOptions(): string[] {
    return [...new Set(this.types.flatMap((t) => t.models.map((m) => m.manufacturer)).filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  typeAssetCount(t: AssetType): number {
    return t.models.reduce((sum, m) => sum + m.assetCount, 0);
  }

  // ===== Types table =====

  get filteredTypes(): AssetType[] {
    const q = this.typeSearch.trim().toLowerCase();
    return this.types.filter(
      (t) =>
        (!q || [t.name, t.code, t.category, t.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeCategory || t.categoryId === this.typeCategory),
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
    if (this.editing && !confirm('Discard unsaved changes to this asset type?')) return;
    this.selectedTypeId = t.id;
    this.editing = false;
    this.loadDetailForm();
  }

  // ===== Tabs =====

  setTab(tab: PageTab): void {
    if (tab === this.tab) return;
    if (this.editing && !confirm('Discard unsaved changes to this asset type?')) return;
    this.editing = false;
    this.loadDetailForm();
    this.tab = tab;
    this.openMenu = null;
  }

  /** Opens the Models tab filtered to one asset type. */
  showModelsOf(t: AssetType): void {
    this.openMenu = null;
    this.setTab('models');
    if (this.tab !== 'models') return;
    this.modelType = t.id;
    this.modelSearch = '';
    this.modelStatus = '';
    this.onModelFilterChange();
  }

  get modelCount(): number {
    return this.types.reduce((sum, t) => sum + t.models.length, 0);
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

  setTypeStatusForChecked(status: Status): void {
    const rows = this.types.filter((t) => this.checkedTypes.has(t.id));
    if (!rows.length) return;
    forkJoin(rows.map((t) => this.api.updateType(t.id, this.typeRequest(t, { status })))).subscribe({
      next: () => {
        rows.forEach((t) => (t.status = status));
        this.checkedTypes.clear();
        this.showToast(`${rows.length} asset type${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
        if (!this.editing) this.loadDetailForm();
      },
      error: (err) => {
        this.showToast(apiErrorMessage(err, 'Could not update the status of every selected asset type.'));
        this.load();
      },
    });
  }

  // ===== Type details =====

  private loadDetailForm(): void {
    const t = this.selectedType;
    this.detailForm = t
      ? { name: t.name, code: t.code, categoryId: t.categoryId, description: t.description, status: t.status }
      : { ...EMPTY_TYPE };
    this.detailSubmitted = false;
  }

  startEdit(t?: AssetType): void {
    this.openMenu = null;
    if (t) this.selectType(t);
    if (this.selectedTypeId === t?.id || !t) this.editing = true;
  }

  cancelEdit(): void {
    this.editing = false;
    this.loadDetailForm();
  }

  saveDetails(): void {
    const t = this.selectedType;
    if (!t || this.saving) return;
    this.detailSubmitted = true;
    const error = this.validateType(this.detailForm, t.id);
    if (error) {
      this.showToast(error);
      return;
    }
    this.saving = true;
    this.api.updateType(t.id, this.typeRequest(t, this.cleanType(this.detailForm))).subscribe({
      next: (saved) => {
        this.saving = false;
        Object.assign(t, this.toType(saved, t.models));
        this.editing = false;
        this.loadDetailForm();
        this.showToast(`${t.name} updated.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not save the asset type.'));
      },
    });
  }

  private cleanType(f: TypeForm): TypeForm {
    return { ...f, name: f.name.trim(), code: f.code.trim().toUpperCase(), categoryId: f.categoryId.trim(), description: f.description.trim() };
  }

  isTypeCodeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return !!c && this.types.some((t) => t.id !== exceptId && t.code.toUpperCase() === c);
  }

  private validateType(f: TypeForm, exceptId: string | null): string | null {
    if (!f.name.trim()) return 'Type name is required.';
    if (!f.code.trim()) return 'Type code is required.';
    if (!f.categoryId.trim()) return 'Category is required.';
    if (this.isTypeCodeTaken(f.code, exceptId)) return `Code "${f.code.trim().toUpperCase()}" is already used by another type.`;
    return null;
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const target = this.selectedType;
    if (!file || !target) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.showToast('Please choose a JPG or PNG image.');
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      this.showToast('Image must be 2 MB or smaller.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => this.saveImage(target, reader.result as string, 'Type image updated.');
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    if (this.selectedType) this.saveImage(this.selectedType, null, 'Type image removed.');
  }

  /** The image is stored on the type itself (PUT with imageUrl); other fields keep their saved values. */
  private saveImage(target: AssetType, image: string | null, message: string): void {
    this.api.updateType(target.id, this.typeRequest(target, { imageUrl: image })).subscribe({
      next: (saved) => {
        target.image = saved.imageUrl ?? undefined;
        this.showToast(message);
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not update the type image.')),
    });
  }

  toggleTypeStatus(t: AssetType): void {
    this.openMenu = null;
    const status: Status = t.status === 'Active' ? 'Inactive' : 'Active';
    this.api.updateType(t.id, this.typeRequest(t, { status })).subscribe({
      next: (saved) => {
        t.status = saved.status;
        if (t.id === this.selectedTypeId && !this.editing) this.loadDetailForm();
        this.showToast(`${t.name} is now ${t.status.toLowerCase()}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not change the status of ${t.name}.`)),
    });
  }

  // ===== Add type modal =====

  openTypeModal(): void {
    this.typeModal = { ...EMPTY_TYPE, categoryId: this.typeCategory };
    this.typeModalSubmitted = false;
    this.typeCodeTouched = false;
    this.typeModalOpen = true;
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

  saveTypeModal(): void {
    this.typeModalSubmitted = true;
    if (this.validateType(this.typeModal, null) || this.saving) return;
    this.saving = true;
    this.api.createType(this.cleanType(this.typeModal)).subscribe({
      next: (saved) => {
        this.saving = false;
        const created = this.toType(saved, []);
        this.types.push(created);
        this.types.sort((a, b) => a.name.localeCompare(b.name));
        this.typeModalOpen = false;
        this.typeSearch = '';
        this.typeCategory = '';
        const index = this.types.indexOf(created);
        this.typePager.page = Math.floor(index / this.typePager.size) + 1;
        this.selectType(created);
        this.showToast(`${created.name} added.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not add the asset type.'));
      },
    });
  }

  // ===== Models table =====

  /** Every model with its asset type, in type order. */
  private get modelRows(): ModelRow[] {
    return this.types.flatMap((t) => t.models.map((m) => ({ m, t })));
  }

  get filteredModels(): ModelRow[] {
    const q = this.modelSearch.trim().toLowerCase();
    return this.modelRows.filter(
      ({ m, t }) =>
        (this.modelType === '' || t.id === this.modelType) &&
        (!q || [m.name, m.code, m.manufacturer, t.name].some((v) => v.toLowerCase().includes(q))) &&
        (!this.modelStatus || m.status === this.modelStatus),
    );
  }

  get pageModels(): ModelRow[] {
    return this.modelPager.slice(this.filteredModels);
  }

  /** The type the Models tab is filtered to, if any. */
  get modelFilterType(): AssetType | null {
    return this.modelType === '' ? null : (this.types.find((t) => t.id === this.modelType) ?? null);
  }

  onModelFilterChange(): void {
    this.modelPager.page = 1;
  }

  /** Asset type a model belongs to. */
  typeOf(m: AssetModel): AssetType | null {
    return this.types.find((t) => t.models.includes(m)) ?? null;
  }

  get allModelsChecked(): boolean {
    return this.pageModels.length > 0 && this.pageModels.every((r) => this.checkedModels.has(r.m.id));
  }

  get someModelsChecked(): boolean {
    return !this.allModelsChecked && this.pageModels.some((r) => this.checkedModels.has(r.m.id));
  }

  toggleAllModels(): void {
    const on = !this.allModelsChecked;
    this.pageModels.forEach((r) => (on ? this.checkedModels.add(r.m.id) : this.checkedModels.delete(r.m.id)));
  }

  setModelStatusForChecked(status: Status): void {
    const rows = this.modelRows.filter((r) => this.checkedModels.has(r.m.id));
    if (!rows.length) return;
    forkJoin(rows.map((r) => this.api.updateModel(r.m.id, this.modelRequest(r.m, r.t.id, status)))).subscribe({
      next: () => {
        rows.forEach((r) => (r.m.status = status));
        this.checkedModels.clear();
        this.showToast(`${rows.length} model${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
      },
      error: (err) => {
        this.showToast(apiErrorMessage(err, 'Could not update the status of every selected model.'));
        this.load();
      },
    });
  }

  toggleModelStatus(m: AssetModel): void {
    this.openMenu = null;
    const t = this.typeOf(m);
    if (!t) return;
    const status: Status = m.status === 'Active' ? 'Inactive' : 'Active';
    this.api.updateModel(m.id, this.modelRequest(m, t.id, status)).subscribe({
      next: (saved) => {
        m.status = saved.status;
        this.showToast(`${m.name} is now ${m.status.toLowerCase()}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not change the status of ${m.name}.`)),
    });
  }

  viewModelAssets(m: AssetModel): void {
    this.openMenu = null;
    this.router.navigate(['/assets/list'], { queryParams: { type: this.typeOf(m)?.code, model: m.code } });
  }

  // ===== Add / edit model modal =====

  get modalType(): AssetType | null {
    return this.types.find((t) => t.id === this.modelModalTypeId) ?? null;
  }

  openModelModal(m?: AssetModel): void {
    this.openMenu = null;
    this.editingModel = m ?? null;
    this.modelModal = m ? { name: m.name, code: m.code, manufacturer: m.manufacturer, status: m.status } : { ...EMPTY_MODEL };
    // a new model goes to the type the table is filtered to, else the selected type
    this.modelModalTypeId = m ? (this.typeOf(m)?.id ?? null) : this.modelType === '' ? this.selectedTypeId || null : this.modelType;
    this.modelModalSubmitted = false;
    this.modelModalOpen = true;
  }

  /** Model codes are unique within their asset type. */
  isModelCodeTaken(code: string): boolean {
    const c = code.trim().toUpperCase();
    return !!c && (this.modalType?.models ?? []).some((m) => m !== this.editingModel && m.code.toUpperCase() === c);
  }

  saveModelModal(): void {
    this.modelModalSubmitted = true;
    const f = this.modelModal;
    const t = this.modalType;
    if (!t || !f.name.trim() || !f.code.trim() || !f.manufacturer.trim() || this.isModelCodeTaken(f.code) || this.saving) return;

    const values = { name: f.name.trim(), code: f.code.trim().toUpperCase(), manufacturer: f.manufacturer.trim(), status: f.status };
    const m = this.editingModel;
    // Keep the manufacturer id only while the manufacturer name is unchanged.
    const manufacturerId = m && m.manufacturer === values.manufacturer ? m.manufacturerId : null;
    const request = { assetTypeId: t.id, manufacturerId, ...values };
    this.saving = true;

    if (m) {
      this.api.updateModel(m.id, request).subscribe({
        next: (saved) => {
          this.saving = false;
          Object.assign(m, { ...toModel(saved), assetCount: m.assetCount });
          // moved to another asset type
          const from = this.typeOf(m);
          if (from && from !== t) {
            from.models.splice(from.models.indexOf(m), 1);
            t.models.push(m);
          }
          this.modelModalOpen = false;
          this.showToast(`${values.name} updated.`);
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'Could not save the model.'));
        },
      });
    } else {
      this.api.createModel(request).subscribe({
        next: (saved) => {
          this.saving = false;
          t.models.push(toModel(saved));
          // show the new model: filter to its type and go to the last page
          this.modelType = t.id;
          this.modelSearch = '';
          this.modelStatus = '';
          this.modelPager.page = this.modelPager.totalPages(t.models.length);
          this.modelModalOpen = false;
          this.showToast(`${values.name} added to ${t.name}.`);
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'Could not add the model.'));
        },
      });
    }
  }

  // ===== Delete =====

  askDeleteType(t: AssetType): void {
    this.openMenu = null;
    const assets = this.typeAssetCount(t);
    if (assets > 0) {
      this.showToast(`${t.name} has ${assets} asset${assets === 1 ? '' : 's'} registered. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = { kind: 'type', item: t };
  }

  askDeleteModel(m: AssetModel): void {
    this.openMenu = null;
    if (m.assetCount > 0) {
      this.showToast(`${m.name} has ${m.assetCount} asset${m.assetCount === 1 ? '' : 's'} registered. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = { kind: 'model', item: m };
  }

  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target || this.saving) return;
    this.saving = true;
    const request = target.kind === 'type' ? this.api.deleteType(target.item.id) : this.api.deleteModel(target.item.id);
    request.subscribe({
      next: () => {
        this.saving = false;
        if (target.kind === 'type') {
          this.types = this.types.filter((t) => t !== target.item);
          this.checkedTypes.delete(target.item.id);
          if (this.modelType === target.item.id) this.modelType = '';
          if (target.item.id === this.selectedTypeId) {
            this.selectedTypeId = this.filteredTypes[0]?.id ?? this.types[0]?.id ?? '';
            this.editing = false;
            this.loadDetailForm();
          }
        } else {
          const models = this.typeOf(target.item)?.models ?? [];
          models.splice(models.indexOf(target.item), 1);
          this.checkedModels.delete(target.item.id);
        }
        this.showToast(`${target.item.name} deleted.`);
        this.deleteTarget = null;
      },
      error: (err) => {
        this.saving = false;
        this.deleteTarget = null;
        this.showToast(apiErrorMessage(err, `Could not delete ${target.item.name}.`));
      },
    });
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
    this.typeModalOpen = false;
    this.modelModalOpen = false;
    this.deleteTarget = null;
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
